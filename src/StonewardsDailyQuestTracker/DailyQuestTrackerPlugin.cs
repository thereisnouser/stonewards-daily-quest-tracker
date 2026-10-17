using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace StonewardsDailyQuestTracker
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class DailyQuestTrackerPlugin : BaseUnityPlugin
    {
        public const string Guid = "community.stonewards.dailyquesttracker";
        public const string Name = "Stonewards Daily Quest Tracker";
        public const string Version = "0.1.3";

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<CompletedQuestMode> _completedQuestMode;
        private ConfigEntry<bool> _showMissingDataNotice;
        private ConfigEntry<PanelCorner> _corner;
        private ConfigEntry<int> _xOffset;
        private ConfigEntry<int> _yOffset;
        private ConfigEntry<float> _scale;
        private ConfigEntry<float> _readabilityScale;
        private ConfigEntry<bool> _autoScaleToResolution;
        private ConfigEntry<bool> _anchorToCurrencyHud;
        private ConfigEntry<bool> _renderBehindGameUi;
        private ConfigEntry<int> _currencyGap;
        private ConfigEntry<float> _refresh;
        private ConfigEntry<Key> _toggleKey;
        private ConfigEntry<Key> _diagnosticKey;
        private DailyQuestReader _reader;
        private QuestHud _hud;
        private CurrencyHudAnchor _currencyAnchor;
        private NativeHudLayer _nativeHudLayer;
        private List<QuestView> _quests = new List<QuestView>();
        private float _nextUpdate;
        private float _nextError;
        private bool _visible = true;
        private bool _wasConnected;
        private float _lastDiagnosticTime = -100f;
        private float _statusUntil;
        private string _diagnosticStatus = "";

        private static readonly FieldInfo InventoryRoot =
            typeof(InventoryScreen).GetField("inventoryScreen", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LoadingInstance =
            typeof(LoadingScreen).GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo LoadingElement =
            typeof(LoadingScreen).GetField("_loadingScreenElement", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Enable Daily Quest Tracker.");
            _completedQuestMode = Config.Bind("General", "CompletedQuestMode",
                CompletedQuestMode.Hide,
                "Hide completed daily quests (Hide), or keep them at the bottom " +
                "with a green checkmark (Mark). The old HideCompleted setting is ignored.");
            _showMissingDataNotice = Config.Bind("General", "ShowMissingQuestData", true,
                "Show a compact warning if the game has not initialized active daily quests.");
            _corner = Config.Bind("Appearance", "Anchor", PanelCorner.TopRight, "Panel screen corner.");
            _xOffset = Config.Bind("Appearance", "HorizontalOffset", 18, "Horizontal edge inset in actual pixels.");
            _yOffset = Config.Bind("Appearance", "VerticalOffset", 240,
                "Vertical edge inset in actual pixels. Top-right default avoids the gold and Bell Control panels.");
            _scale = Config.Bind("Appearance", "Scale", 1.0f,
                new ConfigDescription("Manual HUD size multiplier (0.65 - 1.8).",
                    new AcceptableValueRange<float>(0.65f, 1.8f)));
            _readabilityScale = Config.Bind("Appearance", "ReadabilityScale", 1.12f,
                new ConfigDescription("Extra HUD enlargement, like PartyHUD (1.0 - 1.4).",
                    new AcceptableValueRange<float>(1f, 1.4f)));
            _autoScaleToResolution = Config.Bind("Appearance", "AutoScaleToResolution", true,
                "Maintain the same HUD-to-screen proportions using 1920x1080 as a reference. " +
                "Disable for a fixed physical-pixel panel size.");
            _anchorToCurrencyHud = Config.Bind("Appearance", "AnchorToCurrencyHud", true,
                "When Anchor is TopRight, place the panel below the live coin HUD position. " +
                "Falls back to manual offsets if the native currency badge is not found.");
            _currencyGap = Config.Bind("Appearance", "CurrencyGap", 10,
                new ConfigDescription("Gap below the coin HUD in physical screen pixels.",
                    new AcceptableValueRange<int>(0, 50)));
            _renderBehindGameUi = Config.Bind("Appearance", "RenderBehindGameUI", true,
                "Draw the quest HUD inside the game's native UI Toolkit panel, beneath notifications. " +
                "Set false to restore the original OnGUI rendering if needed.");
            _refresh = Config.Bind("General", "RefreshSeconds", 1.25f,
                new ConfigDescription("How often to read actual quest progress (unscaled seconds).",
                    new AcceptableValueRange<float>(0.3f, 5f)));
            _toggleKey = Config.Bind("Hotkeys", "TogglePanel", Key.F6, "Toggle quest panel.");
            _diagnosticKey = Config.Bind("Hotkeys", "DumpQuestDiagnostics", Key.F9,
                "Write quest-system field names to BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt.");

            _reader = new DailyQuestReader(Logger);
            _hud = new QuestHud();
            _currencyAnchor = new CurrencyHudAnchor(Logger);
            _nativeHudLayer = new NativeHudLayer(Logger);
            Logger.LogInfo("Daily Quest Tracker v0.1.3 loaded. Active missions: QuestManager runtime state. F6: panel, F9: diagnostic.");
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_nativeHudLayer != null) _nativeHudLayer.Detach();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_reader != null) _reader.ResetSession();
            if (_currencyAnchor != null) _currencyAnchor.Reset();
            if (_nativeHudLayer != null) _nativeHudLayer.Detach();
            _quests.Clear();
            _nextUpdate = 0f;
        }

        private void TryExportDiagnostics(string trigger)
        {
            if (Time.unscaledTime - _lastDiagnosticTime < .35f) return;
            _lastDiagnosticTime = Time.unscaledTime;
            try
            {
                string path = QuestDiagnostics.Export(trigger);
                _diagnosticStatus = "QUEST DIAGNOSTICS SAVED - BepInEx/config";
                Logger.LogInfo("Quest diagnostics written to: " + path);
            }
            catch (Exception ex)
            {
                _diagnosticStatus = "QUEST DIAGNOSTICS FAILED - CHECK LOG";
                Logger.LogError("Quest diagnostic export failed: " + ex);
            }
            _statusUntil = Time.unscaledTime + 6f;
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
                        TryExportDiagnostics("F9 hotkey");
                }

                bool connected = NetworkClient.active;
                if (!connected)
                {
                    if (_wasConnected)
                    {
                        _reader.ResetSession();
                        _currencyAnchor.Reset();
                        _quests.Clear();
                    }
                    _wasConnected = false;
                    _nativeHudLayer.Detach();
                    return;
                }
                if (!_wasConnected)
                {
                    _reader.ResetSession();
                    _currencyAnchor.Reset();
                    _wasConnected = true;
                    _nextUpdate = 0f;
                }
                // The layer needs the game's UI Toolkit panel. Currency
                // discovery is also the bounded way to find that panel.
                if ((_anchorToCurrencyHud.Value && _corner.Value == PanelCorner.TopRight) ||
                    _renderBehindGameUi.Value)
                    _currencyAnchor.RefreshIfNeeded();

                VisualElement panelRoot;
                if (_renderBehindGameUi.Value &&
                    _currencyAnchor.TryGetPanelRoot(out panelRoot))
                    _nativeHudLayer.Attach(panelRoot);
                else
                    _nativeHudLayer.Detach();

                if (Time.unscaledTime >= _nextUpdate)
                {
                    _nextUpdate = Time.unscaledTime + _refresh.Value;
                    // Cheap, direct reads of only selected DAILY runtime missions.
                    _quests = _reader.ReadDailyQuests();
                }

                if (_nativeHudLayer.Attached)
                {
                    Rect badge;
                    Rect? currencyBounds =
                        _anchorToCurrencyHud.Value &&
                        _corner.Value == PanelCorner.TopRight &&
                        _currencyAnchor.TryGetBadgeBounds(out badge)
                            ? (Rect?)badge : null;

                    _nativeHudLayer.Sync(_quests,
                        _enabled.Value && _visible && !IsBlocked(),
                        _corner.Value, _xOffset.Value, _yOffset.Value,
                        _scale.Value, _readabilityScale.Value,
                        _autoScaleToResolution.Value, _completedQuestMode.Value,
                        _showMissingDataNotice.Value, currencyBounds,
                        _currencyGap.Value, _reader.TotalDailyQuests,
                        _reader.HasSeenDailyQuests);
                }
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
            if (Event.current == null) return;
            // F9 fallback if the Input System key event is swallowed by UI.
            if (Event.current.type == EventType.KeyDown &&
                _diagnosticKey.Value == Key.F9 &&
                Event.current.keyCode == KeyCode.F9)
                TryExportDiagnostics("F9 IMGUI fallback");

            if (Event.current.type != EventType.Repaint) return;
            if (Time.unscaledTime < _statusUntil)
            {
                var previous = GUI.color;
                GUI.color = new Color(.08f, .10f, .14f, .93f);
                GUI.DrawTexture(new Rect(18, Screen.height - 72, 448, 44),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(29, Screen.height - 67, 423, 35), _diagnosticStatus);
                GUI.color = previous;
            }

            // Native VisualElements and IMGUI must never draw the same panel:
            // the fallback OnGUI layer would cover game notifications.
            if (!_nativeHudLayer.IsDrawing)
                DrawHud(1f, 1f);
        }

        private void DrawHud(float unitsPerPixelX, float unitsPerPixelY,
            float originScreenX = 0f, float originScreenY = 0f)
        {
            if (!_enabled.Value || !_visible || !NetworkClient.active || IsBlocked())
                return;
            if (_quests.Count == 0 && !_showMissingDataNotice.Value &&
                !_reader.HasSeenDailyQuests)
                return;

            Rect badge;
            Rect? currencyBounds =
                _anchorToCurrencyHud.Value && _corner.Value == PanelCorner.TopRight &&
                _currencyAnchor.TryGetBadgeBounds(out badge) ? (Rect?)badge : null;
            _hud.Draw(_quests, _corner.Value, _xOffset.Value, _yOffset.Value,
                _scale.Value, _readabilityScale.Value, _autoScaleToResolution.Value,
                _completedQuestMode.Value, _showMissingDataNotice.Value,
                currencyBounds, _currencyGap.Value,
                _reader.TotalDailyQuests, _reader.HasSeenDailyQuests,
                unitsPerPixelX, unitsPerPixelY,
                originScreenX, originScreenY);
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
    internal enum CompletedQuestMode { Hide, Mark }
    internal sealed class QuestView
    {
        public string Id;
        public string Title;
        public int? Progress;
        public int? Target;
        public bool Completed;
    }
}
