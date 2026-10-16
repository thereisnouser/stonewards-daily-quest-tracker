using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Locates the on-screen currency badge in the active UI Toolkit HUD.
    /// Discovery is throttled and never runs during OnGUI. Once discovered,
    /// the badge's live worldBound is used without repeatedly enumerating UI.
    /// If the game's currency UI isn't recognizable, the caller uses the
    /// configured screen-edge offsets instead.
    /// </summary>
    internal sealed class CurrencyHudAnchor
    {
        private const float RetrySeconds = 8f;
        private const int MaxAttemptsPerScene = 2;
        private const int MaxVisitedElements = 3500;
        private readonly ManualLogSource _log;
        private TextElement _currencyLabel;
        private VisualElement _badge;
        private float _nextProbe;
        private int _lastWidth;
        private int _lastHeight;
        private bool _loggedFailure;
        private int _attemptCount;

        public CurrencyHudAnchor(ManualLogSource log)
        {
            _log = log;
        }

        public void Reset()
        {
            _currencyLabel = null;
            _badge = null;
            _nextProbe = 0f;
            _lastWidth = 0;
            _lastHeight = 0;
            _loggedFailure = false;
            _attemptCount = 0;
        }

        public void RefreshIfNeeded()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                // The same UIDocument can be rescaled in place. Its worldBound
                // will be read live; re-probe if its UI tree was rebuilt.
                _lastWidth = Screen.width;
                _lastHeight = Screen.height;
                if (_currencyLabel == null || _currencyLabel.panel == null)
                    _nextProbe = 0f;
            }

            if (_currencyLabel != null && _currencyLabel.panel != null)
                return;

            // Do not repeat costly UI discovery forever when the game's HUD
            // implementation differs. Two attempts per scene are sufficient
            // for the UI to initialize; after that manual offsets are used.
            _currencyLabel = null;
            _badge = null;
            if (_attemptCount >= MaxAttemptsPerScene ||
                Time.unscaledTime < _nextProbe)
                return;
            _nextProbe = Time.unscaledTime + RetrySeconds;
            _attemptCount++;
            Discover();
        }

        public bool TryGetBadgeBounds(out Rect bounds)
        {
            bounds = default(Rect);
            if (_currencyLabel == null || _currencyLabel.panel == null)
                return false;

            try
            {
                if (!IsVisible(_currencyLabel))
                    return false;
                VisualElement badge = _badge != null && _badge.panel == _currencyLabel.panel
                    ? _badge : _currencyLabel;
                return TryScreenBounds(badge, out bounds) &&
                       bounds.width > 1f && bounds.height > 1f;
            }
            catch (Exception)
            {
                _currencyLabel = null;
                _badge = null;
                return false;
            }
        }

        private void Discover()
        {
            try
            {
                UIDocument[] documents = Resources.FindObjectsOfTypeAll<UIDocument>();
                int remaining = MaxVisitedElements;
                float bestScore = float.NegativeInfinity;
                TextElement bestLabel = null;
                VisualElement bestBadge = null;

                foreach (UIDocument document in documents)
                {
                    if (remaining <= 0) break;
                    if (document == null || !document.gameObject.scene.IsValid() ||
                        !document.gameObject.activeInHierarchy) continue;
                    VisualElement root = document.rootVisualElement;
                    if (root == null || root.panel == null) continue;

                    var stack = new Stack<VisualElement>();
                    stack.Push(root);
                    while (stack.Count != 0 && remaining-- > 0)
                    {
                        VisualElement element = stack.Pop();
                        var label = element as TextElement;
                        if (label != null && HasCurrencyNumber(label.text) && IsVisible(label))
                        {
                            Rect rect;
                            if (TryScreenBounds(label, out rect) &&
                                rect.xMin > Screen.width * .63f &&
                                rect.yMin >= 0f && rect.yMax < Screen.height * .23f)
                            {
                                float score = rect.xMax / Screen.width * 2f -
                                              rect.yMin / Screen.height;
                                if (CurrencyKeywordNearby(label)) score += 4f;
                                if (label.text.Trim().Length >= 4) score += .2f;

                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestLabel = label;
                                    bestBadge = FindCompactBadge(label, rect);
                                }
                            }
                        }

                        for (int i = element.childCount - 1; i >= 0; i--)
                            stack.Push(element.ElementAt(i));
                    }
                }

                if (bestLabel != null)
                {
                    _currencyLabel = bestLabel;
                    _badge = bestBadge ?? bestLabel;
                    _loggedFailure = false;
                    _log.LogInfo("Currency HUD label located. Daily quest panel will follow " +
                        "the badge's live screen position.");
                }
                else if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    _log.LogWarning("Currency HUD label not located. Using manual " +
                        "top-right offsets until the native currency UI can be identified.");
                }
            }
            catch (Exception ex)
            {
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    _log.LogWarning("Currency HUD anchor discovery failed: " + ex.Message);
                }
            }
        }

        private static bool HasCurrencyNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            int digits = 0;
            foreach (char c in text)
            {
                if (c >= '0' && c <= '9') { digits++; continue; }
                if (char.IsWhiteSpace(c) || c == ',' || c == '.' || c == '\u00a0')
                    continue;
                return false;
            }
            return digits >= 2 && digits <= 9;
        }

        private static bool CurrencyKeywordNearby(VisualElement element)
        {
            int depth = 0;
            for (VisualElement current = element; current != null && depth++ < 4;
                 current = current.parent)
            {
                if (HasCurrencyKeyword(current.name))
                    return true;
                foreach (string className in current.GetClasses())
                    if (HasCurrencyKeyword(className)) return true;
            }
            return false;
        }

        private static bool HasCurrencyKeyword(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string name = value.ToLowerInvariant();
            return name.Contains("currency") || name.Contains("money") ||
                   name.Contains("coin") || name.Contains("gold") ||
                   name.Contains("metacurrency");
        }

        private static VisualElement FindCompactBadge(VisualElement label, Rect textBounds)
        {
            VisualElement best = label;
            int depth = 0;
            for (VisualElement parent = label.parent; parent != null && depth++ < 5;
                 parent = parent.parent)
            {
                Rect rect;
                if (!TryScreenBounds(parent, out rect))
                    break;
                // Avoid picking a whole-screen HUD root as the badge.
                if (rect.width > 390f || rect.height > 110f ||
                    rect.width < textBounds.width ||
                    rect.height < textBounds.height)
                    break;
                best = parent;
            }
            return best;
        }

        private static bool IsVisible(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None ||
                    current.resolvedStyle.visibility == UnityEngine.UIElements.Visibility.Hidden ||
                    current.resolvedStyle.opacity <= .01f)
                    return false;
            }
            return element.panel != null;
        }

        private static bool TryScreenBounds(VisualElement element, out Rect rect)
        {
            rect = default(Rect);
            if (element == null || element.panel == null) return false;
            Rect root = element.panel.visualTree.worldBound;
            Rect box = element.worldBound;
            if (root.width <= 0f || root.height <= 0f ||
                box.width <= 0f || box.height <= 0f) return false;

            // UI Toolkit worldBound uses panel points, whereas IMGUI positions
            // use screen pixels. Convert relative to the full runtime panel.
            float sx = Screen.width / root.width;
            float sy = Screen.height / root.height;
            rect = new Rect((box.xMin - root.xMin) * sx,
                            (box.yMin - root.yMin) * sy,
                            box.width * sx, box.height * sy);
            return rect.xMin >= -1f && rect.yMin >= -1f &&
                   rect.xMax <= Screen.width + 1f &&
                   rect.yMax <= Screen.height + 1f;
        }
    }
}
