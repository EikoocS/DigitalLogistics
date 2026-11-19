using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DigitalLogistics.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DigitalLogistics.Storage.Disks
{
    internal sealed class StorageDiskData
    {
        internal sealed class Entry
        {
            public int Id;
            public int Count;
            public int Cells;
            public string Item;
        }

        [JsonProperty("metadata")]
        public JObject Metadata { get; set; } = new();

        [JsonProperty("items")]
        internal List<Entry> Items { get; private set; } = new();
        private TagSystem fluid;
        internal TagSystem Fluid => fluid ??= new TagSystem();

        [JsonProperty("fluid", NullValueHandling = NullValueHandling.Ignore)]
        private string EncodedFluid
        {
            get => fluid == null ? null : JsonUtility.ToJson(new SaveItemNode { itemState = fluid });
            set => fluid = value == null ? null : JsonUtility.FromJson<SaveItemNode>(value).itemState;
        }

        internal int UsedCells => Items.Sum(item => item.Cells);

        internal string Encode(bool measure, out DiskPayloadSize size)
        {
            using var buffer = new MemoryStream();
            CountingWriteStream counter = null;
            using (var compression = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            {
                if (measure) counter = new CountingWriteStream(compression);
                using var text = new StreamWriter(counter ?? (Stream)compression);
                using var json = new JsonTextWriter(text);
                JsonSerializer.Create().Serialize(json, this);
            }
            string encoded = Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
            size = counter == null ? default : new DiskPayloadSize(counter.BytesWritten, buffer.Length, encoded.Length);
            return encoded;
        }

        internal static StorageDiskData Decode(string value)
        {
            using var buffer = new MemoryStream(Convert.FromBase64String(value));
            using var compression = new GZipStream(buffer, CompressionMode.Decompress);
            using var text = new StreamReader(compression);
            using var json = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
            return JsonSerializer.Create().Deserialize<StorageDiskData>(json);
        }
    }
}
