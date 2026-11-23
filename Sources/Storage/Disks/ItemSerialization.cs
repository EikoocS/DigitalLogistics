using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DigitalLogistics.Storage.Items;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DigitalLogistics.Storage.Disks
{
    internal static class ItemSerialization
    {
        [ThreadStatic]
        private static int inspectionDepth;
        internal static bool Inspecting => inspectionDepth > 0;

        [Serializable]
        private sealed class ItemData : SaveState
        {
            public string itemFeatures;
        }

        internal static string Encode(GameItem item) => Encode(item, keepIdentity: false);

        internal static string EncodeSnapshot(GameItem item) => Encode(item, keepIdentity: true);

        private static string Encode(GameItem item, bool keepIdentity)
        {
            var store = PlayerStore.Instance;
            var savedFeatures = store.savedItemFeatureList;
            store.savedItemFeatureList = new List<ItemFeature>();
            try
            {
                var nodes = SaveManager.EncodeNodes(item);
                string features = Encoding.UTF8.GetString(ES3.Serialize(store.savedItemFeatureList));
                if (!keepIdentity)
                {
                    var ids = nodes.ToDictionary(node => node.uniqueId, node => (int)node.uuid);
                    var featureRecords = JArray.Parse(features);
                    foreach (JObject feature in featureRecords)
                        feature["parentItemUniqueId"] = ids[feature.Value<int>("parentItemUniqueId")];
                    features = featureRecords.ToString(Formatting.None);
                    foreach (var node in nodes)
                    {
                        node.uniqueId = (int)node.uuid;
                        var tags = node.itemState.dict.OrderBy(tag => tag.Key, StringComparer.Ordinal).ToArray();
                        node.itemState.dict.Clear();
                        foreach (var tag in tags) node.itemState.dict.Add(tag.Key, tag.Value);
                    }
                    nodes[0].unitCount = 1;
                    nodes[0].itemShape.SetTransform(0, 0, false, 0);
                    nodes[0].itemModifiedShape.SetTransform(0, 0, false, 0);
                }
                return JsonUtility.ToJson(new ItemData
                {
                    saveItems = nodes,
                    itemFeatures = features
                });
            }
            finally
            {
                store.savedItemFeatureList = savedFeatures;
            }
        }

        internal static T Inspect<T>(string json, int count, Func<GameItem, T> read)
        {
            inspectionDepth++;
            try
            {
                using var lease = new ItemInstanceLease(Decode(json, count), true);
                return read(lease.Item);
            }
            finally { inspectionDepth--; }
        }

        internal static GameItem Decode(string json, int count) => Decode(json, count, null);

        internal static GameItem DecodeSnapshot(string json, Action<GameItem> attach) => Decode(json, null, attach);

        private static GameItem Decode(string json, int? count, Action<GameItem> attach)
        {
            var data = JsonUtility.FromJson<ItemData>(json);
            Dictionary<int, int> ids = null;
            if (count.HasValue)
            {
                ids = data.saveItems.ToDictionary(node => node.uniqueId, _ => PlayerStore.Instance.GetUniqueID());
                foreach (var node in data.saveItems) node.uniqueId = ids[node.uniqueId];
                data.saveItems[0].unitCount = count.Value;
            }
            bool completed = false;
            try
            {
                var root = SaveManager.DecodeNodes(data.saveItems);
                attach?.Invoke(root);
                foreach (var node in data.saveItems)
                {
                    var item = node.tempLink;
                    if (attach != null && item == root) continue;
                    MachineHelper.LoadUpgrade(item);
                    SpriteHelper.UpdateWaterContainerSprite(item);
                    GeneralHelper.LoadLockItem(item);
                    PlantTraitList.LoadEdibleProduce(item);
                    PlantTraitList.LoadTasteFeature(item);
                    GeneralHelper.LoadNonOwnedToolTag(item);
                    GeneralHelper.LoadImportantStoryItemTag(item);
                    GeneralHelper.LoadHazardousWasteTag(item);
                    ScavHelper.LoadScavTokenTag(item);
                    LockHelper.LoadLockedContainer(item);
                    MachineBrokenHelper.LoadBrokenMachine(item);
                    MachineHelper.OnLoad(item);
                    MachineTurboBoosterAdv.LoadTurboBoostEligibleTag(item);
                    CurrentConverterHelper.OnLoad(item);
                    if (attach == null) item.onLoaded?.Invoke(item);
                }
                var items = data.saveItems.ToDictionary(node => node.uniqueId, node => node.tempLink);
                foreach (var item in items.Values) item.RemoveAllFeature();
                var features = ES3.Deserialize<List<ItemFeature>>(Encoding.UTF8.GetBytes(data.itemFeatures));
                foreach (var feature in features)
                {
                    if (ids != null) feature.parentItemUniqueId = ids[feature.parentItemUniqueId];
                    items[feature.parentItemUniqueId].itemFeatures.Add(feature);
                }
                if (attach != null)
                    foreach (var node in data.saveItems)
                        if (node.tempLink != root) node.tempLink.onLoaded?.Invoke(node.tempLink);
                completed = true;
                return root;
            }
            finally
            {
                if (!completed)
                    for (int i = data.saveItems.Count - 1; i >= 0; i--)
                        data.saveItems[i].tempLink?.Destroy();
            }
        }
    }
}
