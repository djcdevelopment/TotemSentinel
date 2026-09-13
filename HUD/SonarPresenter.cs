using System;
using TotemSentinel.Services;
using UnityEngine;

namespace TotemSentinel.HUD
{
    internal static class SonarPresenter
    {
        internal static void BeginScan(Vector3 origin, SonarScanner.ScanResult result, int charges, bool isGreed = false)
        {
            PlayPingEffect(origin);
            SonarAbilityPanel.BeginScan(result, charges, isGreed);
        }

        internal static void TriggerCursed(float duration)
        {
            SonarAbilityPanel.TriggerCursed(duration);
        }

        internal static void CompleteScan(SonarScanner.ScanResult result, int charges)
        {
            SonarAbilityPanel.CompleteScan(result, charges);
        }

        internal static void ShowOutOfRangeWarning()
        {
            SonarAbilityPanel.ShowOutOfRange();
        }

        internal static void ShowNoChargesWarning()
        {
            SonarAbilityPanel.ShowDepleted();
        }

        internal static void ShowScanUnavailableWarning()
        {
            SonarAbilityPanel.ShowUnavailable();
        }

        private static void PlayPingEffect(Vector3 origin)
        {
            try
            {
                ZNetScene scene = ZNetScene.instance;
                if (scene == null)
                {
                    ZLog.LogWarning($"[{TotemSentinelPlugin.PluginName}] ZNetScene was unavailable for the sonar effect.");
                    return;
                }

                GameObject effectPrefab = scene.GetPrefab("vfx_WishbonePing");
                if (effectPrefab == null)
                {
                    ZLog.LogWarning($"[{TotemSentinelPlugin.PluginName}] Could not find vfx_WishbonePing.");
                    return;
                }

                if (effectPrefab.GetComponentInChildren<ZNetView>(includeInactive: true) != null)
                {
                    ZLog.LogWarning(
                        $"[{TotemSentinelPlugin.PluginName}] Skipped networked vfx_WishbonePing to preserve local-only scanning.");
                    return;
                }

                GameObject.Instantiate(effectPrefab, origin, Quaternion.identity);
            }
            catch (Exception exception)
            {
                ZLog.LogError($"[{TotemSentinelPlugin.PluginName}] Failed to play the sonar effect: {exception}");
            }
        }

    }
}

