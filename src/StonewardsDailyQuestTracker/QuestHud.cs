using System;
using System.Collections.Generic;
using UnityEngine;

namespace StonewardsDailyQuestTracker
{
    /// <summary>A purely visual overlay. Uses no raycast targets and never changes game input state.</summary>
    internal sealed class QuestHud
    {
        private const float Width = 304f;
        private const float HeaderHeight = 38f;
        private const float RowHeight = 57f;
        private static readonly Color Frame = new Color(.72f, .64f, .48f, 1);
        private static readonly Color Fill = new Color(19f / 255, 23f / 255, 32f / 255, .94f);
        private static readonly Color Header = new Color(76f / 255, 46f / 255, 48f / 255, 1);
        private static readonly Color Gold = new Color(.98f, .79f, .51f, 1);
        private static readonly Color Foreground = new Color(.98f, .90f, .76f, 1);
        private static readonly Color Dim = new Color(.68f, .68f, .67f, 1);
        private static readonly Color Green = new Color(.47f, .87f, .56f, 1);
        private static readonly Color Track = new Color(.16f, .18f, .20f, 1);
        private GUIStyle _heading;
        private GUIStyle _title;
        private GUIStyle _progress;
        private GUIStyle _hint;
        private Font _font;
        private float _fontRetry;

        public void Draw(List<QuestView> quests, PanelCorner anchor, int horizontalInset,
            int verticalInset, float scale, bool hideCompleted, bool showBars, bool showStatus)
        {
            if (_heading == null || (_font == null && Time.unscaledTime > _fontRetry))
                InitStyles();

            var visible = new List<QuestView>(quests.Count);
            for (int i = 0; i < quests.Count; i++)
                if (!hideCompleted || !quests[i].Completed)
                    visible.Add(quests[i]);
            if (visible.Count > 8)
                visible.RemoveRange(8, visible.Count - 8);
            bool showEmpty = visible.Count == 0;
            if (showEmpty && !showStatus && quests.Count == 0)
                return;

            float height = HeaderHeight + (showEmpty ? 42f : visible.Count * RowHeight) + 8f;
            float baseScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            float factor = Mathf.Clamp(baseScale * scale, .35f, 2.0f);
            factor = Mathf.Min(factor, (Screen.width - 12f) / Width, (Screen.height - 12f) / height);
            factor = Mathf.Max(.1f, factor);
            float sw = Width * factor, sh = height * factor;
            float x = anchor == PanelCorner.TopRight || anchor == PanelCorner.BottomRight
                ? Screen.width - Mathf.Max(6, horizontalInset) - sw
                : Mathf.Max(6, horizontalInset);
            float y = anchor == PanelCorner.BottomRight || anchor == PanelCorner.BottomLeft
                ? Screen.height - Mathf.Max(6, verticalInset) - sh
                : Mathf.Max(6, verticalInset);
            x = Mathf.Clamp(x, 6, Mathf.Max(6, Screen.width - sw - 6));
            y = Mathf.Clamp(y, 6, Mathf.Max(6, Screen.height - sh - 6));

            Matrix4x4 oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.identity,
                    new Vector3(factor, factor, 1));
                Rect(7, 0, Width - 14, height, Fill);
                Rect(0, 7, Width, height - 14, Fill);
                Rect(7, 2, Width - 14, HeaderHeight - 2, Header);
                Rect(2, 7, Width - 4, HeaderHeight - 7, Header);
                Border(Width, height);
                Rect(2, HeaderHeight - 1, Width - 4, 1, Frame);
                GUI.Label(new Rect(14, 4, 230, 31), "DAILY QUESTS", _heading);
                GUI.Label(new Rect(252, 7, 36, 24), quests.Count.ToString(), _progress);

                if (showEmpty)
                {
                    GUI.Label(new Rect(15, HeaderHeight + 6, Width - 30, 30),
                        quests.Count == 0 ? "QUEST DATA NOT DETECTED  [F9]" : "ALL VISIBLE QUESTS COMPLETE", _hint);
                }
                for (int i = 0; i < visible.Count; i++)
                {
                    QuestView quest = visible[i];
                    float ry = HeaderHeight + 4 + i * RowHeight;
                    if (i != 0) Rect(12, ry - 4, Width - 24, 1, new Color(.71f, .63f, .48f, .30f));
                    Rect(10, ry + 6, 3, 32, quest.Completed ? Green : Gold);
                    GUI.Label(new Rect(20, ry, 209, 28), Shorten(quest.Title, 30), quest.Completed ? _hint : _title);
                    string value = quest.Progress.HasValue && quest.Target.HasValue && quest.Target.Value > 0
                        ? Math.Min(quest.Progress.Value, quest.Target.Value) + "/" + quest.Target.Value
                        : quest.Completed ? "DONE" : "—";
                    GUI.Label(new Rect(232, ry + 1, 58, 27), value, quest.Completed ? _hint : _progress);
                    if (showBars)
                    {
                        Rect(20, ry + 31, Width - 40, 7, Track);
                        float fraction = quest.Completed ? 1 : quest.Progress.HasValue && quest.Target.HasValue && quest.Target.Value > 0
                            ? Mathf.Clamp01((float)quest.Progress.Value / quest.Target.Value) : 0;
                        if (fraction > 0)
                            Rect(20, ry + 31, (Width - 40) * fraction, 7, quest.Completed ? Green : Gold);
                    }
                }
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; }
        }

        private void InitStyles()
        {
            if (_font == null)
            {
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                    if (f != null && (f.name == "Jersey10-Regular" || f.name == "Jersey10-Regular2"))
                    { _font = f; break; }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                    _fontRetry = Time.unscaledTime + 4f;
                }
            }
            _heading = Style(21, Gold, TextAnchor.MiddleLeft);
            _title = Style(17, Foreground, TextAnchor.MiddleLeft);
            _progress = Style(17, Gold, TextAnchor.MiddleRight);
            _hint = Style(14, Dim, TextAnchor.MiddleLeft);
        }

        private GUIStyle Style(int size, Color color, TextAnchor align)
        {
            var s = new GUIStyle(GUI.skin.label);
            s.font = _font;
            s.fontSize = size;
            s.fontStyle = _font != null && _font.name.StartsWith("Jersey10", StringComparison.OrdinalIgnoreCase)
                ? FontStyle.Normal : FontStyle.Bold;
            s.normal.textColor = color;
            s.alignment = align;
            s.clipping = TextClipping.Clip;
            s.wordWrap = false;
            s.padding = new RectOffset(0, 0, 0, 0);
            return s;
        }

        private static string Shorten(string s, int max) => string.IsNullOrEmpty(s) ? "DAILY QUEST" : s.Length > max ? s.Substring(0, max - 1) + "…" : s;
        private static void Rect(float x, float y, float w, float h, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, Mathf.Max(0, w), Mathf.Max(0, h)), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        private static void Border(float width, float height)
        {
            Rect(8, 0, width - 16, 2, Frame);
            Rect(5, 2, 3, 2, Frame); Rect(2, 5, 3, 2, Frame);
            Rect(width - 8, 2, 3, 2, Frame); Rect(width - 5, 5, 3, 2, Frame);
            Rect(0, 7, 2, height - 14, Frame); Rect(width - 2, 7, 2, height - 14, Frame);
            Rect(8, height - 2, width - 16, 2, Frame);
            Rect(5, height - 4, 3, 2, Frame); Rect(2, height - 7, 3, 2, Frame);
            Rect(width - 8, height - 4, 3, 2, Frame); Rect(width - 5, height - 7, 3, 2, Frame);
        }
    }
}
