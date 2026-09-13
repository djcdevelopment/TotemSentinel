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
        public const string PluginVersion = "1.5.0";

        private const float ScanPulseInterval = 1.0f;

        public static ConfigEntry<int> MaxSonarCharges;
        public static ConfigEntry<int> MaxStoredSonarCharges;
        public static ConfigEntry<float> ScanRadius;
        public static ConfigEntry<KeyCode> PingHotkey;
        public static ConfigEntry<float> StateDuration;
        public static ConfigEntry<float> FinalSummaryDuration;
        public static ConfigEntry<float> GreedMultiplier;
        public static ConfigEntry<float> GreedDuration;
        public static ConfigEntry<KeyCode> GreedModifierKey;

        public static int ActiveSonarCharges = 0;
        public static Vector3 LastScanOrigin = Vector3.zero;

        private static bool _hasActiveSonarSession;
        private static bool _showAbilityPreview;
        private static bool _scanInProgress;
        private static float _scanStartedAt;
        private static float _nextScanPulseAt;
        private static long _scanCpuTicks;
        private static int _scanPulseCount;
        private static long _sessionWorldUid;
        private static bool _deathSuspended;
        private static bool _isGreedScan;
        private static float _greedUntil;

        internal static bool HasActiveSonarSession => _hasActiveSonarSession;
        internal static bool IsGreedActive => Time.unscaledTime < _greedUntil;
        internal static int MaximumStoredSonarCharges =>
            MaxStoredSonarCharges != null ? Math.Max(1, MaxStoredSonarCharges.Value) : 9;

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

            MaxStoredSonarCharges = Config.Bind(
                "Sonar",
                "MaxStoredSonarCharges",
                9,
                new ConfigDescription(
                    "Maximum number of camp checks that can be banked across multiple newly discovered Fuling Totems.",
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

            GreedMultiplier = Config.Bind(
                "Greed",
                "GreedMultiplier",
                3.0f,
                new ConfigDescription(
                    "Multiplier applied to scan radius during Greed's Gambit wide loot searches.",
                    new AcceptableValueRange<float>(1.5f, 5.0f)));

            GreedDuration = Config.Bind(
                "Greed",
                "GreedDuration",
                35.0f,
                new ConfigDescription(
                    "Seconds that the Greed's Gambit cursed retribution window lasts after a wide search.",
                    new AcceptableValueRange<float>(10.0f, 120.0f)));

            GreedModifierKey = Config.Bind(
                "Controls",
                "GreedModifierKey",
                KeyCode.LeftShift,
                "Modifier key held while pressing PingHotkey to trigger Greed's Gambit wide loot search.");

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
                    $"[{PluginName}] Initialized with {MaxSonarCharges.Value} charges per totem, " +
                    $"a {MaximumStoredSonarCharges}-charge storage cap, " +
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
            ZNet znet = ZNet.instance;
            if (_hasActiveSonarSession
                && znet != null
                && _sessionWorldUid != 0L
                && znet.GetWorldUID() != _sessionWorldUid)
            {
                ZLog.Log($"[{PluginName}] Cleared camp checks after the active world changed.");
                ResetSonarSession();
            }

            Player localPlayer = Player.m_localPlayer;
            if (!localPlayer)
            {
                if (znet == null)
                {
                    ResetSonarSession();
                }
                else
                {
                    SuspendSonarForDeath();
                }

                return;
            }

            if (localPlayer.IsDead())
            {
                SuspendSonarForDeath();
                return;
            }

            ResumeSonarAfterDeath();

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

            bool isGreed = Input.GetKey(GreedModifierKey.Value);
            float radius = isGreed ? ScanRadius.Value * GreedMultiplier.Value : ScanRadius.Value;
            float distanceSquared = (localPlayer.transform.position - LastScanOrigin).sqrMagnitude;
            if (distanceSquared > radius * radius)
            {
                ZLog.Log(
                    $"[{PluginName}] Ping rejected: out of range " +
                    $"distance={Math.Sqrt(distanceSquared):0.0}m radius={radius:0.0}m charges={ActiveSonarCharges}.");
                SonarPresenter.ShowOutOfRangeWarning();
                return;
            }

            StartActiveScan(isGreed);
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

        internal static void GrantTotemSonar(Vector3 origin, int totemCount)
        {
            if (totemCount <= 0)
            {
                return;
            }

            RestoreInterruptedScanCharge("a new totem was discovered");

            int previousCharges = _hasActiveSonarSession ? ActiveSonarCharges : 0;
            long requestedGrant = (long)Math.Max(1, MaxSonarCharges.Value) * totemCount;
            ActiveSonarCharges = (int)Math.Min(
                MaximumStoredSonarCharges,
                Math.Min(int.MaxValue, previousCharges + requestedGrant));

            int addedCharges = ActiveSonarCharges - previousCharges;
            LastScanOrigin = origin;
            _sessionWorldUid = ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L;
            _hasActiveSonarSession = true;
            _deathSuspended = false;
            SonarAbilityPanel.Arm();
            ZLog.Log(
                $"[{PluginName}] Camp checks granted " +
                $"origin=({origin.x:0.0},{origin.y:0.0},{origin.z:0.0}) " +
                $"totems={totemCount} added={addedCharges} " +
                $"charges={ActiveSonarCharges}/{MaximumStoredSonarCharges} " +
                $"radius={ScanRadius.Value:0.0}m hotkey={PingHotkey.Value}.");
        }

        private static void StartActiveScan(bool isGreed = false)
        {
            if (ActiveSonarCharges <= 0)
            {
                SonarPresenter.ShowNoChargesWarning();
                return;
            }

            _isGreedScan = isGreed;

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

            if (isGreed)
            {
                ArmGreedState();
            }

            SonarPresenter.BeginScan(LastScanOrigin, result, ActiveSonarCharges, isGreed);
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
            float radius = _isGreedScan ? ScanRadius.Value * GreedMultiplier.Value : ScanRadius.Value;

            ZLog.Log(
                $"[{PluginName}] Camp scan complete " +
                $"origin=({LastScanOrigin.x:0.0},{LastScanOrigin.y:0.0},{LastScanOrigin.z:0.0}) " +
                $"distance={distance:0.0}m radius={radius:0.0}m isGreed={_isGreedScan} " +
                $"goblins={finalResult.Goblins} shamans={finalResult.Shamans} brutes={finalResult.Brutes} " +
                $"fulings={finalResult.Fulings} coins={finalResult.Coins} blackMetal={finalResult.BlackMetal} valuables={finalResult.Valuables} " +
                $"charges={ActiveSonarCharges} pulses={_scanPulseCount} " +
                $"scanCpu={elapsedMilliseconds:0.000}ms source=local-zdo.");

            SonarPresenter.CompleteScan(finalResult, ActiveSonarCharges);
            ResetActiveScanMetrics();
        }

        private static bool TryReadLocalPulse(out SonarScanner.ScanResult result, out long elapsedTicks)
        {
            float radius = _isGreedScan ? ScanRadius.Value * GreedMultiplier.Value : ScanRadius.Value;
            long started = Stopwatch.GetTimestamp();
            bool success = SonarScanner.TryScan(LastScanOrigin, radius, out result);
            elapsedTicks = Stopwatch.GetTimestamp() - started;
            return success;
        }

        private static void AbortActiveScan()
        {
            ZLog.LogError($"[{PluginName}] A live scan pulse could not access the current local ZDO sector table.");
            ResetActiveScan();
            SonarAbilityPanel.CancelScan();
            SonarPresenter.ShowScanUnavailableWarning();
        }

        private static void SuspendSonarForDeath()
        {
            if (!_hasActiveSonarSession || _deathSuspended)
            {
                return;
            }

            bool restoredCharge = _scanInProgress;
            RestoreInterruptedScanCharge("the player died");
            _deathSuspended = true;
            SonarAbilityPanel.Suspend();
            ZLog.Log(
                $"[{PluginName}] Camp checks preserved through death " +
                $"charges={ActiveSonarCharges}/{MaximumStoredSonarCharges} " +
                $"interruptedChargeRestored={restoredCharge}.");
        }

        private static void ResumeSonarAfterDeath()
        {
            if (!_deathSuspended)
            {
                return;
            }

            _deathSuspended = false;
            SonarAbilityPanel.Resume();
            ZLog.Log(
                $"[{PluginName}] Camp checks restored after respawn " +
                $"charges={ActiveSonarCharges}/{MaximumStoredSonarCharges}.");
        }

        private static void RestoreInterruptedScanCharge(string reason)
        {
            if (!_scanInProgress)
            {
                return;
            }

            ActiveSonarCharges = Math.Min(MaximumStoredSonarCharges, ActiveSonarCharges + 1);
            ResetActiveScan();
            SonarAbilityPanel.CancelScan();
            ZLog.Log(
                $"[{PluginName}] Interrupted camp check cancelled because {reason}; " +
                $"one charge was restored.");
        }

        private static void ResetActiveScan()
        {
            _scanInProgress = false;
            _isGreedScan = false;
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
            _sessionWorldUid = 0L;
            _deathSuspended = false;
            DisarmGreedState();
            ResetActiveScan();
            SonarAbilityPanel.Reset();
        }

        internal static void ArmGreedState()
        {
            _greedUntil = Time.unscaledTime + GreedDuration.Value;
            ZLog.Log($"[{PluginName}] Greed's Gambit activated for {GreedDuration.Value:0}s. Any damage will enrage the horde!");
        }

        internal static void DisarmGreedState()
        {
            _greedUntil = 0f;
        }

        internal static void TriggerGreedRetribution(Player localPlayer)
        {
            if (!IsGreedActive)
            {
                return;
            }

            _greedUntil = 0f;
            ZLog.LogWarning($"[{PluginName}] A single scratch triggered Greed's Retribution! Alerting horde across 3x sector.");

            if (localPlayer != null)
            {
                localPlayer.Message(MessageHud.MessageType.Center, "<color=#EF4444>CURSED! The Totem's greed awakens the horde!</color>");
            }

            float huntRadius = ScanRadius.Value * GreedMultiplier.Value;
            float huntRadiusSq = huntRadius * huntRadius;
            Vector3 origin = localPlayer != null ? localPlayer.transform.position : LastScanOrigin;

            try
            {
                var allAi = BaseAI.GetAllInstances();
                int alerted = 0;
                if (allAi != null)
                {
                    foreach (var ai in allAi)
                    {
                        if (ai == null) continue;
                        if ((ai.transform.position - origin).sqrMagnitude <= huntRadiusSq)
                        {
                            ai.Alert();
                            ai.SetHuntPlayer(true);
                            alerted++;
                        }
                    }
                }
                ZLog.Log($"[{PluginName}] Greed's Retribution: {alerted} creatures alerted.");
            }
            catch (Exception ex)
            {
                ZLog.LogError($"[{PluginName}] Failed alerting creatures during retribution: {ex}");
            }

            SonarPresenter.TriggerCursed(GreedDuration.Value);
        }
    }
}
