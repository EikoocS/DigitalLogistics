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

        internal static string Encode(GameItem item)
        {
            var store = PlayerStore.Instance;
            var savedFeatures = store.savedItemFeatureList;
            store.savedItemFeatureList = new List<ItemFeature>();
            try
            {
                var nodes = SaveManager.EncodeNodes(item);
                var ids = nodes.ToDictionary(node => node.uniqueId, node => (int)node.uuid);
                var features = JArray.Parse(Encoding.UTF8.GetString(ES3.Serialize(store.savedItemFeatureList)));
                foreach (JObject feature in features)
                    feature["parentItemUniqueId"] = ids[feature.Value<int>("parentItemUniqueId")];
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
                return JsonUtility.ToJson(new ItemData
                {
                    saveItems = nodes,
                    itemFeatures = features.ToString(Formatting.None)
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

        internal static GameItem Decode(string json, int count)
        {
            var data = JsonUtility.FromJson<ItemData>(json);
            var ids = data.saveItems.ToDictionary(node => node.uniqueId, _ => PlayerStore.Instance.GetUniqueID());
            foreach (var node in data.saveItems) node.uniqueId = ids[node.uniqueId];
            data.saveItems[0].unitCount = count;
            bool completed = false;
            try
            {
                var root = SaveManager.DecodeNodes(data.saveItems);
                foreach (var node in data.saveItems)
                {
                    var item = node.tempLink;
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
                    item.onLoaded?.Invoke(item);
                }
                var items = data.saveItems.ToDictionary(node => node.uniqueId, node => node.tempLink);
                foreach (var item in items.Values) item.RemoveAllFeature();
                var features = ES3.Deserialize<List<ItemFeature>>(Encoding.UTF8.GetBytes(data.itemFeatures));
                foreach (var feature in features)
                {
                    feature.parentItemUniqueId = ids[feature.parentItemUniqueId];
                    items[feature.parentItemUniqueId].itemFeatures.Add(feature);
                }
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
