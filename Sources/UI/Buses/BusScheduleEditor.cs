using System;
using DigitalLogistics.Buses;
using TMPro;
using UnityEngine;
using static DigitalLogistics.UI.Common.MachineryControlFactory;
using static DigitalLogistics.UI.Buses.BusRowControls;

namespace DigitalLogistics.UI.Buses
{
    internal static class BusScheduleEditor
    {
        internal const int HeaderHeight = Line;
        internal const int TransferEntryHeight = Line * 3 + RowGap;
        internal const int TriggerEntryHeight = Line * 2 + RowGap;

        internal static void Build<T>(BusRowControls controls, int y, ScheduledBusConfiguration<T> configuration, Func<bool> enabled,
            string limitKey, string amountKey, int amountWidth, Action<RectTransform, T> bindAmount)
            where T : BusScheduleEntry, new()
        {
            var root = controls.Root;
            LabelWidth(root, Padding, y, "ui.bus.schedule");
            CreateButton(root, new Vector2(-Padding, -y), new Vector2(48, 36), "+", () =>
            {
                BusPlanStore.Add(configuration, new T());
                controls.Rebuild();
            });

            y += HeaderHeight;
            foreach (var entry in BusPlanStore.GetPlans(configuration))
            {
                var filter = entry as FilteredBusScheduleEntry;
                int height = filter == null ? TriggerEntryHeight : TransferEntryHeight;
                var card = CreateSection(root, "ScheduleEntry", y, height - RowGap);
                var toggle = controls.BindToggle(card, new Vector2(Padding, -Padding),
                    () => entry.Enabled, value => entry.Enabled = value);
                controls.AddLateRefresh(() => toggle.SetDisabled(!enabled()));

                int x = NameX + LabelWidth(card, NameX, Padding, "ui.bus.run_order") + Padding;
                controls.BindInteger(CreateInput(card, new Vector2(x, -Padding), 64, stretch: false),
                    () => entry.Order, value => entry.Order = Mathf.Clamp(value, 1, 15));
                x += 64 + Padding * 2;
                x += LabelWidth(card, x, Padding, "ui.bus.run_sub_order") + Padding;
                controls.BindInteger(CreateInput(card, new Vector2(x, -Padding), 88, stretch: false),
                    () => entry.SubOrder, value => entry.SubOrder = value);
                CreateButton(card, new Vector2(-Padding, -Padding), new Vector2(48, 36), "×", () =>
                {
                    BusPlanStore.Remove(configuration, entry);
                    controls.Rebuild();
                });

                int fieldY = Padding + Line;
                if (filter != null)
                {
                    controls.BindFilter(CreateLabeledInput(card, fieldY, "ui.common.filter"), () => filter.Filter,
                        () => DigitalLogistics.Localize("ui.io_bus.inherit_filter"), filter.SetFilter,
                        inheritWhenEmpty: true);
                    fieldY += Line;
                }

                var amount = CreateStrip(card, "ScheduleAmount", new Vector2(-Padding, -fieldY), amountWidth, 36, right: true);
                var amountLabel = CreateLabel(amount, new Vector2(-Padding, 0), DigitalLogistics.Localize(amountKey));
                ((RectTransform)amountLabel.root.transform).pivot = Vector2.one;
                FitLabel(amountLabel);
                bindAmount(amount, entry);
                bool IsEntryEnabled() => enabled() && entry.Enabled;
                var limit = controls.BindToggle(card, new Vector2(Padding, -fieldY), () => entry.Limit,
                    value => entry.Limit = value, amount.gameObject.SetActive, DigitalLogistics.Localize(limitKey));
                controls.BindAvailability(card, IsEntryEnabled);
                controls.AddLateRefresh(() => limit.SetDisabled(!IsEntryEnabled()));
                y += height;
            }
        }

        private static int LabelWidth(RectTransform parent, int x, int y, string key) =>
            FitLabel(CreateLabel(parent, new Vector2(x, -y), DigitalLogistics.Localize(key)));

        internal static TMP_InputField CreateLabeledInput(RectTransform parent, int y, string key)
        {
            int x = Padding * 2 + LabelWidth(parent, Padding, y, key);
            return CreateInput(parent, new Vector2(x, -y), -Padding - x);
        }
    }
}
