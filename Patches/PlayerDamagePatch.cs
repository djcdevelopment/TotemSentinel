using HarmonyLib;

namespace TotemSentinel.Patches
{
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class PlayerDamagePatch
    {
        private static void Postfix(Character __instance, HitData hit)
        {
            if (__instance != Player.m_localPlayer || hit == null)
            {
                return;
            }

            if (hit.GetTotalDamage() > 0.05f && TotemSentinelPlugin.IsGreedActive)
            {
                TotemSentinelPlugin.TriggerGreedRetribution(Player.m_localPlayer);
            }
        }
    }
}

