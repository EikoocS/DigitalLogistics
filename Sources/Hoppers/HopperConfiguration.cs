using System;
using System.Collections.Generic;
using System.Linq;
using DigitalLogistics.Filtering;
using DigitalLogistics.Item;
using DigitalLogistics.Storage;
using Newtonsoft.Json;

namespace DigitalLogistics.Hoppers
{
    internal sealed class HopperConfiguration
    {
        internal const string DefaultFilter = "True";
        public bool Disabled;
        public string Filter = string.Empty;
        public int InputIndex = -1;
        public int OutputIndex = -1;
        private string parsedFilter;
        private CachedItemExpression expression;

        internal CachedItemExpression GetExpression()
        {
            string filter = string.IsNullOrWhiteSpace(Filter) ? DefaultFilter : Filter;
            if (filter != parsedFilter)
            {
                expression = CachedItemExpressionParser.Parse(filter);
                parsedFilter = filter;
            }
            return expression;
        }

        private static string ModId => DigitalLogistics.Instance.Manifest.ID;
        private static string Key(Hopper hopper) => $"hopper.{(hopper.IsFluid ? "fluid" : "item")}.{hopper.GetUniqueID()}";

        internal static HopperConfiguration Load(Hopper hopper) =>
            JsonConvert.DeserializeObject<HopperConfiguration>(ModHelper.GetModData(ModId, Key(hopper), "{}"));

        internal void Save(Hopper hopper)
        {
            InputIndex = hopper.Input.Index;
            OutputIndex = hopper.Output.Index;
            ModHelper.SetModData(ModId, Key(hopper), JsonConvert.SerializeObject(this));
        }
    }

    internal sealed class HopperPort
    {
        private readonly Dictionary<GameItem, int> selections = new();
        internal GameItem Target { get; private set; }
        internal GameInventory[] Slots { get; private set; } = Array.Empty<GameInventory>();
        internal int Index => Slots.Length == 0 ? -1
            : selections.TryGetValue(Target, out var index) && index < Slots.Length ? index : 0;
        internal GameInventory Inventory => Index < 0 ? null : Slots[Index];
        internal bool Available => Index >= 0 && !ItemAccess.IsPinnedOrBlocked(Target);

        internal bool Update(GameItem target, GameInventory[] slots)
        {
            if (Target == target && Slots.SequenceEqual(slots)) return false;
            Target = target;
            Slots = slots;
            return true;
        }

        internal void Select(int index)
        {
            if (index >= 0 && index < Slots.Length) selections[Target] = index;
        }

        internal List<string> GetSlotNames() => Slots.Select((slot, index) =>
        {
            string name = slot == null ? Target.GetDisplayName(preferCustomName: true)
                : SharedProviderRegistry.GetName(slot, Target);
            return Slots.Length == 1 ? name : $"{index + 1}. {name}";
        }).ToList();
    }
}
