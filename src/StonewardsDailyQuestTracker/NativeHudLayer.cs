using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// A true UI Toolkit HUD. Unlike an IMGUIContainer/MonoBehaviour.OnGUI,
    /// native VisualElements respect sibling paint order. We append the quest
    /// panel after the HUD and raise QuestNotificationHolder above it.
    /// The existing QuestHud remains the safe legacy fallback.
    /// </summary>
    internal sealed class NativeHudLayer
    {
        private const float Width = 310f;
        private const float HeaderHeight = 32f;
        private const float ItemHeight = 30f;
        private const float FooterHeight = 18f;
        private const float EdgeMargin = 8f;
        private const int MaxRows = 5;
        private const float PinnedGap = 8f;
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float PanelSizeBoost = .95f;
        private const float RetrySeconds = 5f;

        private static readonly Color GoldFrame = new Color(.72f, .64f, .48f, 1f);
        private static readonly Color MainFill = new Color(19f / 255f, 23f / 255f, 32f / 255f, .96f);
        private static readonly Color HeaderFill = new Color(76f / 255f, 46f / 255f, 48f / 255f, 1f);
        private static readonly Color GoldText = new Color(.98f, .79f, .51f, 1f);
        private static readonly Color PrimaryText = new Color(.98f, .90f, .76f, 1f);
        private static readonly Color SuccessText = new Color(.47f, .87f, .56f, 1f);
        private static readonly Color RowDivider = new Color(.71f, .63f, .48f, .27f);

        private readonly ManualLogSource _log;
        private VisualElement _panelRoot;
        private VisualElement _parent;
        private VisualElement _notificationBranch;
        private VisualElement _panel;
        // Mirror QuestHud.DrawBackground/DrawFrame exactly: two stepped
        // background pieces, two stepped header pieces and twelve border
        // segments. A plain CSS border loses the game's cut-off corners.
        private readonly VisualElement[] _backgroundPieces = new VisualElement[2];
        private readonly VisualElement[] _headerPieces = new VisualElement[2];
        private readonly VisualElement[] _framePieces = new VisualElement[12];
        private VisualElement _headerDivider;
        private VisualElement _headerAccent;
        private Label _heading;
        private Label _count;
        private Label _empty;
        private Label _footer;
        private readonly VisualElement[] _bars = new VisualElement[MaxRows];
        private readonly Label[] _ticks = new Label[MaxRows];
        private readonly Label[] _titles = new Label[MaxRows];
        private readonly Label[] _counters = new Label[MaxRows];
        private readonly VisualElement[] _dividers = new VisualElement[MaxRows];
        private readonly bool[] _completedRows = new bool[MaxRows];
        private VisualElement _pinnedGroup;
        private readonly VisualElement[] _pinBackground = new VisualElement[2];
        private readonly VisualElement[] _pinHeader = new VisualElement[2];
        private readonly VisualElement[] _pinFrame = new VisualElement[12];
        private VisualElement _pinDivider;
        private Label _pinHeading;
        private Label _pinCount;
        private readonly VisualElement[] _pinBars = new VisualElement[PinnedMissions.MaxPins];
        private readonly Label[] _pinTicks = new Label[PinnedMissions.MaxPins];
        private readonly Label[] _pinTitles = new Label[PinnedMissions.MaxPins];
        private readonly Label[] _pinCounters = new Label[PinnedMissions.MaxPins];
        private readonly VisualElement[] _pinDividers = new VisualElement[PinnedMissions.MaxPins];
        private List<QuestView> _previousPins;

        private Font _font;
        private Font _checkFont;
        private float _nextSearch;
        private bool _reportedFailure;
        private float _previousX = float.NaN;
        private float _previousY = float.NaN;
        private float _previousSx = float.NaN;
        private float _previousSy = float.NaN;
        private float _previousHeight = float.NaN;
        private List<QuestView> _previousQuests;
        private CompletedQuestMode _previousMode;
        private int _previousTotal = -1;
        private bool _previousHasSeen;

        public NativeHudLayer(ManualLogSource log)
        {
            _log = log;
        }

        public bool Attached =>
            _panel != null && _parent != null && _panelRoot != null &&
            _notificationBranch != null &&
            _panel.parent == _parent &&
            _notificationBranch.parent == _parent &&
            _panel.panel != null && _panel.panel == _panelRoot.panel;

        // OnGUI must not also paint the panel once native labels are visible:
        // the OnGUI fallback would otherwise sit on top of notifications.
        public bool IsDrawing =>
            Attached && _panel.resolvedStyle.display != DisplayStyle.None &&
            _panel.worldBound.width > 2f && _panel.worldBound.height > 2f;

        public void Attach(VisualElement root)
        {
            if (root == null || root.panel == null)
            {
                Detach();
                return;
            }
            if (Attached && ReferenceEquals(_panelRoot, root))
                return;

            if (_panel != null) Detach();
            if (Time.unscaledTime < _nextSearch) return;

            try
            {
                // Confirmed in the game's F9 diagnostic:
                // [UI] > QuestNotificationHolder > QuestNotificationParent > UINotification.
                VisualElement holder = root.Q<VisualElement>("QuestNotificationHolder");
                if (holder == null || holder.panel != root.panel)
                {
                    Retry("QuestNotificationHolder unavailable.");
                    return;
                }

                VisualElement notificationBranch = holder;
                VisualElement parent = holder.parent;
                Rect viewport = root.worldBound;
                while (parent != null && !CoversViewport(parent.worldBound, viewport))
                {
                    notificationBranch = parent;
                    parent = parent.parent;
                }
                if (parent == null)
                {
                    Retry("No screen-sized ancestor for QuestNotificationHolder.");
                    return;
                }

                InitializeStyles();
                BuildPanel();
                // Append our native panel after the existing HUD background,
                // then move ONLY the game's notification branch to the very
                // front. Otherwise later full-screen siblings could obscure
                // the entire quest panel (the original hidden-HUD bug).
                parent.Add(_panel);
                notificationBranch.BringToFront();
                _panelRoot = root;
                _parent = parent;
                _notificationBranch = notificationBranch;
                _previousQuests = null;
                _previousHeight = float.NaN;
                _previousX = float.NaN;
                _reportedFailure = false;
                _log.LogInfo("Native quest HUD inserted before notification branch '" +
                    notificationBranch.name + "'. No IMGUIContainer is used.");
            }
            catch (Exception ex)
            {
                Detach();
                Retry("Native quest HUD failed: " + ex.Message);
            }
        }

        private void Retry(string reason)
        {
            _nextSearch = Time.unscaledTime + RetrySeconds;
            if (_reportedFailure) return;
            _reportedFailure = true;
            _log.LogWarning(reason + " Using legacy OnGUI HUD.");
        }

        private static bool CoversViewport(Rect candidate, Rect viewport)
        {
            return candidate.width >= viewport.width * .92f &&
                   candidate.height >= viewport.height * .92f &&
                   candidate.xMin <= viewport.xMin + viewport.width * .04f &&
                   candidate.yMin <= viewport.yMin + viewport.height * .04f &&
                   candidate.xMax >= viewport.xMax - viewport.width * .04f &&
                   candidate.yMax >= viewport.yMax - viewport.height * .04f;
        }

        public void Sync(List<QuestView> quests, bool visible, PanelCorner anchor,
            int horizontalInset, int verticalInset, float requestedScale,
            float readabilityScale, bool autoScaleToResolution,
            CompletedQuestMode completedMode, bool showStatus,
            Rect? currencyBadge, int currencyGap, int totalDailyQuests,
            bool hasSeenDailyQuests, List<QuestView> pinned)
        {
            if (!Attached) return;

            int remaining = 0;
            for (int i = 0; i < quests.Count; i++)
                if (!quests[i].Completed) remaining++;

            int matching = completedMode == CompletedQuestMode.Hide
                ? remaining : quests.Count;
            bool empty = matching == 0;
            int pinRows = Math.Min(pinned.Count, PinnedMissions.MaxPins);
            bool shouldShow = visible &&
                (pinRows > 0 ||
                 !(empty && quests.Count == 0 && !showStatus && !hasSeenDailyQuests));

            if (!shouldShow)
            {
                _panel.style.display = DisplayStyle.None;
                return;
            }
            if (_panel.resolvedStyle.display == DisplayStyle.None)
                _panel.style.display = DisplayStyle.Flex;

            int rows = Math.Min(matching, MaxRows);
            int overflow = Math.Max(0, matching - rows);
            float height = HeaderHeight + (empty ? 39f : rows * ItemHeight + 5f) +
                           (overflow > 0 ? FooterHeight : 0f);
            float pinnedHeight = pinRows > 0
                ? HeaderHeight + pinRows * ItemHeight + 5f : 0f;
            float totalHeight = height + (pinRows > 0 ? PinnedGap + pinnedHeight : 0f);
            float baseScale = Mathf.Clamp(requestedScale *
                Mathf.Clamp(readabilityScale, 1f, 1.4f) * PanelSizeBoost, .55f, 1.8f);
            float resolutionScale = autoScaleToResolution
                ? Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight)
                : 1f;
            float factor = baseScale * resolutionScale;
            factor = Mathf.Min(factor, (Screen.width - EdgeMargin * 2f) / Width,
                (Screen.height - EdgeMargin * 2f) / totalHeight);

            bool followCurrency = anchor == PanelCorner.TopRight &&
                currencyBadge.HasValue && currencyBadge.Value.yMax >= 0f &&
                currencyBadge.Value.yMax < Screen.height - 60f;
            float badgeBottom = followCurrency ? currencyBadge.Value.yMax : 0f;
            float gap = Mathf.Max(0, currencyGap);
            if (followCurrency)
            {
                float below = Screen.height - EdgeMargin - badgeBottom - gap;
                if (below >= 48f) factor = Mathf.Min(factor, below / totalHeight);
                else followCurrency = false;
            }
            if (autoScaleToResolution && !followCurrency)
            {
                float usable = Screen.height -
                    Mathf.Max(EdgeMargin, verticalInset) - EdgeMargin;
                if (usable >= 72f) factor = Mathf.Min(factor, usable / totalHeight);
            }
            if (factor <= 0f) return;

            float widthPixels = Width * factor, heightPixels = totalHeight * factor;
            bool right = anchor == PanelCorner.TopRight || anchor == PanelCorner.BottomRight;
            bool bottom = anchor == PanelCorner.BottomRight || anchor == PanelCorner.BottomLeft;
            float x = right ? Screen.width - Mathf.Max(EdgeMargin, horizontalInset) - widthPixels :
                Mathf.Max(EdgeMargin, horizontalInset);
            float y = bottom ? Screen.height - Mathf.Max(EdgeMargin, verticalInset) - heightPixels :
                Mathf.Max(EdgeMargin, verticalInset);
            if (followCurrency)
            {
                x = currencyBadge.Value.xMax - widthPixels;
                y = badgeBottom + gap;
            }
            x = Mathf.Clamp(x, EdgeMargin, Screen.width - widthPixels - EdgeMargin);
            y = Mathf.Clamp(y, EdgeMargin, Screen.height - heightPixels - EdgeMargin);

            // The parent can start at a different panel-space position than
            // the root. Convert from physical screen pixels to parent-local
            // panel points before assigning absolute CSS positions.
            Rect viewport = _panelRoot.worldBound;
            Rect parentBounds = _parent.worldBound;
            float ux = viewport.width / Screen.width;
            float uy = viewport.height / Screen.height;
            float panelX = x * ux - (parentBounds.xMin - viewport.xMin);
            float panelY = y * uy - (parentBounds.yMin - viewport.yMin);
            float sx = factor * ux;
            float sy = factor * uy;

            if (Different(panelX, _previousX) || Different(panelY, _previousY))
            {
                _panel.style.left = panelX;
                _panel.style.top = panelY;
                _previousX = panelX;
                _previousY = panelY;
            }
            bool geometryChanged = Different(sx, _previousSx) ||
                Different(sy, _previousSy) || Different(totalHeight, _previousHeight);
            if (geometryChanged)
            {
                Layout(sx, sy, height, totalHeight, rows, overflow, pinRows);
                _previousSx = sx;
                _previousSy = sy;
                _previousHeight = totalHeight;
            }

            int total = Math.Max(quests.Count, totalDailyQuests);
            if (!ReferenceEquals(_previousQuests, quests) ||
                _previousMode != completedMode || _previousTotal != total ||
                _previousHasSeen != hasSeenDailyQuests || geometryChanged)
            {
                Populate(quests, completedMode, remaining, total, rows,
                    overflow, empty, hasSeenDailyQuests);
                _previousQuests = quests;
                _previousMode = completedMode;
                _previousTotal = total;
                _previousHasSeen = hasSeenDailyQuests;
            }
            if (!ReferenceEquals(_previousPins, pinned) || geometryChanged)
            {
                PopulatePins(pinned);
                _previousPins = pinned;
            }
        }

        private static bool Different(float a, float b)
        {
            return float.IsNaN(b) || Mathf.Abs(a - b) > .01f;
        }

        private void InitializeStyles()
        {
            if (_font != null) return;
            foreach (Font candidate in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (candidate == null) continue;
                if (candidate.name == "Jersey10-Regular")
                {
                    _font = candidate;
                    break;
                }
                if (_font == null && candidate.name == "Jersey10-Regular2")
                    _font = candidate;
            }
            try { _checkFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch (Exception) { }
            if (_font == null) _font = _checkFont;
        }

        private void BuildPanel()
        {
            _panel = new VisualElement { name = "StonewardsDailyQuestTracker" };
            _panel.pickingMode = PickingMode.Ignore;
            _panel.style.position = Position.Absolute;
            // Keep the panel container itself transparent. The stepped
            // silhouette must expose the world at all four cut-off corners.
            _panel.style.backgroundColor = Color.clear;
            _panel.style.overflow = Overflow.Hidden;

            // Order matters: background, title bar, frame, then text.
            // Reproduce the exact geometry of legacy QuestHud.DrawBackground.
            for (int i = 0; i < _backgroundPieces.Length; i++)
                _backgroundPieces[i] = MakeRect(_panel, MainFill);
            for (int i = 0; i < _headerPieces.Length; i++)
                _headerPieces[i] = MakeRect(_panel, HeaderFill);
            for (int i = 0; i < _framePieces.Length; i++)
                _framePieces[i] = MakeRect(_panel, GoldFrame);
            _headerDivider = MakeRect(_panel, GoldFrame);
            _headerAccent = MakeRect(_panel, GoldText);
            _heading = MakeLabel(_panel, GoldText, TextAnchor.MiddleLeft);
            _count = MakeLabel(_panel, GoldText, TextAnchor.MiddleRight);
            _empty = MakeLabel(_panel, PrimaryText, TextAnchor.MiddleCenter);
            _footer = MakeLabel(_panel, GoldText, TextAnchor.MiddleCenter);
            _heading.text = "DAILY QUESTS";

            for (int i = 0; i < MaxRows; i++)
            {
                _bars[i] = MakeRect(_panel, GoldText);
                _ticks[i] = MakeLabel(_panel, SuccessText, TextAnchor.MiddleLeft, _checkFont);
                _ticks[i].text = "✔";
                _titles[i] = MakeLabel(_panel, PrimaryText, TextAnchor.MiddleLeft);
                _counters[i] = MakeLabel(_panel, GoldText, TextAnchor.MiddleRight);
                _dividers[i] = MakeRect(_panel, RowDivider);
            }

            _pinnedGroup = new VisualElement
            {
                name = "DailyQuestTrackerPinnedMissions",
                pickingMode = PickingMode.Ignore
            };
            _pinnedGroup.style.position = Position.Absolute;
            _pinnedGroup.style.overflow = Overflow.Hidden;
            _panel.Add(_pinnedGroup);
            for (int i = 0; i < 2; i++)
            {
                _pinBackground[i] = MakeRect(_pinnedGroup, MainFill);
                _pinHeader[i] = MakeRect(_pinnedGroup, HeaderFill);
            }
            for (int i = 0; i < _pinFrame.Length; i++)
                _pinFrame[i] = MakeRect(_pinnedGroup, GoldFrame);
            _pinDivider = MakeRect(_pinnedGroup, GoldFrame);
            _pinHeading = MakeLabel(_pinnedGroup, GoldText, TextAnchor.MiddleLeft);
            _pinHeading.text = "PINNED QUESTS";
            _pinCount = MakeLabel(_pinnedGroup, GoldText, TextAnchor.MiddleRight);
            for (int i = 0; i < PinnedMissions.MaxPins; i++)
            {
                _pinBars[i] = MakeRect(_pinnedGroup, GoldText);
                _pinTicks[i] = MakeLabel(_pinnedGroup, SuccessText, TextAnchor.MiddleLeft, _checkFont);
                _pinTicks[i].text = "✔";
                _pinTitles[i] = MakeLabel(_pinnedGroup, PrimaryText, TextAnchor.MiddleLeft);
                _pinCounters[i] = MakeLabel(_pinnedGroup, GoldText, TextAnchor.MiddleRight);
                _pinDividers[i] = MakeRect(_pinnedGroup, RowDivider);
            }
        }

        private static VisualElement MakeRect(VisualElement parent, Color color)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.backgroundColor = color;
            parent.Add(element);
            return element;
        }

        private Label MakeLabel(VisualElement parent, Color color, TextAnchor anchor,
            Font font = null)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.color = color;
            label.style.unityTextAlign = anchor;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            if (font != null || _font != null)
                label.style.unityFont = font ?? _font;
            parent.Add(label);
            return label;
        }

        private static void SetRect(VisualElement element,
            float x, float y, float width, float height, float sx, float sy)
        {
            element.style.left = x * sx;
            element.style.top = y * sy;
            element.style.width = Mathf.Max(0f, width * sx);
            element.style.height = Mathf.Max(0f, height * sy);
        }

        private static void SetFont(Label label, float points, float sy)
        {
            label.style.fontSize = Mathf.Max(9f, points * sy);
        }

        private void Layout(float sx, float sy, float height, float totalHeight,
            int rows, int overflow, int pinRows)
        {
            _panel.style.width = Width * sx;
            _panel.style.height = totalHeight * sy;

            // The original pixel-stepped shape from QuestHud.DrawBackground.
            SetRect(_backgroundPieces[0], 7, 0, Width - 14, height, sx, sy);
            SetRect(_backgroundPieces[1], 0, 7, Width, height - 14, sx, sy);
            SetRect(_headerPieces[0], 7, 2, Width - 14, HeaderHeight - 2, sx, sy);
            SetRect(_headerPieces[1], 2, 7, Width - 4, HeaderHeight - 7, sx, sy);

            // QuestHud.DrawFrame: gold 2 px outline with clipped stair corners.
            SetRect(_framePieces[0], 8, 0, Width - 16, 2, sx, sy);
            SetRect(_framePieces[1], 5, 2, 3, 2, sx, sy);
            SetRect(_framePieces[2], 2, 5, 3, 2, sx, sy);
            SetRect(_framePieces[3], Width - 8, 2, 3, 2, sx, sy);
            SetRect(_framePieces[4], Width - 5, 5, 3, 2, sx, sy);
            SetRect(_framePieces[5], 0, 7, 2, height - 14, sx, sy);
            SetRect(_framePieces[6], Width - 2, 7, 2, height - 14, sx, sy);
            SetRect(_framePieces[7], 8, height - 2, Width - 16, 2, sx, sy);
            SetRect(_framePieces[8], 5, height - 4, 3, 2, sx, sy);
            SetRect(_framePieces[9], 2, height - 7, 3, 2, sx, sy);
            SetRect(_framePieces[10], Width - 8, height - 4, 3, 2, sx, sy);
            SetRect(_framePieces[11], Width - 5, height - 7, 3, 2, sx, sy);

            SetRect(_headerDivider, 2, HeaderHeight - 1, Width - 4, 1, sx, sy);
            SetRect(_headerAccent, 10, HeaderHeight - 4, 46, 1, sx, sy);
            SetRect(_heading, 13, 3, Width - 132, 26, sx, sy);
            SetRect(_count, Width - 120, 3, 105, 26, sx, sy);
            SetRect(_empty, 13, HeaderHeight + 5, Width - 26, 30, sx, sy);
            SetRect(_footer, 13, HeaderHeight + 3 + rows * ItemHeight,
                Width - 26, FooterHeight - 2, sx, sy);
            SetFont(_heading, 18, sy);
            SetFont(_count, 16, sy);
            SetFont(_empty, 15, sy);
            SetFont(_footer, 13, sy);
            for (int i = 0; i < MaxRows; i++)
            {
                float rowY = HeaderHeight + 3f + i * ItemHeight;
                SetRect(_bars[i], 8, rowY + 6, 2, 17, sx, sy);
                SetRect(_ticks[i], 7, rowY + 2, 24, 24, sx, sy);
                SetRect(_titles[i], 16, rowY + 1, Width - 16 - 75, 24, sx, sy);
                SetRect(_counters[i], Width - 70, rowY + 1, 57, 24, sx, sy);
                SetRect(_dividers[i], 11, rowY + ItemHeight - 2,
                    Width - 22, 1, sx, sy);
                SetFont(_ticks[i], 19, sy);
                SetFont(_titles[i], 17, sy);
                SetFont(_counters[i], 16, sy);
            }
            LayoutPins(sx, sy, height, pinRows);
        }

        private void LayoutPins(float sx, float sy, float dailyHeight, int rows)
        {
            _pinnedGroup.style.display = rows > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (rows == 0) return;
            float h = HeaderHeight + rows * ItemHeight + 5f;
            SetRect(_pinnedGroup, 0, dailyHeight + PinnedGap, Width, h, sx, sy);
            SetRect(_pinBackground[0], 7, 0, Width - 14, h, sx, sy);
            SetRect(_pinBackground[1], 0, 7, Width, h - 14, sx, sy);
            SetRect(_pinHeader[0], 7, 2, Width - 14, HeaderHeight - 2, sx, sy);
            SetRect(_pinHeader[1], 2, 7, Width - 4, HeaderHeight - 7, sx, sy);
            // The same stepped gold frame used by the daily section.
            SetRect(_pinFrame[0], 8, 0, Width - 16, 2, sx, sy);
            SetRect(_pinFrame[1], 5, 2, 3, 2, sx, sy);
            SetRect(_pinFrame[2], 2, 5, 3, 2, sx, sy);
            SetRect(_pinFrame[3], Width - 8, 2, 3, 2, sx, sy);
            SetRect(_pinFrame[4], Width - 5, 5, 3, 2, sx, sy);
            SetRect(_pinFrame[5], 0, 7, 2, h - 14, sx, sy);
            SetRect(_pinFrame[6], Width - 2, 7, 2, h - 14, sx, sy);
            SetRect(_pinFrame[7], 8, h - 2, Width - 16, 2, sx, sy);
            SetRect(_pinFrame[8], 5, h - 4, 3, 2, sx, sy);
            SetRect(_pinFrame[9], 2, h - 7, 3, 2, sx, sy);
            SetRect(_pinFrame[10], Width - 8, h - 4, 3, 2, sx, sy);
            SetRect(_pinFrame[11], Width - 5, h - 7, 3, 2, sx, sy);
            SetRect(_pinDivider, 2, HeaderHeight - 1, Width - 4, 1, sx, sy);
            SetRect(_pinHeading, 13, 3, Width - 132, 26, sx, sy);
            SetRect(_pinCount, Width - 120, 3, 105, 26, sx, sy);
            SetFont(_pinHeading, 18, sy);
            SetFont(_pinCount, 15, sy);
            for (int i = 0; i < PinnedMissions.MaxPins; i++)
            {
                float y = HeaderHeight + 3f + i * ItemHeight;
                SetRect(_pinBars[i], 8, y + 6, 2, 17, sx, sy);
                SetRect(_pinTicks[i], 7, y + 2, 24, 24, sx, sy);
                SetRect(_pinTitles[i], 16, y + 1, 219, 24, sx, sy);
                SetRect(_pinCounters[i], Width - 70, y + 1, 57, 24, sx, sy);
                SetRect(_pinDividers[i], 11, y + ItemHeight - 2, Width - 22, 1, sx, sy);
                SetFont(_pinTicks[i], 19, sy);
                SetFont(_pinTitles[i], 17, sy);
                SetFont(_pinCounters[i], 16, sy);
            }
        }

        private void PopulatePins(List<QuestView> pins)
        {
            int count = Math.Min(pins.Count, PinnedMissions.MaxPins);
            _pinCount.text = count + "/" + PinnedMissions.MaxPins;
            for (int i = 0; i < PinnedMissions.MaxPins; i++)
            {
                bool active = i < count;
                QuestView q = active ? pins[i] : null;
                bool done = active && q.Completed;
                _pinBars[i].style.display = active && !done ? DisplayStyle.Flex : DisplayStyle.None;
                _pinTicks[i].style.display = done ? DisplayStyle.Flex : DisplayStyle.None;
                _pinTitles[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                _pinCounters[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                _pinDividers[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                if (!active) continue;
                _pinTitles[i].style.color = done ? SuccessText : PrimaryText;
                _pinCounters[i].style.color = done ? SuccessText : GoldText;
                _pinTitles[i].text = q.Title ?? "Mission";
                _pinCounters[i].text = q.Progress.HasValue && q.Target.HasValue
                    ? Math.Min(q.Progress.Value, q.Target.Value) + "/" + q.Target.Value
                    : done ? "DONE" : "—";
            }
        }

        private void Populate(List<QuestView> quests, CompletedQuestMode mode,
            int remaining, int total, int rows, int overflow,
            bool empty, bool hasSeenDailyQuests)
        {
            _count.text = quests.Count == 0 && !hasSeenDailyQuests
                ? "—" : remaining + "/" + total + " LEFT";
            _empty.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.text = quests.Count == 0 && !hasSeenDailyQuests
                ? "QUEST PROGRESS NOT DETECTED" : "ALL QUESTS COMPLETED";
            _footer.style.display = overflow > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _footer.text = "+ " + overflow + " MORE QUESTS";

            int row = 0;
            for (int phase = 0; phase < (mode == CompletedQuestMode.Mark ? 2 : 1); phase++)
            {
                for (int i = 0; i < quests.Count && row < MaxRows; i++)
                {
                    QuestView q = quests[i];
                    if (q.Completed != (phase == 1)) continue;
                    bool done = q.Completed;
                    _completedRows[row] = done;
                    _bars[row].style.display = done ? DisplayStyle.None : DisplayStyle.Flex;
                    _ticks[row].style.display = done ? DisplayStyle.Flex : DisplayStyle.None;
                    _titles[row].style.color = done ? SuccessText : PrimaryText;
                    _counters[row].style.color = done ? SuccessText : GoldText;
                    _titles[row].text = q.Title ?? "Daily quest";
                    _counters[row].text = q.Progress.HasValue && q.Target.HasValue
                        ? Math.Min(q.Progress.Value, q.Target.Value) + "/" + q.Target.Value
                        : done ? "DONE" : "—";
                    _titles[row].style.left = (done ? 33f : 16f) * _previousSx;
                    _titles[row].style.width = (done ? 202f : 219f) * _previousSx;
                    row++;
                }
            }

            for (int i = 0; i < MaxRows; i++)
            {
                bool active = i < row;
                _bars[i].style.display = active && !_completedRows[i]
                    ? DisplayStyle.Flex : DisplayStyle.None;
                _ticks[i].style.display = active && _completedRows[i]
                    ? DisplayStyle.Flex : DisplayStyle.None;
                _titles[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                _counters[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                _dividers[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Detach()
        {
            if (_panel != null)
            {
                try { _panel.RemoveFromHierarchy(); }
                catch (Exception) { }
            }
            _panel = null;
            _panelRoot = null;
            _parent = null;
            _notificationBranch = null;
            _previousQuests = null;
            _previousPins = null;
            _previousX = float.NaN;
            _previousY = float.NaN;
            _previousSx = float.NaN;
            _previousSy = float.NaN;
            _previousHeight = float.NaN;
        }
    }
}
