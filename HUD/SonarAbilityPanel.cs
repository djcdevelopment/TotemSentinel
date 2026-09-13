using TotemSentinel.Services;
using UnityEngine;

namespace TotemSentinel.HUD
{
    internal static class SonarAbilityPanel
    {
        private enum PanelState
        {
            Hidden,
            Ready,
            Scanning,
            Found,
            Clear,
            Complete,
            OutOfRange,
            Depleted,
            Unavailable,
            Cursed
        }

        private static readonly Vector3[] MapCorners = new Vector3[4];
        private static readonly Color ReadyColor = new Color(0.94f, 0.94f, 0.94f, 1.0f);
        private static readonly Color GoldColor = new Color(1.0f, 0.69f, 0.18f, 1.0f);
        private static readonly Color OrangeColor = new Color(1.0f, 0.45f, 0.08f, 1.0f);
        private static readonly Color RedColor = new Color(1.0f, 0.16f, 0.19f, 1.0f);
        private static readonly Color GreenColor = new Color(0.08f, 0.78f, 0.30f, 1.0f);
        private static readonly Color MutedColor = new Color(0.72f, 0.72f, 0.72f, 1.0f);
        private static readonly Color BackgroundColor = new Color(0.025f, 0.025f, 0.025f, 0.90f);

        private static PanelState _state;
        private static SonarScanner.ScanResult _result;
        private static bool _hasResult;
        private static SonarScanner.ScanResult _resultBeforeScan;
        private static bool _hadResultBeforeScan;
        private static bool _hasScanBackup;
        private static bool _dismissed;
        private static bool _suspended;
        private static float _suspendedAt;
        private static float _stateStarted;
        private static float _finalScanStarted = -1.0f;
        private static bool _isGreedScan;
        private static float _cursedUntil;
        private static float _cursedStarted;
        private static GUIStyle _titleStyle;
        private static GUIStyle _mainStyle;
        private static GUIStyle _detailStyle;
        private static GUIStyle _summaryStyle;
        private static GUIStyle _closeStyle;

        internal static bool CanBeginScan =>
            _state == PanelState.Ready || _state == PanelState.Hidden;

        internal static void Arm()
        {
            _result = default;
            _hasResult = false;
            _hasScanBackup = false;
            _dismissed = false;
            _suspended = false;
            _finalScanStarted = -1.0f;
            SetState(PanelState.Ready);
        }

        internal static void BeginScan(SonarScanner.ScanResult result, int charges, bool isGreed = false)
        {
            _isGreedScan = isGreed;
            _resultBeforeScan = _result;
            _hadResultBeforeScan = _hasResult;
            _hasScanBackup = true;
            _result = result;
            _hasResult = true;
            _dismissed = false;
            SetState(PanelState.Scanning);
        }

        internal static void TriggerCursed(float duration)
        {
            _cursedStarted = Time.unscaledTime;
            _cursedUntil = Time.unscaledTime + duration;
            _dismissed = false;
            SetState(PanelState.Cursed);
        }

        internal static void UpdateScan(SonarScanner.ScanResult result)
        {
            if (_state == PanelState.Scanning)
            {
                _result = result;
                _hasResult = true;
            }
        }

        internal static void CompleteScan(SonarScanner.ScanResult result, int charges)
        {
            _result = result;
            _hasResult = true;
            _hasScanBackup = false;
            _dismissed = false;
            if (charges <= 0)
            {
                _finalScanStarted = Time.unscaledTime;
            }

            SetState(result.Fulings > 0 ? PanelState.Found : PanelState.Clear);
        }

        internal static void CancelScan()
        {
            if (_hasScanBackup)
            {
                _result = _resultBeforeScan;
                _hasResult = _hadResultBeforeScan;
            }

            _hasScanBackup = false;
            SetState(TotemSentinelPlugin.ActiveSonarCharges > 0 ? PanelState.Ready : PanelState.Hidden);
        }

        internal static void Suspend()
        {
            if (_suspended)
            {
                return;
            }

            _suspended = true;
            _suspendedAt = Time.unscaledTime;
        }

        internal static void Resume()
        {
            if (!_suspended)
            {
                return;
            }

            float suspendedDuration = Time.unscaledTime - _suspendedAt;
            if (_finalScanStarted >= 0.0f)
            {
                _finalScanStarted += suspendedDuration;
            }

            _suspended = false;
            _suspendedAt = 0.0f;
            SetState(
                TotemSentinelPlugin.ActiveSonarCharges > 0
                    ? PanelState.Ready
                    : (_hasResult ? PanelState.Complete : PanelState.Hidden));
        }

        internal static void ShowOutOfRange()
        {
            _dismissed = false;
            SetState(PanelState.OutOfRange);
        }

        internal static void ShowDepleted()
        {
            _dismissed = false;
            SetState(PanelState.Depleted);
        }

        internal static void ShowUnavailable()
        {
            _dismissed = false;
            SetState(PanelState.Unavailable);
        }

        internal static void Reset()
        {
            _result = default;
            _hasResult = false;
            _resultBeforeScan = default;
            _hadResultBeforeScan = false;
            _hasScanBackup = false;
            _dismissed = false;
            _suspended = false;
            _suspendedAt = 0.0f;
            _state = PanelState.Hidden;
            _stateStarted = 0.0f;
            _finalScanStarted = -1.0f;
            _isGreedScan = false;
            _cursedUntil = 0f;
            _cursedStarted = 0f;
        }

        internal static void Draw(bool preview)
        {
            if (_suspended)
            {
                return;
            }

            UpdateState();

            bool hasUsableSession =
                TotemSentinelPlugin.HasActiveSonarSession && TotemSentinelPlugin.ActiveSonarCharges > 0;
            if (!preview && (_dismissed || (_state == PanelState.Hidden && !hasUsableSession)))
            {
                return;
            }

            if (_state == PanelState.Hidden && hasUsableSession && !_dismissed)
            {
                SetState(PanelState.Ready);
            }

            EnsureStyles();
            bool showSummary = ShouldShowSummary();
            Rect card = GetCardRect(showSummary);
            float scale = Mathf.Clamp(Screen.height / 1080.0f, 0.80f, 1.45f);
            float border = Mathf.Max(3.0f, 4.0f * scale);
            Color accent = GetAccentColor(preview);

            DrawSolidRect(card, accent);
            DrawSolidRect(
                new Rect(card.x + border, card.y + border, card.width - border * 2.0f, card.height - border * 2.0f),
                BackgroundColor);

            _titleStyle.fontSize = Mathf.RoundToInt(17.0f * scale);
            _mainStyle.fontSize = Mathf.RoundToInt(27.0f * scale);
            _detailStyle.fontSize = Mathf.RoundToInt(13.0f * scale);
            _summaryStyle.fontSize = Mathf.RoundToInt(13.0f * scale);
            _closeStyle.fontSize = Mathf.RoundToInt(16.0f * scale);

            DrawContent(card, scale, preview, accent, showSummary);
        }

        private static void DrawContent(Rect card, float scale, bool preview, Color accent, bool showSummary)
        {
            float inset = 10.0f * scale;
            float width = card.width - inset * 2.0f;
            Rect titleRect = new Rect(card.x + inset, card.y + 9.0f * scale, width, 25.0f * scale);
            Rect mainRect = new Rect(card.x + inset, card.y + 35.0f * scale, width, 42.0f * scale);
            Rect detailRect = new Rect(card.x + inset, card.y + 79.0f * scale, width, 38.0f * scale);

            _titleStyle.normal.textColor = accent;
            _mainStyle.normal.textColor = accent;
            _detailStyle.normal.textColor = ReadyColor;

            PanelState displayState = _state;
            if (displayState == PanelState.Hidden && preview)
            {
                GUI.Label(titleRect, "CAMP CHECKS", _titleStyle);
                GUI.Label(mainRect, "-- / --", _mainStyle);
                GUI.Label(detailRect, "WAITING FOR A TOTEM", _detailStyle);
                return;
            }

            switch (displayState)
            {
                case PanelState.Ready:
                    DrawReady(titleRect, mainRect, detailRect);
                    break;
                case PanelState.Scanning:
                    float remaining = Mathf.Max(0.0f, GetStateDuration() - (Time.unscaledTime - _stateStarted));
                    string scanTitle = _isGreedScan ? "GREED SCAN" : "SCANNING";
                    string scanDetail = _isGreedScan ? "3x WIDE LOOT SEARCH" : "LISTENING ACROSS THE CAMP";
                    GUI.Label(titleRect, scanTitle, _titleStyle);
                    GUI.Label(mainRect, $"{remaining:0}s", _mainStyle);
                    GUI.Label(detailRect, scanDetail, _detailStyle);
                    break;
                case PanelState.Cursed:
                    float cursedRemaining = Mathf.Max(0.0f, _cursedUntil - Time.unscaledTime);
                    GUI.Label(titleRect, "CURSED!", _titleStyle);
                    GUI.Label(mainRect, $"{cursedRemaining:0}s", _mainStyle);
                    GUI.Label(detailRect, "HORDE ALERTED ACROSS CAMP", _detailStyle);
                    break;
                case PanelState.Found:
                    GUI.Label(titleRect, "FULINGS FOUND", _titleStyle);
                    GUI.Label(mainRect, _result.Fulings.ToString(), _mainStyle);
                    GUI.Label(detailRect, "LAST CAMP CHECK", _detailStyle);
                    break;
                case PanelState.Clear:
                    GUI.Label(titleRect, "CAMP", _titleStyle);
                    GUI.Label(mainRect, "CLEAR", _mainStyle);
                    GUI.Label(detailRect, "LAST CAMP CHECK", _detailStyle);
                    break;
                case PanelState.Complete:
                    GUI.Label(titleRect, "CHECKS COMPLETE", _titleStyle);
                    GUI.Label(mainRect, $"0 / {TotemSentinelPlugin.MaximumStoredSonarCharges}", _mainStyle);
                    GUI.Label(detailRect, $"CLOSES IN {FormatFinalTimeRemaining()}", _detailStyle);
                    DrawCloseButton(card, scale);
                    break;
                case PanelState.OutOfRange:
                    GUI.Label(titleRect, "OUT OF RANGE", _titleStyle);
                    GUI.Label(mainRect, $"> {TotemSentinelPlugin.ScanRadius.Value:0}m", _mainStyle);
                    GUI.Label(detailRect, "RETURN TO THE TOTEM'S CAMP", _detailStyle);
                    break;
                case PanelState.Depleted:
                    GUI.Label(titleRect, "NO CHECKS", _titleStyle);
                    GUI.Label(mainRect, "0", _mainStyle);
                    GUI.Label(detailRect, "LAST RESULT REMAINS BELOW", _detailStyle);
                    break;
                case PanelState.Unavailable:
                    GUI.Label(titleRect, "SCAN FAILED", _titleStyle);
                    GUI.Label(mainRect, "--", _mainStyle);
                    GUI.Label(detailRect, "CHECK THE COMFYSENTINEL LOG", _detailStyle);
                    break;
            }

            if (showSummary)
            {
                DrawSummary(card, scale);
            }
        }

        private static void DrawReady(Rect titleRect, Rect mainRect, Rect detailRect)
        {
            int charges = TotemSentinelPlugin.ActiveSonarCharges;
            int maximum = TotemSentinelPlugin.MaximumStoredSonarCharges;
            GUI.Label(titleRect, "CAMP CHECKS", _titleStyle);

            _mainStyle.richText = true;
            string chargeText = charges < maximum
                ? $"<color=#FFB02E>{charges}</color> / {maximum}"
                : $"{charges} / {maximum}";
            GUI.Label(mainRect, chargeText, _mainStyle);
            _mainStyle.richText = false;

            GUI.Label(detailRect, $"\"{TotemSentinelPlugin.PingHotkey.Value}\" CHECK  ·  SHIFT GREED", _detailStyle);
        }

        private static void DrawSummary(Rect card, float scale)
        {
            float inset = 14.0f * scale;
            float top = card.y + 123.0f * scale;
            float rowHeight = 17.0f * scale;
            float width = card.width - inset * 2.0f;

            DrawSolidRect(new Rect(card.x + inset, top - 6.0f * scale, width, 1.0f), new Color(1.0f, 0.69f, 0.18f, 0.45f));

            int row = 0;
            DrawSummaryRow(card.x + inset, top, width, rowHeight, ref row, "FULINGS", _result.Goblins);
            DrawSummaryRow(card.x + inset, top, width, rowHeight, ref row, "SHAMANS", _result.Shamans);
            DrawSummaryRow(card.x + inset, top, width, rowHeight, ref row, "BRUTES", _result.Brutes);
            DrawSummaryRow(card.x + inset, top, width, rowHeight, ref row, "COINS", _result.Coins);
            DrawSummaryRow(card.x + inset, top, width, rowHeight, ref row, "BLACK METAL", _result.BlackMetal);
            DrawSummaryRow(card.x + inset, top, width, rowHeight, ref row, "VALUABLES", _result.Valuables);

            if (row == 0)
            {
                GUI.Label(new Rect(card.x + inset, top, width, rowHeight), "NOTHING DETECTED", _summaryStyle);
            }
        }

        private static void DrawSummaryRow(
            float x,
            float top,
            float width,
            float rowHeight,
            ref int row,
            string label,
            int count)
        {
            if (count <= 0)
            {
                return;
            }

            GUI.Label(new Rect(x, top + row * rowHeight, width, rowHeight), $"{label}   {count}", _summaryStyle);
            row++;
        }

        private static void DrawCloseButton(Rect card, float scale)
        {
            Rect closeRect = new Rect(
                card.xMax - 31.0f * scale,
                card.y + 7.0f * scale,
                23.0f * scale,
                23.0f * scale);
            if (GUI.Button(closeRect, "X", _closeStyle))
            {
                _dismissed = true;
                SetState(PanelState.Hidden);
                ZLog.Log($"[{TotemSentinelPlugin.PluginName}] Final camp-check summary dismissed by the player.");
            }
        }

        private static void UpdateState()
        {
            if (_finalScanStarted >= 0.0f
                && Time.unscaledTime - _finalScanStarted >= GetFinalSummaryDuration())
            {
                if (_state != PanelState.Hidden)
                {
                    ZLog.Log($"[{TotemSentinelPlugin.PluginName}] Final camp-check summary closed after its timer elapsed.");
                }

                _dismissed = true;
                _state = PanelState.Hidden;
                return;
            }

            float elapsed = Time.unscaledTime - _stateStarted;
            float stateDuration = GetStateDuration();
            if (_state == PanelState.Cursed && Time.unscaledTime >= _cursedUntil)
            {
                SetState(TotemSentinelPlugin.ActiveSonarCharges > 0 ? PanelState.Ready : PanelState.Complete);
            }
            else if ((_state == PanelState.Found || _state == PanelState.Clear) && elapsed >= stateDuration)
            {
                SetState(TotemSentinelPlugin.ActiveSonarCharges > 0 ? PanelState.Ready : PanelState.Complete);
            }
            else if ((_state == PanelState.OutOfRange || _state == PanelState.Unavailable) && elapsed >= stateDuration)
            {
                SetState(TotemSentinelPlugin.ActiveSonarCharges > 0 ? PanelState.Ready : PanelState.Complete);
            }
            else if (_state == PanelState.Depleted && elapsed >= stateDuration)
            {
                SetState(_hasResult ? PanelState.Complete : PanelState.Hidden);
            }
        }

        private static bool ShouldShowSummary()
        {
            if (!_hasResult)
            {
                return false;
            }

            return _state == PanelState.Ready
                || _state == PanelState.Scanning
                || _state == PanelState.Found
                || _state == PanelState.Clear
                || _state == PanelState.Complete
                || _state == PanelState.Depleted
                || _state == PanelState.Cursed;
        }

        private static Rect GetCardRect(bool showSummary)
        {
            float scale = Mathf.Clamp(Screen.height / 1080.0f, 0.80f, 1.45f);
            float width = 230.0f * scale;
            float summaryRows = showSummary ? Mathf.Max(1, GetSummaryRowCount()) : 0;
            float height = (125.0f + summaryRows * 17.0f + (showSummary ? 8.0f : 0.0f)) * scale;
            float x = Screen.width - width - 22.0f * scale;
            float y = 285.0f * scale;

            Minimap minimap = Minimap.instance;
            if (minimap != null
                && minimap.m_smallRoot != null
                && minimap.m_smallRoot.activeInHierarchy
                && minimap.m_mapImageSmall != null)
            {
                minimap.m_mapImageSmall.rectTransform.GetWorldCorners(MapCorners);
                float mapRight = MapCorners[0].x;
                float mapBottom = MapCorners[0].y;
                for (int index = 1; index < MapCorners.Length; index++)
                {
                    mapRight = Mathf.Max(mapRight, MapCorners[index].x);
                    mapBottom = Mathf.Min(mapBottom, MapCorners[index].y);
                }

                x = mapRight - width;
                y = Screen.height - mapBottom + 10.0f * scale;
            }

            x = Mathf.Clamp(x, 8.0f, Screen.width - width - 8.0f);
            y = Mathf.Clamp(y, 8.0f, Screen.height - height - 8.0f);
            return new Rect(x, y, width, height);
        }

        private static int GetSummaryRowCount()
        {
            int rows = 0;
            rows += _result.Goblins > 0 ? 1 : 0;
            rows += _result.Shamans > 0 ? 1 : 0;
            rows += _result.Brutes > 0 ? 1 : 0;
            rows += _result.Coins > 0 ? 1 : 0;
            rows += _result.BlackMetal > 0 ? 1 : 0;
            rows += _result.Valuables > 0 ? 1 : 0;
            return rows;
        }

        private static Color GetAccentColor(bool preview)
        {
            switch (_state)
            {
                case PanelState.Cursed:
                    return RedColor;
                case PanelState.Scanning:
                    return _isGreedScan ? GoldColor : OrangeColor;
                case PanelState.Found:
                    return RedColor;
                case PanelState.Clear:
                    return GreenColor;
                case PanelState.Complete:
                    return GoldColor;
                case PanelState.OutOfRange:
                case PanelState.Depleted:
                case PanelState.Unavailable:
                    return OrangeColor;
                case PanelState.Ready:
                    return ReadyColor;
                default:
                    return preview ? MutedColor : ReadyColor;
            }
        }

        private static string FormatFinalTimeRemaining()
        {
            int remaining = Mathf.Max(
                0,
                Mathf.CeilToInt(GetFinalSummaryDuration() - (Time.unscaledTime - _finalScanStarted)));
            return $"{remaining / 60}:{remaining % 60:00}";
        }

        private static float GetStateDuration()
        {
            return TotemSentinelPlugin.StateDuration != null
                ? TotemSentinelPlugin.StateDuration.Value
                : 10.0f;
        }

        private static float GetFinalSummaryDuration()
        {
            return TotemSentinelPlugin.FinalSummaryDuration != null
                ? TotemSentinelPlugin.FinalSummaryDuration.Value
                : 300.0f;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void SetState(PanelState state)
        {
            _state = state;
            _stateStarted = Time.unscaledTime;
        }

        private static void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = CreateCenteredStyle(FontStyle.Bold, ReadyColor);
            _mainStyle = CreateCenteredStyle(FontStyle.Bold, ReadyColor);
            _detailStyle = CreateCenteredStyle(FontStyle.Normal, ReadyColor);
            _detailStyle.wordWrap = true;
            _summaryStyle = CreateCenteredStyle(FontStyle.Normal, GoldColor);
            _closeStyle = CreateCenteredStyle(FontStyle.Bold, ReadyColor);
            _closeStyle.hover.textColor = RedColor;
            _closeStyle.active.textColor = OrangeColor;
        }

        private static GUIStyle CreateCenteredStyle(FontStyle fontStyle, Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = fontStyle,
                clipping = TextClipping.Clip,
                normal = { textColor = color }
            };
        }
    }
}

