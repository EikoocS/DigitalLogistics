using System.Collections.Generic;
using DigitalLogistics.Controller;
using DigitalLogistics.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DigitalLogistics.Patch
{
    [HarmonyPatch(typeof(ItemMultiSelectHandler), "IsPointerOverDragSurface")]
    internal static class StorageTerminalScrollbar
    {
        private static void Postfix(Vector2 screenPosition, ref bool __result)
        {
            if (__result) return;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            }, hits);
            if (hits.Count == 0) return;
            var target = hits[0].gameObject;
            __result = target.GetComponentInParent<StorageTerminalRefreshButton>() != null
                || target.GetComponentInParent<Scrollbar>() != null
                    && target.GetComponentInParent<StorageTerminalController>() != null;
        }
    }
}
