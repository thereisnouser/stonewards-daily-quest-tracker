using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Renders the existing IMGUI-based HUD inside the game's UI Toolkit panel.
    /// Stays above the game's HUD background while yielding to identifiable
    /// notification layers. Falls back to OnGUI if the container cannot repaint.
    /// </summary>
    internal sealed class NativeHudLayer
    {
        private readonly Action _draw;
        private readonly ManualLogSource _log;
        private VisualElement _root;
        private IMGUIContainer _container;
        private bool _loggedAttachmentFailure;
        private float _lastRepaintTime = -1f;
        private float _attachedTime;

        public NativeHudLayer(Action draw, ManualLogSource log)
        {
            _draw = draw;
            _log = log;
        }

        public bool Attached =>
            _root != null && _container != null &&
            _container.parent == _root && _container.panel != null;

        public bool HasRecentRepaint =>
            Attached && _lastRepaintTime >= _attachedTime &&
            Time.unscaledTime - _lastRepaintTime < 0.75f;

        public float UnitsPerPixelX =>
            Attached && Screen.width > 0 && _root.worldBound.width > 0f
                ? _root.worldBound.width / Screen.width : 1f;

        public float UnitsPerPixelY =>
            Attached && Screen.height > 0 && _root.worldBound.height > 0f
                ? _root.worldBound.height / Screen.height : 1f;

        public void Attach(VisualElement root)
        {
            if (root == null || root.panel == null)
            {
                Detach();
                return;
            }
            if (Attached && ReferenceEquals(_root, root))
                return;

            Detach();
            try
            {
                // Track actual Repaint events. A mounted IMGUIContainer is not
                // proof that Unity is painting it; the legacy HUD stays as a
                // fallback until the native surface has repainted.
                var container = new IMGUIContainer(() =>
                {
                    if (Event.current != null &&
                        Event.current.type == EventType.Repaint)
                        _lastRepaintTime = Time.unscaledTime;
                    _draw();
                });
                container.name = "StonewardsDailyQuestTracker";
                container.pickingMode = PickingMode.Ignore;
                container.focusable = false;
                container.style.position = Position.Absolute;
                container.style.left = 0;
                container.style.right = 0;
                container.style.top = 0;
                container.style.bottom = 0;
                container.style.overflow = Overflow.Hidden;

                // SendToBack() previously hid the tracker behind the game's
                // fullscreen HUD layers. Newly added siblings render above
                // older ones. Keep the quest panel in front of the background,
                // but immediately behind an identifiable notification layer.
                root.Add(container);
                VisualElement notifications = FindNotificationSibling(root, container);
                if (notifications != null)
                    container.PlaceBehind(notifications);

                _root = root;
                _container = container;
                _attachedTime = Time.unscaledTime;
                _lastRepaintTime = -1f;
                _loggedAttachmentFailure = false;
                _log.LogInfo(notifications != null
                    ? "Daily quest HUD placed beneath the native notification layer."
                    : "Daily quest HUD attached in front of base HUD layers. " +
                      "Waiting for native repaint.");
            }
            catch (Exception ex)
            {
                Detach();
                if (!_loggedAttachmentFailure)
                {
                    _loggedAttachmentFailure = true;
                    _log.LogWarning("Native HUD layering unavailable. " +
                        "Using legacy OnGUI fallback: " + ex.Message);
                }
            }
        }

        // Keep the scan strictly to direct siblings; descending into the
        // gameplay UI and moving its ancestor would hide the quest panel again.
        private static VisualElement FindNotificationSibling(
            VisualElement root, VisualElement ownContainer)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                VisualElement candidate = root.ElementAt(i);
                if (candidate == ownContainer) continue;
                if (HasNotificationName(candidate.name))
                    return candidate;
                foreach (string className in candidate.GetClasses())
                    if (HasNotificationName(className))
                        return candidate;
            }
            return null;
        }

        private static bool HasNotificationName(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf("notification", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("toast", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Detach()
        {
            if (_container != null)
            {
                try { _container.RemoveFromHierarchy(); }
                catch (Exception) { }
            }
            _container = null;
            _root = null;
            _lastRepaintTime = -1f;
        }
    }
}
