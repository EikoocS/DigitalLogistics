using System.Collections.Generic;
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

        internal string Encode(bool measure, out DiskPayloadSize size) =>
            DiskPayloadCodec.Encode(text =>
            {
                using var json = new JsonTextWriter(text) { CloseOutput = false };
                JsonSerializer.Create().Serialize(json, this);
            }, measure, out size);

        internal static StorageDiskData Decode(string value) =>
            DiskPayloadCodec.Decode(value, text =>
            {
                using var json = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
                return JsonSerializer.Create().Deserialize<StorageDiskData>(json);
            });
    }
}
