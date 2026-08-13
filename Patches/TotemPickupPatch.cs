using HarmonyLib;
using UnityEngine;

namespace ComfySentinel.Patches
{
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup), new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
    internal static class TotemPickupPatch
    {
        private static readonly int GoblinTotemPrefabHash = "GoblinTotem".GetStableHashCode();

        private readonly struct PickupState
        {
            internal readonly bool Eligible;
            internal readonly Vector3 Origin;
            internal readonly int TotemCount;

            internal PickupState(bool eligible, Vector3 origin, int totemCount)
            {
                Eligible = eligible;
                Origin = origin;
                TotemCount = totemCount;
            }
        }

        private static void Prefix(Humanoid __instance, GameObject go, ref PickupState __state)
        {
            __state = default;

            if (__instance != Player.m_localPlayer || go == null)
            {
                return;
            }

            ItemDrop itemDrop = go.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                return;
            }

            itemDrop.Load();
            ItemDrop.ItemData itemData = itemDrop.m_itemData;
            GameObject itemPrefab = itemData?.m_dropPrefab;
            if (itemPrefab == null
                || itemPrefab.name.GetStableHashCode() != GoblinTotemPrefabHash
                || itemData.m_pickedUp)
            {
                return;
            }

            __state = new PickupState(true, itemDrop.transform.position, Mathf.Max(1, itemData.m_stack));
        }

        private static void Postfix(PickupState __state, ref bool __result)
        {
            if (__state.Eligible && __result)
            {
                ComfySentinelPlugin.GrantTotemSonar(__state.Origin, __state.TotemCount);
            }
        }
    }
}
