using System;
using System.Collections.Generic;
using UnityEngine;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Read-only, non-interactive in-game HUD. Stonewards/Party HUD colors,
    /// stepped borders, generous typography and room for only active quests.
    /// No runtime font searches or UI-object creation on each repaint.
    /// </summary>
    internal sealed class QuestHud
    {
        private const float Width = 338f;
        private const float HeaderHeight = 44f;
        private const float ItemHeight = 62f;
        private const float FooterHeight = 20f;
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
        private static readonly Color Track = new Color(.12f, .15f, .19f, 1f);
        private static readonly Color TrackBorder = new Color(.36f, .35f, .31f, .9f);

        private GUIStyle _heading;
        private GUIStyle _rowTitle;
        private GUIStyle _rowDone;
        private GUIStyle _rowCounter;
        private GUIStyle _rowCounterDone;
        private GUIStyle _subtle;
        private GUIStyle _emptyTitle;
        private GUIStyle _emptyInfo;
        private Font _font;

        public void Draw(List<QuestView> quests, PanelCorner anchor,
            int horizontalInset, int verticalInset, float requestedScale,
            float readabilityScale, bool autoScaleToResolution,
            bool hideCompleted, bool showBars, bool showStatus)
        {
            if (_heading == null) InitializeStyles();

            // This is only a tiny cached list of live quest states, never an
            // enumeration of the game's database assets.
            int matching = 0;
            for (int i = 0; i < quests.Count; i++)
                if (!hideCompleted || !quests[i].Completed)
                    matching++;

            bool empty = matching == 0;
            if (empty && quests.Count == 0 && !showStatus) return;
            int visible = Math.Min(matching, MaxVisible);
            int overflow = Math.Max(0, matching - visible);
            float height = HeaderHeight + (empty ? 72f : visible * ItemHeight + 6f) +
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

            if (autoScaleToResolution)
            {
                // Respect the requested top/bottom reserved space when it fits.
                // For an impossible offset (e.g. 1000 px on a 720 px screen),
                // let the later position clamp win instead of collapsing the HUD.
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
                GUI.Label(new Rect(16, 5, Width - 128, 34), "DAILY QUESTS", _heading);
                GUI.Label(new Rect(Width - 103, 7, 85, 30),
                    empty ? "TRACKER" : matching + " QUESTS", _rowCounter);

                if (empty)
                {
                    bool noSource = quests.Count == 0;
                    GUI.Label(new Rect(18, HeaderHeight + 11, Width - 36, 28),
                        noSource ? "QUEST PROGRESS NOT DETECTED" : "ALL QUESTS COMPLETED",
                        _emptyTitle);
                    GUI.Label(new Rect(18, HeaderHeight + 40, Width - 36, 22),
                        noSource ? "Open Missions / Daily for diagnostics" :
                            "Completed quests are hidden", _emptyInfo);
                    return;
                }

                int row = 0;
                for (int i = 0; i < quests.Count && row < MaxVisible; i++)
                {
                    QuestView q = quests[i];
                    if (hideCompleted && q.Completed) continue;
                    DrawRow(q, HeaderHeight + 4f + row * ItemHeight, showBars);
                    row++;
                }
                if (overflow > 0)
                {
                    float footerY = HeaderHeight + 5f + visible * ItemHeight;
                    DrawRect(15, footerY, Width - 30, 1, RowDivider);
                    GUI.Label(new Rect(18, footerY + 2, Width - 36, 16),
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
            DrawRect(10, HeaderHeight - 5, 65, 2, GoldText);
        }

        private void DrawRow(QuestView q, float y, bool showBar)
        {
            DrawRect(13, y + 7, 3, 35, q.Completed ? Success : GoldText);
            GUI.Label(new Rect(24, y + 1, Width - 122, 28),
                Shorten(q.Title, 37), q.Completed ? _rowDone : _rowTitle);

            string counter = q.Completed ? "DONE" : q.Progress.HasValue && q.Target.HasValue
                ? Math.Min(q.Progress.Value, q.Target.Value) + "/" + q.Target.Value : "—";
            GUI.Label(new Rect(Width - 104, y + 1, 89, 28),
                counter, q.Completed ? _rowCounterDone : _rowCounter);

            if (showBar)
            {
                const float barWidth = Width - 49f;
                DrawRect(24, y + 35, barWidth, 9, TrackBorder);
                DrawRect(25, y + 36, barWidth - 2f, 7, Track);
                float percent = q.Completed ? 1f :
                    q.Progress.HasValue && q.Target.HasValue && q.Target.Value > 0
                        ? Mathf.Clamp01((float)q.Progress.Value / q.Target.Value) : 0f;
                if (percent > 0f)
                    DrawRect(25, y + 36, (barWidth - 2f) * percent, 7,
                        q.Completed ? Success : GoldText);
            }
            DrawRect(14, y + ItemHeight - 4f, Width - 28, 1, RowDivider);
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

            _heading = MakeStyle(22, GoldText, TextAnchor.MiddleLeft);
            _rowTitle = MakeStyle(19, PrimaryText, TextAnchor.MiddleLeft);
            _rowDone = MakeStyle(19, Success, TextAnchor.MiddleLeft);
            _rowCounter = MakeStyle(17, GoldText, TextAnchor.MiddleRight);
            _rowCounterDone = MakeStyle(17, Success, TextAnchor.MiddleRight);
            _subtle = MakeStyle(14, SecondaryText, TextAnchor.MiddleCenter);
            _emptyTitle = MakeStyle(18, PrimaryText, TextAnchor.MiddleCenter);
            _emptyInfo = MakeStyle(15, SecondaryText, TextAnchor.MiddleCenter);
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

        private static string Shorten(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return "Daily quest";
            return text.Length > count ? text.Substring(0, count - 1) + "…" : text;
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
