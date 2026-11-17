using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalStorage.Storage
{
    internal sealed class TriggerBusAction
    {
        internal readonly struct Option
        {
            internal readonly string Id;
            internal readonly string Name;

            internal Option(string id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        internal readonly Action<GameItem, string> Trigger;
        internal readonly Func<GameItem, string, bool> CanTrigger;
        internal readonly Func<GameItem, Action, IDisposable> SubscribeInputs;
        internal readonly Func<Option[]> GetOptions;

        internal TriggerBusAction(Action<GameItem, string> trigger, Func<GameItem, string, bool> canTrigger,
            Func<GameItem, Action, IDisposable> subscribeInputs = null, Func<Option[]> getOptions = null)
        {
            Trigger = trigger;
            CanTrigger = canTrigger;
            SubscribeInputs = subscribeInputs;
            GetOptions = getOptions;
        }
    }

    internal static class TriggerBusActions
    {
        internal static readonly Dictionary<string, TriggerBusAction> Actions = new()
        {
            ["blender"] = new((item, _) => Blend(item), (item, _) => CanBlend(item),
                (item, changed) => WatchInputs(item, changed, false, 1, 2)),
            ["chem_finisher"] = new(FinishChemical, CanFinishChemical,
                (item, changed) => WatchInputs(item, changed, false, 1),
                () => ChemFinisherRecipe.chemFinisherRecipes
                    .Select(recipe => new TriggerBusAction.Option(recipe.identifier, recipe.displayName)).ToArray()),
            ["bottle_printer"] = new(PrintBottle, CanPrintBottle, getOptions: () => new[]
            {
                new TriggerBusAction.Option("small_bottled_water",
                    LocHelper.GetLocalizedMechanicOrDefault("mech_bottle_printer_btn_print_small", "Print Small Bottle")),
                new TriggerBusAction.Option("bottled_water",
                    LocHelper.GetLocalizedMechanic("mech_bottle_printer_btn_print_normal")),
                new TriggerBusAction.Option("large_bottled_water",
                    LocHelper.GetLocalizedMechanic("mech_bottle_printer_btn_print_large"))
            }),
            ["wine_bottle"] = new((item, _) => WineHelper.StartFermentation(item),
                (item, _) => CanFerment(item), (item, changed) => WatchInputs(item, changed, true, 0))
        };

        private static IDisposable WatchInputs(GameItem machine, Action changed, bool watchLiquid, params int[] indices) =>
            new TriggerBusInputWatcher(indices.Select(index => (GameInventory)MachineHelper.GetBodyStorage(machine, index)),
                watchLiquid ? machine : null, changed);

        private static bool CanBlend(GameItem machine)
        {
            var input = (GameInventory)MachineHelper.GetBodyStorage(machine, 1);
            var container = (GameSlotInventory)MachineHelper.GetBodyStorage(machine, 2);
            return container.childItem != null && !WaterHelper.IsFull(container.childItem)
                && input.childItems.Any(item => item.IsTag("BLENDABLE_TAG"));
        }

        private static bool CanFinishChemical(GameItem machine, string option)
        {
            var container = (GameSlotInventory)MachineHelper.GetBodyStorage(machine, 1);
            var recipe = ChemFinisherRecipe.GetRecipeById(option);
            return container.childItem != null && !WaterHelper.IsEmpty(container.childItem)
                && recipe.CanOutput(container.childItem)
                && CanPlace(machine, 2, option);
        }

        private static int BottleCost(string option) => option switch
        {
            "bottled_water" => MachineBottlePrinter.NORMAL_PRINT_COST,
            "large_bottled_water" => MachineBottlePrinter.LARGE_PRINT_COST,
            _ => MachineBottlePrinter.SMALL_PRINT_COST
        };

        private static bool CanPrintBottle(GameItem machine, string option)
        {
            if (option == "bottled_water" && !MachineBottlePrinter.IsNormalUnlocked(machine)
                || option == "large_bottled_water" && !MachineBottlePrinter.IsLargeUnlocked(machine)) return false;
            var battery = MachineHelper.GetBatterySlot(machine).childItem;
            return battery != null && PowerHelper.CanDrawPowerSource(battery, BottleCost(option))
                && CanPlace(machine, 1, option);
        }

        private static bool CanPlace(GameItem machine, int outputIndex, string identifier)
        {
            var product = DirectoryMaster.Item(identifier);
            try { return GraphUtils.CanAccept(MachineHelper.GetBodyStorage(machine, outputIndex), product); }
            finally { product.Destroy(); }
        }

        private static bool CanFerment(GameItem bottle) =>
            bottle.GetTagReadonly("WINE_CONTAINER_STATE").GetInt() == 0
            && WaterHelper.GetCurrentCapacityML(bottle) >= 749
            && GraphUtils.FindAllChildrenType<GameItem>(bottle).Any(item =>
                item.IsTag("WINE_INGREDIENT_TAG") || item.IsTag("WINE_YEAST_ILLEGAL_TAG"));

        private static void Blend(GameItem machine)
        {
            var input = (GameInventory)MachineHelper.GetBodyStorage(machine, 1);
            var container = (GameSlotInventory)MachineHelper.GetBodyStorage(machine, 2);
            AudioManager.Instance.Play2("blender");
            foreach (var item in input.childItems.ToArray())
            {
                if (container.childItem == null || WaterHelper.IsFull(container.childItem)) break;
                if (item.IsTag("BLENDABLE_TAG"))
                    ChemicalHelper.BlendInto(item, container.childItem, MachineBlender.GetLoss(machine));
            }
        }

        private static void FinishChemical(GameItem machine, string option)
        {
            machine.ModifyTag("SELECTED_CHEM_ID_TAG", state => state.Enable().SetString(option));
            var container = (GameSlotInventory)MachineHelper.GetBodyStorage(machine, 1);
            var output = MachineHelper.GetBodyStorage(machine, 2);
            var recipe = ChemFinisherRecipe.GetRecipeById(option);
            var product = DirectoryMaster.Item(option);
            ChemicalHelper.RemoveAllLiquid(product);
            ChemicalHelper.TransferLiquidToChem(container.childItem, product, recipe.requiredVolume);
            MachineChemFinisher.IntroduceImpurity(product, MachineChemFinisher.GetLoss(machine));
            GraphUtils.TryAcceptAll(output, product);
            ChemicalProductHelper.GradeProduct(product);
            AchievementTracker.Instance.CheckLabPure(product);
        }

        private static void PrintBottle(GameItem machine, string option)
        {
            var battery = MachineHelper.GetBatterySlot(machine).childItem;
            var output = MachineHelper.GetBodyStorage(machine, 1);
            var bottle = DirectoryMaster.Item(option);
            GraphUtils.TryAcceptAll(output, bottle);
            PowerHelper.DrawPowerSource(battery, BottleCost(option));
        }
    }
}
