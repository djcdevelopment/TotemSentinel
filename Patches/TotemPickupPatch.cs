using HarmonyLib;
using UnityEngine;

namespace ComfySentinel.Patches
{
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact), new[] { typeof(Humanoid), typeof(bool), typeof(bool) })]
    internal static class TotemPickupPatch
    {
        private static readonly int GoblinTotemPrefabHash = "GoblinTotem".GetStableHashCode();

        private readonly struct PickupState
        {
            internal readonly bool Eligible;
            internal readonly Vector3 Origin;

            internal PickupState(bool eligible, Vector3 origin)
            {
                Eligible = eligible;
                Origin = origin;
            }
        }

        private static void Prefix(Pickable __instance, Humanoid character, bool repeat, ref PickupState __state)
        {
            __state = default;

            if (repeat
                || character != Player.m_localPlayer
                || __instance == null
                || __instance.m_itemPrefab == null
                || __instance.m_itemPrefab.name.GetStableHashCode() != GoblinTotemPrefabHash
                || __instance.GetEnabled == 0
                || !__instance.CanBePicked())
            {
                return;
            }

            ZNetView netView = __instance.GetComponent<ZNetView>();
            if (netView == null || !netView.IsValid())
            {
                return;
            }

            if (__instance.m_tarPreventsPicking)
            {
                Floating floating = __instance.GetComponent<Floating>();
                if (floating != null && floating.IsInTar())
                {
                    return;
                }
            }

            __state = new PickupState(true, __instance.transform.position);
        }

        private static void Postfix(PickupState __state)
        {
            if (__state.Eligible)
            {
                ComfySentinelPlugin.StartSonarSession(__state.Origin);
            }
        }
    }
}
