using System.Collections.Generic;
using DigitalStorage.Storage;
using System;

namespace DigitalStorage.Controller
{
    internal sealed class StorageTerminalGroup
    {
        internal string Name { get; }
        internal List<StoredItem> Items { get; } = new List<StoredItem>();
        internal long TotalPrice { get; private set; }
        internal int ContrabandLevel { get; private set; }
        internal int? Heat { get; private set; }

        internal StorageTerminalGroup(string name) => Name = name;

        internal void SetItems(IEnumerable<StoredItem> items)
        {
            Items.Clear();
            TotalPrice = 0;
            ContrabandLevel = 0;
            Heat = null;
            foreach (var item in items) Add(item);
        }

        internal void Add(StoredItem item)
        {
            Items.Add(item);
            TotalPrice += item.Price;
            ContrabandLevel = Math.Max(ContrabandLevel, item.ContrabandLevel);
            if (item.Heat.HasValue && (!Heat.HasValue || item.Heat.Value > Heat.Value)) Heat = item.Heat;
        }
    }
}
