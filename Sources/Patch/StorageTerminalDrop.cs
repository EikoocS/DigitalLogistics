using System.Collections.Generic;
using DigitalLogistics.Controller;
using DigitalLogistics.UI;
using HarmonyLib;
using UnityEngine;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(ItemMouseDragHandler), "EndDrag")]
    internal static class StorageTerminalDrop
    {
        private static void Prefix(ItemMouseDragHandler __instance, ref bool cancelled,
            GameItemElement ___currentItem, int ___currentRemove,
            ref GameItem ___lastItem, ref GameInventory ___lastInventory)
        {
            if (cancelled || !DigitalLogistics.Instance.Enabled || !__instance.IsDraggingItem) return;
            var controller = FindTarget(___currentItem.fakeBackground);
            if (controller == null) return;

            cancelled = true;
            ___lastItem = null;
            ___lastInventory = null;
            controller.Input(___currentItem, ___currentRemove);
        }

        internal static StorageTerminalController FindTarget(TreeNodeRender ignored)
        {
            var position = Input.mousePosition;
            var controller = StorageTerminalUI.FindController(position, ignored);
            return controller != null && controller.isActiveAndEnabled && !controller.Refreshing
                && controller.ContainsPointer(position, ignored) ? controller : null;
        }
    }

    [HarmonyPatch(typeof(ItemMultiSelectHandler), "EndGroupDrag")]
    internal static class StorageTerminalGroupDrop
    {
        private static bool Prefix(ItemMultiSelectHandler __instance, List<GameItemElement> ___selectedItems)
        {
            if (!DigitalLogistics.Instance.Enabled || !__instance.IsGroupDragging) return true;
            var controller = StorageTerminalDrop.FindTarget(null);
            if (controller == null) return true;

            var items = ___selectedItems.ToArray();
            __instance.Cancel();
            foreach (var item in items) controller.Input(item, -1);
            if (items.Length != 0)
                AudioManager.Instance.TryPlayOne(items[0].soundDragEnd, items[0].soundDragStart);
            return false;
        }
    }
}
