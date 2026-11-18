using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DigitalLogistics.Storage
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
        internal int UsedCells => Items.Sum(item => item.Cells);

        internal string Encode()
        {
            using var buffer = new MemoryStream();
            using (var compression = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            using (var text = new StreamWriter(compression))
            using (var json = new JsonTextWriter(text))
                JsonSerializer.Create().Serialize(json, this);
            return Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
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
