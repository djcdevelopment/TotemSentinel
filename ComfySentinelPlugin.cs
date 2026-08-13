using System;
using System.Diagnostics;
using BepInEx;
using BepInEx.Configuration;
using ComfySentinel.Commands;
using ComfySentinel.HUD;
using ComfySentinel.Services;
using HarmonyLib;
using UnityEngine;

namespace ComfySentinel
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ComfySentinelPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "comfy.mods.comfysentinel";
        public const string PluginName = "ComfySentinel";
        public const string PluginVersion = "1.3.0";

        public static ConfigEntry<int> MaxSonarCharges;
        public static ConfigEntry<float> ScanRadius;
        public static ConfigEntry<KeyCode> PingHotkey;
        public static ConfigEntry<float> StateDuration;
        public static ConfigEntry<float> FinalSummaryDuration;

        public static int ActiveSonarCharges = 0;
        public static Vector3 LastScanOrigin = Vector3.zero;

        private static bool _hasActiveSonarSession;
        private static bool _showAbilityPreview;

        internal static bool HasActiveSonarSession => _hasActiveSonarSession;

        private Harmony _harmony;
        private bool _patchesApplied;

        private void Awake()
        {
            MaxSonarCharges = Config.Bind(
                "Sonar",
                "MaxSonarCharges",
                3,
                new ConfigDescription(
                    "Number of camp checks granted by each Fuling Totem. Checks are spent only when the hotkey is pressed.",
                    new AcceptableValueRange<int>(1, int.MaxValue)));

            ScanRadius = Config.Bind(
                "Sonar",
                "ScanRadius",
                64.0f,
                new ConfigDescription(
                    "Radius in metres around the picked-up Fuling Totem that is scanned for Fulings.",
                    new AcceptableValueRange<float>(1.0f, 128.0f)));

            PingHotkey = Config.Bind(
                "Controls",
                "PingHotkey",
                KeyCode.V,
                "Key used to spend another sonar charge while within range of the totem origin.");

            StateDuration = Config.Bind(
                "Interface",
                "StateDuration",
                10.0f,
                new ConfigDescription(
                    "Seconds that scanning, result, and warning states remain visible.",
                    new AcceptableValueRange<float>(1.0f, 60.0f)));

            FinalSummaryDuration = Config.Bind(
                "Interface",
                "FinalSummaryDuration",
                300.0f,
                new ConfigDescription(
                    "Seconds after the final scan before the completed camp-check card closes automatically.",
                    new AcceptableValueRange<float>(30.0f, 1800.0f)));

            if (!SonarScanner.Initialize())
            {
                ZLog.LogError($"[{PluginName}] Failed to initialize ZDO sector access; gameplay patches were not installed.");
                return;
            }

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(ComfySentinelPlugin).Assembly);
                TotemAlertCommands.Register();
                _patchesApplied = true;
                ZLog.Log(
                    $"[{PluginName}] Initialized with {MaxSonarCharges.Value} charges, " +
                    $"a {ScanRadius.Value:0.#}m radius, {StateDuration.Value:0.#}s states, " +
                    $"and a {FinalSummaryDuration.Value:0.#}s final summary.");
            }
            catch (Exception exception)
            {
                ZLog.LogError($"[{PluginName}] Failed to install Harmony patches: {exception}");
            }
        }

        private void Update()
        {
            Player localPlayer = Player.m_localPlayer;
            if (!localPlayer)
            {
                ResetSonarSession();
                return;
            }

            if (!_hasActiveSonarSession
                || Console.IsVisible()
                || TextInput.IsVisible()
                || !Input.GetKeyDown(PingHotkey.Value))
            {
                return;
            }

            if (ActiveSonarCharges <= 0)
            {
                ZLog.Log($"[{PluginName}] Ping rejected: no camp checks remain.");
                SonarPresenter.ShowNoChargesWarning();
                return;
            }

            if (!SonarAbilityPanel.CanBeginScan)
            {
                ZLog.Log($"[{PluginName}] Ping ignored: the camp-check display is still presenting the previous state.");
                return;
            }

            float radius = ScanRadius.Value;
            float distanceSquared = (localPlayer.transform.position - LastScanOrigin).sqrMagnitude;
            if (distanceSquared > radius * radius)
            {
                ZLog.Log(
                    $"[{PluginName}] Ping rejected: out of range " +
                    $"distance={Math.Sqrt(distanceSquared):0.0}m radius={radius:0.0}m charges={ActiveSonarCharges}.");
                SonarPresenter.ShowOutOfRangeWarning();
                return;
            }

            ExecutePing();
        }

        private void OnDestroy()
        {
            TotemAlertCommands.ClearTestScenario();
            ResetSonarSession();

            if (_patchesApplied)
            {
                _harmony.UnpatchSelf();
                _patchesApplied = false;
            }
        }

        private void OnGUI()
        {
            SonarAbilityPanel.Draw(_showAbilityPreview);
        }

        internal static bool SetAbilityPreview(bool? visible = null)
        {
            _showAbilityPreview = visible ?? !_showAbilityPreview;
            return _showAbilityPreview;
        }

        internal static void StartSonarSession(Vector3 origin)
        {
            LastScanOrigin = origin;
            ActiveSonarCharges = Math.Max(1, MaxSonarCharges.Value);
            _hasActiveSonarSession = true;
            SonarAbilityPanel.Arm();
            ZLog.Log(
                $"[{PluginName}] Camp checks armed " +
                $"origin=({origin.x:0.0},{origin.y:0.0},{origin.z:0.0}) " +
                $"charges={ActiveSonarCharges} radius={ScanRadius.Value:0.0}m hotkey={PingHotkey.Value}.");
        }

        private static void ExecutePing()
        {
            if (ActiveSonarCharges <= 0)
            {
                SonarPresenter.ShowNoChargesWarning();
                return;
            }

            long started = Stopwatch.GetTimestamp();
            if (!SonarScanner.TryScan(LastScanOrigin, ScanRadius.Value, out SonarScanner.ScanResult result))
            {
                ZLog.LogError($"[{PluginName}] A sonar scan could not access the current ZDO sector table.");
                SonarPresenter.ShowScanUnavailableWarning();
                return;
            }

            double elapsedMilliseconds =
                (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            ActiveSonarCharges--;
            Player localPlayer = Player.m_localPlayer;
            double distance = localPlayer != null
                ? Math.Sqrt((localPlayer.transform.position - LastScanOrigin).sqrMagnitude)
                : 0.0;

            ZLog.Log(
                $"[{PluginName}] Camp scan complete " +
                $"origin=({LastScanOrigin.x:0.0},{LastScanOrigin.y:0.0},{LastScanOrigin.z:0.0}) " +
                $"distance={distance:0.0}m radius={ScanRadius.Value:0.0}m " +
                $"goblins={result.Goblins} shamans={result.Shamans} brutes={result.Brutes} " +
                $"fulings={result.Fulings} coins={result.Coins} blackMetal={result.BlackMetal} " +
                $"charges={ActiveSonarCharges} duration={elapsedMilliseconds:0.000}ms.");

            SonarPresenter.PresentScan(LastScanOrigin, result, ActiveSonarCharges);
        }

        private static void ResetSonarSession()
        {
            ActiveSonarCharges = 0;
            LastScanOrigin = Vector3.zero;
            _hasActiveSonarSession = false;
            SonarAbilityPanel.Reset();
        }
    }
}
