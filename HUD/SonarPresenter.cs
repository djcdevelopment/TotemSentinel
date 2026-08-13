using System;
using ComfySentinel.Services;
using UnityEngine;

namespace ComfySentinel.HUD
{
    internal static class SonarPresenter
    {
        internal static void PresentScan(Vector3 origin, SonarScanner.ScanResult result, int charges)
        {
            PlayPingEffect(origin);
            SonarAbilityPanel.BeginScan(result, charges);
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
                    ZLog.LogWarning($"[{ComfySentinelPlugin.PluginName}] ZNetScene was unavailable for the sonar effect.");
                    return;
                }

                GameObject effectPrefab = scene.GetPrefab("vfx_WishbonePing");
                if (effectPrefab == null)
                {
                    ZLog.LogWarning($"[{ComfySentinelPlugin.PluginName}] Could not find vfx_WishbonePing.");
                    return;
                }

                GameObject.Instantiate(effectPrefab, origin, Quaternion.identity);
            }
            catch (Exception exception)
            {
                ZLog.LogError($"[{ComfySentinelPlugin.PluginName}] Failed to play the sonar effect: {exception}");
            }
        }

    }
}
