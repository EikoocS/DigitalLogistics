using DigitalLogistics.Storage.Items;
using System.Collections.Generic;
using DigitalLogistics.Storage;
using System;

namespace DigitalLogistics.Controller
{
    internal sealed class StorageTerminalGroup
    {
        internal string Name { get; }
        internal List<CachedItem> Items { get; } = new List<CachedItem>();
        internal long TotalPrice { get; private set; }
        internal int ContrabandLevel { get; private set; }
        internal int? Heat { get; private set; }

        internal StorageTerminalGroup(string name) => Name = name;

        internal void SetItems(IEnumerable<CachedItem> items)
        {
            Items.Clear();
            TotalPrice = 0;
            ContrabandLevel = 0;
            Heat = null;
            foreach (var item in items) Add(item);
        }

        internal void Add(CachedItem item) => Insert(Items.Count, item);

        internal void Insert(int index, CachedItem item)
        {
            Items.Insert(index, item);
            TotalPrice += item.Price;
            ContrabandLevel = Math.Max(ContrabandLevel, item.ContrabandLevel);
            if (item.Heat.HasValue && (!Heat.HasValue || item.Heat.Value > Heat.Value)) Heat = item.Heat;
        }
    }
}
