using System;
using System.Collections.Generic;
using UnityEngine;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Read-only, compact text HUD: one quest name and numeric progress per row.
    /// Uses PartyHUD styling and resolution scaling without progress bars.
    /// Game data is sampled elsewhere, never during rendering.
    /// </summary>
    internal sealed class QuestHud
    {
        private const float Width = 310f;
        private const float HeaderHeight = 32f;
        private const float ItemHeight = 30f;
        private const float FooterHeight = 18f;
        private const int MaxVisible = 5;
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float EdgeMargin = 8f;
        // Match PartyHUD's default multiplier and readability behavior.
        private const float PanelSizeBoost = 0.95f;

        private static readonly Color GoldFrame = new Color(.72f, .64f, .48f, 1f);
        private static readonly Color MainFill = new Color(19f / 255f, 23f / 255f, 32f / 255f, .96f);
        private static readonly Color HeaderFill = new Color(76f / 255f, 46f / 255f, 48f / 255f, 1f);
        private static readonly Color GoldText = new Color(.98f, .79f, .51f, 1f);
        private static readonly Color PrimaryText = new Color(.98f, .90f, .76f, 1f);
        private static readonly Color SecondaryText = new Color(.76f, .76f, .73f, 1f);
        private static readonly Color Success = new Color(.47f, .87f, .56f, 1f);
        private static readonly Color RowDivider = new Color(.71f, .63f, .48f, .27f);

        private GUIStyle _heading;
        private GUIStyle _rowTitle;
        private GUIStyle _rowDone;
        private GUIStyle _rowCounter;
        private GUIStyle _rowCounterDone;
        private GUIStyle _subtle;
        private GUIStyle _emptyTitle;
        private Font _font;
        private readonly Dictionary<string, string> _fittedLabels =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _fittedCompletedLabels =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public void Draw(List<QuestView> quests, PanelCorner anchor,
            int horizontalInset, int verticalInset, float requestedScale,
            float readabilityScale, bool autoScaleToResolution,
            CompletedQuestMode completedMode, bool showStatus,
            Rect? currencyBadge, int currencyGap)
        {
            if (_heading == null) InitializeStyles();

            // This is only a tiny cached list of live quest states, never an
            // enumeration of the game's database assets.
            int remaining = 0;
            for (int i = 0; i < quests.Count; i++)
                if (!quests[i].Completed)
                    remaining++;
            int matching = completedMode == CompletedQuestMode.Hide
                ? remaining : quests.Count;

            bool empty = matching == 0;
            if (empty && quests.Count == 0 && !showStatus) return;
            int visible = Math.Min(matching, MaxVisible);
            int overflow = Math.Max(0, matching - visible);
            float height = HeaderHeight + (empty ? 39f : visible * ItemHeight + 5f) +
                           (overflow > 0 ? FooterHeight : 0f);

            // Mirror PartyHUD: clamp preference before multiplying by the
            // resolution ratio. This allows proportional enlargement at 1440p/4K
            // instead of capping the final (resolution-adjusted) GUI scale.
            float baseScale = Mathf.Clamp(requestedScale *
                Mathf.Clamp(readabilityScale, 1f, 1.4f) *
                PanelSizeBoost, 0.55f, 1.8f);
            float resolutionScale = autoScaleToResolution
                ? Mathf.Min(Screen.width / ReferenceWidth,
                    Screen.height / ReferenceHeight)
                : 1f;
            float factor = baseScale * resolutionScale;

            // Offsets are physical screen pixels, independent of GUI.matrix.
            // Reduce size only as needed to fit within the actual display.
            factor = Mathf.Min(factor, (Screen.width - EdgeMargin * 2f) / Width,
                (Screen.height - EdgeMargin * 2f) / height);

            bool followCurrency = anchor == PanelCorner.TopRight &&
                currencyBadge.HasValue && currencyBadge.Value.yMax >= 0f &&
                currencyBadge.Value.yMax < Screen.height - 60f;
            float badgeBottom = followCurrency ? currencyBadge.Value.yMax : 0f;
            float gap = Mathf.Max(0, currencyGap);
            if (followCurrency)
            {
                // Fit entirely below the currency badge rather than using an
                // arbitrary top-edge offset. UI is allowed to change at runtime.
                float below = Screen.height - EdgeMargin - badgeBottom - gap;
                if (below >= 48f)
                    factor = Mathf.Min(factor, below / height);
                else
                    followCurrency = false;
            }
            if (autoScaleToResolution && !followCurrency)
            {
                // Manual inset is a fallback when no currency HUD was found.
                float usableHeight = Screen.height -
                    Mathf.Max(EdgeMargin, verticalInset) - EdgeMargin;
                if (usableHeight >= 72f)
                    factor = Mathf.Min(factor, usableHeight / height);
            }
            if (factor <= 0f)
                return;

            float panelWidth = Width * factor;
            float panelHeight = height * factor;
            bool right = anchor == PanelCorner.TopRight || anchor == PanelCorner.BottomRight;
            bool bottom = anchor == PanelCorner.BottomRight || anchor == PanelCorner.BottomLeft;
            float x = right ? Screen.width - Mathf.Max(EdgeMargin, horizontalInset) - panelWidth :
                Mathf.Max(EdgeMargin, horizontalInset);
            float y = bottom ? Screen.height - Mathf.Max(EdgeMargin, verticalInset) - panelHeight :
                Mathf.Max(EdgeMargin, verticalInset);
            if (followCurrency)
            {
                // Align the panel's right edge with the native currency badge,
                // and leave a configurable pixel gap directly below it.
                x = currencyBadge.Value.xMax - panelWidth;
                y = badgeBottom + gap;
            }
            // Apply physical-pixel bounds after scaling. This keeps all four
            // anchors visible even on non-16:9 and very small screens.
            x = Mathf.Clamp(x, EdgeMargin, Screen.width - panelWidth - EdgeMargin);
            y = Mathf.Clamp(y, EdgeMargin, Screen.height - panelHeight - EdgeMargin);

            Matrix4x4 matrix = GUI.matrix;
            Color guiColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(x, y, 0f),
                    Quaternion.identity, new Vector3(factor, factor, 1f));
                DrawBackground(height);
                GUI.Label(new Rect(13, 3, Width - 132, 26), "DAILY QUESTS", _heading);
                GUI.Label(new Rect(Width - 120, 3, 105, 26),
                    quests.Count == 0 ? "—" : remaining + "/" + quests.Count + " LEFT",
                    _rowCounter);

                if (empty)
                {
                    bool noSource = quests.Count == 0;
                    GUI.Label(new Rect(13, HeaderHeight + 5, Width - 26, 30),
                        noSource ? "QUEST PROGRESS NOT DETECTED" : "ALL QUESTS COMPLETED",
                        _emptyTitle);
                    return;
                }

                int row = 0;
                // Unfinished objectives always come first, in the order
                // selected by the game. Completed ones follow in Mark mode.
                for (int i = 0; i < quests.Count && row < MaxVisible; i++)
                {
                    QuestView q = quests[i];
                    if (q.Completed) continue;
                    DrawRow(q, HeaderHeight + 3f + row * ItemHeight);
                    row++;
                }
                if (completedMode == CompletedQuestMode.Mark)
                {
                    for (int i = 0; i < quests.Count && row < MaxVisible; i++)
                    {
                        QuestView q = quests[i];
                        if (!q.Completed) continue;
                        DrawRow(q, HeaderHeight + 3f + row * ItemHeight);
                        row++;
                    }
                }
                if (overflow > 0)
                {
                    float footerY = HeaderHeight + 3f + visible * ItemHeight;
                    GUI.Label(new Rect(13, footerY, Width - 26, FooterHeight - 2f),
                        "+ " + overflow + " MORE QUESTS", _subtle);
                }
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.color = guiColor;
            }
        }

        private void DrawBackground(float height)
        {
            // Native stepped silhouette and burgundy title bar.
            DrawRect(7, 0, Width - 14, height, MainFill);
            DrawRect(0, 7, Width, height - 14, MainFill);
            DrawRect(7, 2, Width - 14, HeaderHeight - 2, HeaderFill);
            DrawRect(2, 7, Width - 4, HeaderHeight - 7, HeaderFill);
            DrawFrame(Width, height);
            DrawRect(2, HeaderHeight - 1, Width - 4, 1, GoldFrame);
            DrawRect(10, HeaderHeight - 4, 46, 1, GoldText);
        }

        private void DrawRow(QuestView q, float y)
        {
            float labelX = q.Completed ? 33f : 16f;
            const float counterWidth = 57f;
            const float counterX = Width - counterWidth - 13f;
            float labelWidth = counterX - labelX - 5f;

            // Completed rows are explicitly check-marked (rather than only
            // tinted green). The tick is drawn from rectangles so it remains
            // visible even if the game's font has no Unicode check glyph.
            if (q.Completed)
                DrawTick(7f, y + 7f);
            else
                DrawRect(8, y + 6, 2, 17, GoldText);
            GUIStyle rowStyle = q.Completed ? _rowDone : _rowTitle;
            GUI.Label(new Rect(labelX, y + 1, labelWidth, 24f),
                FitText(q.Title, labelWidth, rowStyle, q.Completed), rowStyle);

            string counter = q.Progress.HasValue && q.Target.HasValue
                ? Math.Min(q.Progress.Value, q.Target.Value) + "/" + q.Target.Value
                : q.Completed ? "DONE" : "—";
            GUI.Label(new Rect(counterX, y + 1, counterWidth, 24f),
                counter, q.Completed ? _rowCounterDone : _rowCounter);

            DrawRect(11, y + ItemHeight - 2, Width - 22, 1, RowDivider);
        }

        private static void DrawTick(float x, float y)
        {
            DrawRect(x, y + 6, 3, 3, Success);
            DrawRect(x + 3, y + 9, 3, 3, Success);
            DrawRect(x + 6, y + 12, 3, 3, Success);
            DrawRect(x + 9, y + 9, 3, 3, Success);
            DrawRect(x + 12, y + 6, 3, 3, Success);
            DrawRect(x + 15, y + 3, 3, 3, Success);
            DrawRect(x + 18, y, 3, 3, Success);
        }

        private string FitText(string value, float availableWidth, GUIStyle style,
            bool completed)
        {
            if (string.IsNullOrEmpty(value)) return "Daily quest";
            // Completed rows have a narrower text area due to their tick icon.
            // Keep separate width-fit caches so newly completed titles cannot
            // reuse a longer label and overlap the counter.
            Dictionary<string, string> cache = completed
                ? _fittedCompletedLabels : _fittedLabels;
            if (cache.TryGetValue(value, out string cached))
                return cached;
            // Measure each distinct localized label once, not every frame.
            if (style.CalcSize(new GUIContent(value)).x <= availableWidth)
            {
                cache[value] = value;
                return value;
            }
            const string suffix = "…";
            int lo = 0, hi = value.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                string candidate = value.Substring(0, mid) + suffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= availableWidth)
                    lo = mid;
                else hi = mid - 1;
            }
            string fitted = value.Substring(0, lo) + suffix;
            cache[value] = fitted;
            return fitted;
        }

        private void InitializeStyles()
        {
            // Font discovery exactly once; the old HUD repeatedly searched
            // every Unity Font object when a native font wasn't available.
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
            if (_font == null)
            {
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (Exception) { }
            }

            _heading = MakeStyle(18, GoldText, TextAnchor.MiddleLeft);
            _rowTitle = MakeStyle(17, PrimaryText, TextAnchor.MiddleLeft);
            _rowDone = MakeStyle(17, Success, TextAnchor.MiddleLeft);
            _rowCounter = MakeStyle(16, GoldText, TextAnchor.MiddleRight);
            _rowCounterDone = MakeStyle(16, Success, TextAnchor.MiddleRight);
            _subtle = MakeStyle(13, SecondaryText, TextAnchor.MiddleCenter);
            _emptyTitle = MakeStyle(15, PrimaryText, TextAnchor.MiddleCenter);
        }

        private GUIStyle MakeStyle(int fontSize, Color color, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.font = _font;
            style.fontSize = fontSize;
            style.fontStyle = _font != null &&
                _font.name.StartsWith("Jersey10", StringComparison.OrdinalIgnoreCase)
                ? FontStyle.Normal : FontStyle.Bold;
            style.normal.textColor = color;
            style.alignment = alignment;
            style.wordWrap = false;
            style.clipping = TextClipping.Clip;
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static void DrawRect(float x, float y, float w, float h, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, Mathf.Max(0, w), Mathf.Max(0, h)),
                Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawFrame(float width, float height)
        {
            DrawRect(8, 0, width - 16, 2, GoldFrame);
            DrawRect(5, 2, 3, 2, GoldFrame);
            DrawRect(2, 5, 3, 2, GoldFrame);
            DrawRect(width - 8, 2, 3, 2, GoldFrame);
            DrawRect(width - 5, 5, 3, 2, GoldFrame);
            DrawRect(0, 7, 2, height - 14, GoldFrame);
            DrawRect(width - 2, 7, 2, height - 14, GoldFrame);
            DrawRect(8, height - 2, width - 16, 2, GoldFrame);
            DrawRect(5, height - 4, 3, 2, GoldFrame);
            DrawRect(2, height - 7, 3, 2, GoldFrame);
            DrawRect(width - 8, height - 4, 3, 2, GoldFrame);
            DrawRect(width - 5, height - 7, 3, 2, GoldFrame);
        }
    }
}
