using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Places our IMGUI quest HUD in the same native UI hierarchy as the
    /// game's QuestNotificationHolder, directly BEFORE that notification layer.
    /// This guarantees the game notification subtree is rendered on top without
    /// moving game elements, modifying its quest logic or hiding the whole HUD.
    /// Legacy OnGUI is used when the native container cannot repaint.
    /// </summary>
    internal sealed class NativeHudLayer
    {
        private const string NotificationHolderName = "QuestNotificationHolder";
        private const float MinViewportCoverage = 0.92f;
        private const float RetrySeconds = 5f;

        private readonly Action _draw;
        private readonly ManualLogSource _log;
        private VisualElement _panelRoot;
        private VisualElement _parent;
        private VisualElement _notificationLayer;
        private IMGUIContainer _container;
        private float _nextSearch;
        private float _lastRepaintTime = -1f;
        private float _attachedTime;
        private bool _reportedUnavailable;

        public NativeHudLayer(Action draw, ManualLogSource log)
        {
            _draw = draw;
            _log = log;
        }

        public bool Attached =>
            _container != null && _parent != null && _panelRoot != null &&
            _container.parent == _parent &&
            _notificationLayer != null && _notificationLayer.parent == _parent &&
            _container.panel != null && _container.panel == _panelRoot.panel;

        public bool HasRecentRepaint =>
            Attached && _lastRepaintTime >= _attachedTime &&
            Time.unscaledTime - _lastRepaintTime < .75f;

        // GUI.matrix inside a UI Toolkit IMGUIContainer uses panel points,
        // whereas QuestHud calculates global physical screen pixels.
        public float UnitsPerPixelX =>
            Attached && Screen.width > 0 && _panelRoot.worldBound.width > 0f
                ? _panelRoot.worldBound.width / Screen.width : 1f;

        public float UnitsPerPixelY =>
            Attached && Screen.height > 0 && _panelRoot.worldBound.height > 0f
                ? _panelRoot.worldBound.height / Screen.height : 1f;

        // Offsets compensate when the notification layer's nearest full-screen
        // parent doesn't begin at the panel's own coordinate origin.
        public float OriginScreenX =>
            Attached && _container.worldBound.width > 0f
                ? (_container.worldBound.xMin - _panelRoot.worldBound.xMin) /
                  UnitsPerPixelX : 0f;

        public float OriginScreenY =>
            Attached && _container.worldBound.height > 0f
                ? (_container.worldBound.yMin - _panelRoot.worldBound.yMin) /
                  UnitsPerPixelY : 0f;

        public void Attach(VisualElement panelRoot)
        {
            if (panelRoot == null || panelRoot.panel == null)
            {
                Detach();
                return;
            }

            if (Attached && ReferenceEquals(_panelRoot, panelRoot))
                return;

            if (_container != null)
                Detach();

            if (Time.unscaledTime < _nextSearch)
                return;

            try
            {
                // The diagnostic captured the actual game node:
                // [UI] -> QuestNotificationHolder -> QuestNotificationParent
                // -> UINotification (e.g. "Player Joined" / "Quest completed").
                VisualElement holder = panelRoot.Q<VisualElement>(NotificationHolderName);
                if (holder == null || holder.panel != panelRoot.panel)
                {
                    Retry("QuestNotificationHolder not found yet.");
                    return;
                }

                // Prefer a direct sibling of QuestNotificationHolder. If its
                // parent is not a full-screen layer, climb just far enough to
                // find a screen-sized parent; preserve exact sibling ordering.
                Rect viewport = panelRoot.worldBound;
                VisualElement notificationBranch = holder;
                VisualElement parent = notificationBranch.parent;
                while (parent != null && !CoversViewport(parent.worldBound, viewport))
                {
                    notificationBranch = parent;
                    parent = notificationBranch.parent;
                }
                if (parent == null)
                {
                    Retry("Notification hierarchy has no full-screen parent.");
                    return;
                }

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
                container.style.top = 0;
                container.style.right = 0;
                container.style.bottom = 0;
                container.style.overflow = Overflow.Hidden;

                // In UI Toolkit, later siblings render over earlier siblings.
                // Our panel is now the immediately preceding sibling, not an
                // unrelated overlay appended above every element in the game.
                parent.Add(container);
                container.PlaceBehind(notificationBranch);

                _panelRoot = panelRoot;
                _parent = parent;
                _notificationLayer = notificationBranch;
                _container = container;
                _attachedTime = Time.unscaledTime;
                _lastRepaintTime = -1f;
                _reportedUnavailable = false;
                _log.LogInfo("Quest HUD placed directly behind game notification branch '" +
                    notificationBranch.name + "' beneath '" + parent.name + "'.");
            }
            catch (Exception ex)
            {
                Detach();
                Retry("Native HUD placement failed: " + ex.Message);
            }
        }

        private void Retry(string reason)
        {
            _nextSearch = Time.unscaledTime + RetrySeconds;
            if (_reportedUnavailable) return;
            _reportedUnavailable = true;
            _log.LogWarning(reason + " Using legacy HUD until native notifications are available.");
        }

        private static bool CoversViewport(Rect candidate, Rect viewport)
        {
            return candidate.width >= viewport.width * MinViewportCoverage &&
                   candidate.height >= viewport.height * MinViewportCoverage &&
                   candidate.xMin <= viewport.xMin + viewport.width * .04f &&
                   candidate.yMin <= viewport.yMin + viewport.height * .04f &&
                   candidate.xMax >= viewport.xMax - viewport.width * .04f &&
                   candidate.yMax >= viewport.yMax - viewport.height * .04f;
        }

        public void Detach()
        {
            if (_container != null)
            {
                try { _container.RemoveFromHierarchy(); }
                catch (Exception) { }
            }
            _container = null;
            _panelRoot = null;
            _parent = null;
            _notificationLayer = null;
            _lastRepaintTime = -1f;
        }
    }
}
