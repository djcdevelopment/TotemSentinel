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
        public const string PluginVersion = "1.4.0";

        private const float ScanPulseInterval = 1.0f;

        public static ConfigEntry<int> MaxSonarCharges;
        public static ConfigEntry<float> ScanRadius;
        public static ConfigEntry<KeyCode> PingHotkey;
        public static ConfigEntry<float> StateDuration;
        public static ConfigEntry<float> FinalSummaryDuration;

        public static int ActiveSonarCharges = 0;
        public static Vector3 LastScanOrigin = Vector3.zero;

        private static bool _hasActiveSonarSession;
        private static bool _showAbilityPreview;
        private static bool _scanInProgress;
        private static float _scanStartedAt;
        private static float _nextScanPulseAt;
        private static long _scanCpuTicks;
        private static int _scanPulseCount;

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

            if (_scanInProgress)
            {
                UpdateActiveScan(localPlayer);
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

            StartActiveScan();
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
            ResetActiveScan();
            LastScanOrigin = origin;
            ActiveSonarCharges = Math.Max(1, MaxSonarCharges.Value);
            _hasActiveSonarSession = true;
            SonarAbilityPanel.Arm();
            ZLog.Log(
                $"[{PluginName}] Camp checks armed " +
                $"origin=({origin.x:0.0},{origin.y:0.0},{origin.z:0.0}) " +
                $"charges={ActiveSonarCharges} radius={ScanRadius.Value:0.0}m hotkey={PingHotkey.Value}.");
        }

        private static void StartActiveScan()
        {
            if (ActiveSonarCharges <= 0)
            {
                SonarPresenter.ShowNoChargesWarning();
                return;
            }

            if (!TryReadLocalPulse(out SonarScanner.ScanResult result, out long elapsedTicks))
            {
                ZLog.LogError($"[{PluginName}] A sonar scan could not access the current ZDO sector table.");
                SonarPresenter.ShowScanUnavailableWarning();
                return;
            }

            ActiveSonarCharges--;
            _scanInProgress = true;
            _scanStartedAt = Time.unscaledTime;
            _nextScanPulseAt = _scanStartedAt + ScanPulseInterval;
            _scanCpuTicks = elapsedTicks;
            _scanPulseCount = 1;
            SonarPresenter.BeginScan(LastScanOrigin, result, ActiveSonarCharges);
        }

        private static void UpdateActiveScan(Player localPlayer)
        {
            float now = Time.unscaledTime;
            if (now - _scanStartedAt < StateDuration.Value)
            {
                if (now < _nextScanPulseAt)
                {
                    return;
                }

                if (!TryReadLocalPulse(out SonarScanner.ScanResult liveResult, out long liveElapsedTicks))
                {
                    AbortActiveScan();
                    return;
                }

                _scanCpuTicks += liveElapsedTicks;
                _scanPulseCount++;
                _nextScanPulseAt = now + ScanPulseInterval;
                SonarAbilityPanel.UpdateScan(liveResult);
                return;
            }

            if (!TryReadLocalPulse(out SonarScanner.ScanResult finalResult, out long finalElapsedTicks))
            {
                AbortActiveScan();
                return;
            }

            _scanCpuTicks += finalElapsedTicks;
            _scanPulseCount++;
            _scanInProgress = false;

            double elapsedMilliseconds = _scanCpuTicks * 1000.0 / Stopwatch.Frequency;
            double distance = Math.Sqrt((localPlayer.transform.position - LastScanOrigin).sqrMagnitude);

            ZLog.Log(
                $"[{PluginName}] Camp scan complete " +
                $"origin=({LastScanOrigin.x:0.0},{LastScanOrigin.y:0.0},{LastScanOrigin.z:0.0}) " +
                $"distance={distance:0.0}m radius={ScanRadius.Value:0.0}m " +
                $"goblins={finalResult.Goblins} shamans={finalResult.Shamans} brutes={finalResult.Brutes} " +
                $"fulings={finalResult.Fulings} coins={finalResult.Coins} blackMetal={finalResult.BlackMetal} " +
                $"charges={ActiveSonarCharges} pulses={_scanPulseCount} " +
                $"scanCpu={elapsedMilliseconds:0.000}ms source=local-zdo.");

            SonarPresenter.CompleteScan(finalResult, ActiveSonarCharges);
            ResetActiveScanMetrics();
        }

        private static bool TryReadLocalPulse(out SonarScanner.ScanResult result, out long elapsedTicks)
        {
            long started = Stopwatch.GetTimestamp();
            bool success = SonarScanner.TryScan(LastScanOrigin, ScanRadius.Value, out result);
            elapsedTicks = Stopwatch.GetTimestamp() - started;
            return success;
        }

        private static void AbortActiveScan()
        {
            ZLog.LogError($"[{PluginName}] A live scan pulse could not access the current local ZDO sector table.");
            ResetActiveScan();
            SonarPresenter.ShowScanUnavailableWarning();
        }

        private static void ResetActiveScan()
        {
            _scanInProgress = false;
            ResetActiveScanMetrics();
        }

        private static void ResetActiveScanMetrics()
        {
            _scanStartedAt = 0.0f;
            _nextScanPulseAt = 0.0f;
            _scanCpuTicks = 0L;
            _scanPulseCount = 0;
        }

        private static void ResetSonarSession()
        {
            ActiveSonarCharges = 0;
            LastScanOrigin = Vector3.zero;
            _hasActiveSonarSession = false;
            ResetActiveScan();
            SonarAbilityPanel.Reset();
        }
    }
}
