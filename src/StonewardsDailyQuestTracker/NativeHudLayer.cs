using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Renders the existing IMGUI-based HUD inside the game's UI Toolkit panel.
    /// As the first root child, it stays underneath the game's native overlays
    /// (notifications, currency, etc.) without modifying their hierarchy.
    /// </summary>
    internal sealed class NativeHudLayer
    {
        private readonly Action _draw;
        private readonly ManualLogSource _log;
        private VisualElement _root;
        private IMGUIContainer _container;
        private bool _loggedAttachmentFailure;

        public NativeHudLayer(Action draw, ManualLogSource log)
        {
            _draw = draw;
            _log = log;
        }

        public bool Attached =>
            _root != null && _container != null &&
            _container.parent == _root && _container.panel != null;

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
                var container = new IMGUIContainer(_draw);
                container.name = "StonewardsDailyQuestTracker";
                container.pickingMode = PickingMode.Ignore;
                container.focusable = false;
                container.style.position = Position.Absolute;
                container.style.left = 0;
                container.style.right = 0;
                container.style.top = 0;
                container.style.bottom = 0;
                container.style.overflow = Overflow.Hidden;

                // Unlike MonoBehaviour.OnGUI, a UI Toolkit child participates
                // in the native panel's draw order. Other UI remains above it.
                root.Add(container);
                container.SendToBack();
                _root = root;
                _container = container;
                _loggedAttachmentFailure = false;
                _log.LogInfo("Daily quest HUD attached behind native UI overlays.");
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

        public void Detach()
        {
            if (_container != null)
            {
                try { _container.RemoveFromHierarchy(); }
                catch (Exception) { }
            }
            _container = null;
            _root = null;
        }
    }
}
