using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class DailyQuestTrackerPlugin : BaseUnityPlugin
    {
        public const string Guid = "community.stonewards.dailyquesttracker";
        public const string Name = "Stonewards Daily Quest Tracker";
        public const string Version = "0.1.0";

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _hideCompleted;
        private ConfigEntry<bool> _showBars;
        private ConfigEntry<bool> _showMissingDataNotice;
        private ConfigEntry<PanelCorner> _corner;
        private ConfigEntry<int> _xOffset;
        private ConfigEntry<int> _yOffset;
        private ConfigEntry<float> _scale;
        private ConfigEntry<float> _refresh;
        private ConfigEntry<Key> _toggleKey;
        private ConfigEntry<Key> _diagnosticKey;
        private DailyQuestReader _reader;
        private QuestHud _hud;
        private List<QuestView> _quests = new List<QuestView>();
        private float _nextUpdate;
        private float _nextError;
        private bool _visible = true;

        private static readonly FieldInfo InventoryRoot =
            typeof(InventoryScreen).GetField("inventoryScreen", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LoadingInstance =
            typeof(LoadingScreen).GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo LoadingElement =
            typeof(LoadingScreen).GetField("_loadingScreenElement", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Enable Daily Quest Tracker.");
            _hideCompleted = Config.Bind("General", "HideCompleted", false, "Hide completed daily quests.");
            _showBars = Config.Bind("Appearance", "ShowProgressBars", true, "Draw a small progress bar below each objective.");
            _showMissingDataNotice = Config.Bind("General", "ShowDiscoveryStatus", false,
                "Show a status panel when the game quest API could not be recognized. The default is to show nothing.");
            _corner = Config.Bind("Appearance", "Anchor", PanelCorner.TopRight, "Panel screen corner.");
            _xOffset = Config.Bind("Appearance", "HorizontalOffset", 18, "Horizontal edge inset in actual pixels.");
            _yOffset = Config.Bind("Appearance", "VerticalOffset", 335,
                "Vertical edge inset in actual pixels. Top-right default avoids the gold and Bell Control panels.");
            _scale = Config.Bind("Appearance", "Scale", 1.0f,
                new ConfigDescription("Scale, relative to the 1920x1080 reference resolution.",
                    new AcceptableValueRange<float>(0.65f, 1.5f)));
            _refresh = Config.Bind("General", "RefreshSeconds", 0.8f,
                new ConfigDescription("How often to read actual quest progress (unscaled seconds).",
                    new AcceptableValueRange<float>(0.3f, 5f)));
            _toggleKey = Config.Bind("Hotkeys", "TogglePanel", Key.F6, "Toggle quest panel.");
            _diagnosticKey = Config.Bind("Hotkeys", "DumpQuestDiagnostics", Key.F9,
                "Write quest-system field names to BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt.");

            _reader = new DailyQuestReader(Logger);
            _hud = new QuestHud();
            Logger.LogInfo("Daily Quest Tracker loaded (F6: panel, F9: quest diagnostics). Progress is read only from game state.");
        }

        private void Update()
        {
            try
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard[_toggleKey.Value] != null && keyboard[_toggleKey.Value].wasPressedThisFrame)
                        _visible = !_visible;
                    if (keyboard[_diagnosticKey.Value] != null && keyboard[_diagnosticKey.Value].wasPressedThisFrame)
                    {
                        string path = _reader.DumpDiagnostics();
                        Logger.LogInfo("Quest diagnostics written: " + path);
                    }
                }

                if (Time.unscaledTime < _nextUpdate)
                    return;
                _nextUpdate = Time.unscaledTime + _refresh.Value;
                // Read even while a menu is open, so the tracker can pick up quest refreshes.
                _quests = _reader.ReadDailyQuests();
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextError)
                {
                    Logger.LogWarning("Quest update failed (will retry): " + ex);
                    _nextError = Time.unscaledTime + 15f;
                }
            }
        }

        private void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint ||
                !_enabled.Value || !_visible || !NetworkClient.active || IsBlocked())
                return;

            if (_quests.Count == 0 && !_showMissingDataNotice.Value)
                return;

            _hud.Draw(_quests, _corner.Value, _xOffset.Value, _yOffset.Value,
                _scale.Value, _hideCompleted.Value, _showBars.Value, _showMissingDataNotice.Value);
        }

        private static bool IsBlocked()
        {
            try
            {
                if (LoadingInstance != null && LoadingElement != null)
                {
                    var instance = LoadingInstance.GetValue(null);
                    var element = instance == null ? null : LoadingElement.GetValue(instance) as VisualElement;
                    if (element != null && element.panel != null)
                    {
                        bool visible = true;
                        for (VisualElement e = element; e != null; e = e.parent)
                            if (e.resolvedStyle.display == DisplayStyle.None ||
                                e.resolvedStyle.visibility == UnityEngine.UIElements.Visibility.Hidden ||
                                e.resolvedStyle.opacity <= .01f)
                                visible = false;
                        if (visible)
                            return true;
                    }
                }
                var shop = ShopMenu.Instance;
                if (shop != null && shop.IsOpen) return true;
                var selection = CharacterSelectionScreen.Instance;
                if (selection != null && selection.IsOpen) return true;
                var missionMenu = GlobalMissionMenu.Instance;
                if (missionMenu != null && missionMenu.IsOpen) return true;
                var upgrade = RogueLikeUpgradeMenu.Instance;
                if (upgrade != null && upgrade.IsOpen) return true;
                var inventory = InventoryScreen.Instance;
                if (inventory != null && InventoryRoot != null)
                {
                    var root = InventoryRoot.GetValue(inventory) as VisualElement;
                    if (root != null && root.panel != null && root.resolvedStyle.display != DisplayStyle.None)
                        return true;
                }
                var input = InputManager.Instance;
                return input != null && input.CurrentInputState != InputManager.InputState.Gameplay &&
                       input.CurrentInputState != InputManager.InputState.Dead;
            }
            catch { return false; }
        }
    }

    internal enum PanelCorner { TopRight, TopLeft, BottomRight, BottomLeft }
    internal sealed class QuestView
    {
        public string Id;
        public string Title;
        public int? Progress;
        public int? Target;
        public bool Completed;
    }
}
