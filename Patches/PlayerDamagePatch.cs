using HarmonyLib;

namespace ComfySentinel.Patches
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

            if (hit.GetTotalDamage() > 0.05f && ComfySentinelPlugin.IsGreedActive)
            {
                ComfySentinelPlugin.TriggerGreedRetribution(Player.m_localPlayer);
            }
        }
    }
}
