using DigitalStorage.UI;
using HarmonyLib;
using UnityEngine;

namespace DigitalStorage.Patch
{
    [HarmonyPatch(typeof(ItemMouseDragHandler), "EndDrag")]
    internal static class StorageTerminalDrop
    {
        private static void Prefix(ItemMouseDragHandler __instance, ref bool cancelled,
            GameItemElement ___currentItem, int ___currentRemove,
            ref GameItem ___lastItem, ref GameInventory ___lastInventory)
        {
            if (cancelled || !DigitalStorage.Instance.Enabled || !__instance.IsDraggingItem) return;
            var controller = StorageTerminalUI.FindController(Input.mousePosition, ___currentItem.fakeBackground);
            if (controller == null || !controller.isActiveAndEnabled || controller.Refreshing
                || !controller.ContainsPointer(Input.mousePosition, ___currentItem.fakeBackground)) return;

            cancelled = true;
            ___lastItem = null;
            ___lastInventory = null;
            controller.Input(___currentItem, ___currentRemove);
        }
    }
}
