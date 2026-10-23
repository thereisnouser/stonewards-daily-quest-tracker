using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Client-only mission pins. Identifies real QuestSO assets from the
    /// game's GlobalMissionButton entries and reads GLOBAL RuntimeMissions.
    /// Never changes game missions or invokes reward/quest mutation methods.
    /// </summary>
    internal sealed class PinnedMissions
    {
        public const int MaxPins = 5;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;
        private const float MenuRefreshSeconds = .5f;

        private static readonly Type ManagerType = typeof(FirstPersonController).Assembly.GetType("QuestManager");
        private static readonly Type MissionType = typeof(FirstPersonController).Assembly.GetType("RuntimeMission");
        private static readonly Type QuestType = typeof(FirstPersonController).Assembly.GetType("QuestSO");
        private static readonly FieldInfo ManagerInstance = ManagerType?.GetField("Instance", Flags);
        private static readonly FieldInfo RuntimeMissions = ManagerType?.GetField("runtimeMissions", Flags);
        private static readonly FieldInfo QuestDatabase = ManagerType?.GetField("QuestDatabaseSO", Flags);
        private static readonly FieldInfo QuestSOId = QuestType?.GetField("ID", Flags);
        private static readonly FieldInfo MissionQuest = MissionType?.GetField("QuestSO", Flags);
        private static readonly FieldInfo MissionKind = MissionType?.GetField("missionType", Flags);
        private static readonly FieldInfo MissionCurrent = MissionType?.GetField("currentValue", Flags);
        private static readonly FieldInfo MissionTarget = MissionType?.GetField("target", Flags);
        private static readonly FieldInfo MissionReached = MissionType?.GetField("targetAlreadyReached", Flags);
        private static readonly PropertyInfo MissionDescription = MissionType?.GetProperty("Description", Flags);
        private static readonly PropertyInfo MissionTitle = MissionType?.GetProperty("Title", Flags);
        private static readonly MethodInfo QuestCompleted = QuestType == null ? null :
            ManagerType?.GetMethod("IsQuestCompleted", Flags, null, new[] { QuestType }, null);

        private static readonly FieldInfo MenuButtons = typeof(GlobalMissionMenu).GetField("missionButtons", Flags);
        private static readonly FieldInfo MenuDailyRoot = typeof(GlobalMissionMenu).GetField("dailyButtonListParent", Flags);
        private static readonly FieldInfo ButtonQuest = typeof(GlobalMissionButton).GetField("questSO", Flags);
        private static readonly FieldInfo ButtonDescription = typeof(GlobalMissionButton).GetField("missionDescription", Flags);

        private readonly ConfigEntry<string> _saved;
        private readonly ManualLogSource _log;
        private readonly List<string> _ids = new List<string>(MaxPins);
        private readonly Dictionary<string, string> _titles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private float _nextMenuUpdate;
        private readonly Dictionary<VisualElement, Button> _buttons =
            new Dictionary<VisualElement, Button>();
        private readonly Texture2D _pinIcon;

        // On the screenshot right:56 makes the PIN button overlap both the
        // completion tick and the reward. Keep it between progress and reward.
        private const float PinRightInset = 174f;
        private const float PinSize = 37f;

        public PinnedMissions(ConfigEntry<string> saved, ManualLogSource log)
        {
            _saved = saved;
            _log = log;
            _pinIcon = LoadPinIcon(log);
            Load();
        }

        public int Count => _ids.Count;

        private void Load()
        {
            _ids.Clear();
            foreach (string token in (_saved.Value ?? "").Split('|'))
            {
                string id = token.Trim();
                if (id.Length == 0 || id.Length > 200 || _ids.Contains(id) ||
                    _ids.Count >= MaxPins) continue;
                _ids.Add(id);
            }
        }

        public void UpdateMenu()
        {
            if (Time.unscaledTime < _nextMenuUpdate) return;
            _nextMenuUpdate = Time.unscaledTime + MenuRefreshSeconds;

            GlobalMissionMenu menu = GlobalMissionMenu.Instance;
            if (menu == null || !menu.IsOpen)
            {
                _buttons.Clear();
                return;
            }

            IList items = Get(MenuButtons, menu) as IList;
            VisualElement dailyRoot = Get(MenuDailyRoot, menu) as VisualElement;
            if (items == null || ButtonQuest == null) return;

            // A bounded scan of the rows already created by the game. The
            // original mission menu owns their lifetime and rebuilds them.
            for (int i = 0; i < items.Count; i++)
            {
                GlobalMissionButton row = items[i] as GlobalMissionButton;
                if (row == null || row.panel == null) continue;
                if (dailyRoot != null && IsInside(row, dailyRoot)) continue;

                object quest = Get(ButtonQuest, row);
                string id = Get(QuestSOId, quest) as string;
                if (string.IsNullOrWhiteSpace(id)) continue;

                Label description = Get(ButtonDescription, row) as Label;
                if (description != null && !string.IsNullOrWhiteSpace(description.text))
                    _titles[id] = description.text;

                Button pin;
                if (!_buttons.TryGetValue(row, out pin) ||
                    pin == null || pin.parent != row)
                {
                    // If the menu was hidden and opened again, reuse the
                    // existing button rather than adding a duplicate.
                    pin = row.Q<Button>("DailyQuestTrackerPin");
                    if (pin == null)
                    {
                        string capturedId = id;
                        pin = new Button(() => Toggle(capturedId))
                        {
                            name = "DailyQuestTrackerPin",
                            pickingMode = PickingMode.Position,
                            focusable = false
                        };
                        pin.style.position = Position.Absolute;
                        pin.style.right = PinRightInset;
                        pin.style.top = 12f;
                        pin.style.width = PinSize;
                        pin.style.height = PinSize;
                        pin.style.borderLeftWidth = 1f;
                        pin.style.borderRightWidth = 1f;
                        pin.style.borderTopWidth = 1f;
                        pin.style.borderBottomWidth = 1f;
                        pin.style.paddingLeft = 0f;
                        pin.style.paddingRight = 0f;
                        pin.style.paddingTop = 0f;
                        pin.style.paddingBottom = 0f;
                        pin.text = "";
                        pin.userData = id;

                        if (_pinIcon != null)
                        {
                            var glyph = new Image
                            {
                                name = "DailyQuestTrackerPinGlyph",
                                image = _pinIcon,
                                scaleMode = ScaleMode.ScaleToFit,
                                pickingMode = PickingMode.Ignore
                            };
                            glyph.style.position = Position.Absolute;
                            glyph.style.left = 4f;
                            glyph.style.top = 3f;
                            glyph.style.width = 27f;
                            glyph.style.height = 29f;
                            pin.Add(glyph);
                        }
                        else
                        {
                            // If Unity's image converter is unavailable, show
                            // the control rather than silently losing pinning.
                            pin.text = "+";
                            pin.style.fontSize = 25f;
                            pin.style.unityTextAlign = TextAnchor.MiddleCenter;
                        }
                        var hoveredPin = pin;
                        pin.RegisterCallback<PointerEnterEvent>(evt =>
                            SetPinAppearance(hoveredPin, (string)hoveredPin.userData, true));
                        pin.RegisterCallback<PointerLeaveEvent>(evt =>
                            SetPinAppearance(hoveredPin, (string)hoveredPin.userData));
                        pin.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                        pin.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
                        pin.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                        row.Add(pin);
                    }
                    _buttons[row] = pin;
                }
                pin.userData = id;
                SetPinAppearance(pin, id);
            }
        }

        private static bool IsInside(VisualElement child, VisualElement parent)
        {
            for (VisualElement element = child; element != null; element = element.parent)
                if (ReferenceEquals(element, parent)) return true;
            return false;
        }

        private void Toggle(string id)
        {
            if (_ids.Remove(id))
            {
                _saved.Value = string.Join("|", _ids.ToArray());
                _log.LogInfo("Unpinned global mission: " + id);
            }
            else
            {
                if (_ids.Count >= MaxPins)
                {
                    _log.LogWarning("You can pin at most " + MaxPins + " missions.");
                    return;
                }
                _ids.Add(id);
                _saved.Value = string.Join("|", _ids.ToArray());
                _log.LogInfo("Pinned global mission: " + id);
            }
            _nextMenuUpdate = 0f;
            // Refresh immediately; the menu should never show a stale state.
            foreach (Button control in _buttons.Values)
                if (control != null && control.panel != null &&
                    control.userData is string key)
                    SetPinAppearance(control, key);
        }

        private void SetPinAppearance(Button pin, string id, bool hovering = false)
        {
            bool selected = _ids.Contains(id);
            // Subtle slate/burgundy button compatible with the game's menu;
            // gold frame for tracking, brighter on hover.
            Color edge = hovering ? new Color(1f, .85f, .56f, 1f) :
                selected ? new Color(.98f, .78f, .38f, 1f) :
                new Color(.60f, .55f, .44f, 1f);
            pin.style.borderLeftColor = edge;
            pin.style.borderRightColor = edge;
            pin.style.borderTopColor = edge;
            pin.style.borderBottomColor = edge;
            pin.style.backgroundColor = hovering
                ? new Color(.34f, .23f, .23f, .98f)
                : selected ? new Color(.38f, .27f, .18f, .98f)
                    : new Color(.16f, .20f, .26f, .93f);

            Image glyph = pin.Q<Image>("DailyQuestTrackerPinGlyph");
            if (glyph != null)
                glyph.tintColor = selected || hovering
                    ? Color.white : new Color(.84f, .79f, .70f, .84f);
            else
                pin.style.color = edge;
            pin.tooltip = selected ? "Unpin mission" :
                _ids.Count >= MaxPins ? "Five missions maximum" : "Pin mission";
        }

        private static Texture2D LoadPinIcon(ManualLogSource log)
        {
            try
            {
                using (Stream stream = typeof(PinnedMissions).Assembly
                    .GetManifestResourceStream("StonewardsDailyQuestTracker.pin-icon.png"))
                {
                    if (stream == null)
                        throw new InvalidOperationException("Embedded pin artwork not found.");
                    byte[] png = new byte[checked((int)stream.Length)];
                    int offset = 0;
                    while (offset < png.Length)
                    {
                        int read = stream.Read(png, offset, png.Length - offset);
                        if (read == 0) throw new EndOfStreamException();
                        offset += read;
                    }

                    // ImageConversion is a Unity extension in a separate
                    // module. Reflection avoids adding new game DLL references
                    // to the mod's otherwise stable build dependencies.
                    Type converter = Type.GetType(
                        "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                    if (converter == null)
                        throw new InvalidOperationException("Unity ImageConversion unavailable.");
                    MethodInfo load = converter.GetMethod("LoadImage",
                        BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
                    if (load == null)
                        throw new MissingMethodException("ImageConversion.LoadImage");
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = "DailyQuestTracker-BrassPin",
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    object result = load.Invoke(null, new object[] { texture, png, false });
                    if (result is bool loaded && !loaded)
                        throw new InvalidDataException("Unity failed to decode pin artwork.");
                    return texture;
                }
            }
            catch (Exception ex)
            {
                log.LogWarning("Pinned mission icon load failed: " + ex.Message);
                return null;
            }
        }

        public List<QuestView> Read()
        {
            var result = new List<QuestView>(_ids.Count);
            if (_ids.Count == 0) return result;
            object manager = Get(ManagerInstance, null);
            if (manager == null) return result;

            IList missions = Get(RuntimeMissions, manager) as IList;
            IList definitions = null;
            object database = Get(QuestDatabase, manager);
            if (database != null)
            {
                FieldInfo questsField = database.GetType().GetField("Quests", Flags);
                definitions = Get(questsField, database) as IList;
            }

            foreach (string id in _ids)
            {
                object quest = null;
                object found = null;
                if (missions != null)
                {
                    foreach (object mission in missions)
                    {
                        if (mission == null) continue;
                        object kind = Get(MissionKind, mission);
                        if (kind == null || !string.Equals(kind.ToString(),
                                "GLOBAL", StringComparison.OrdinalIgnoreCase))
                            continue;
                        object asset = Get(MissionQuest, mission);
                        if (string.Equals(Get(QuestSOId, asset) as string, id,
                            StringComparison.Ordinal))
                        {
                            quest = asset;
                            found = mission;
                            break;
                        }
                    }
                }
                // Completed quests may disappear from active runtime missions;
                // resolve the asset from the persistent game database then.
                if (quest == null && definitions != null)
                    foreach (object asset in definitions)
                        if (string.Equals(Get(QuestSOId, asset) as string, id,
                            StringComparison.Ordinal))
                        {
                            quest = asset;
                            break;
                        }

                bool completed = false;
                if (quest != null && QuestCompleted != null)
                    try { completed = QuestCompleted.Invoke(manager, new[] { quest }) is bool done && done; }
                    catch (Exception) { }

                int? current = null;
                int? target = null;
                if (found != null)
                {
                    object cur = Get(MissionCurrent, found);
                    object tar = Get(MissionTarget, found);
                    if (cur is float c && tar is float t && t > 0f &&
                        !float.IsNaN(c) && !float.IsNaN(t))
                    {
                        current = Mathf.Max(0, Mathf.RoundToInt(c));
                        target = Mathf.Max(1, Mathf.RoundToInt(t));
                        completed = completed || current.Value >= target.Value ||
                            Get(MissionReached, found) is bool reached && reached;
                    }
                    string liveTitle = Get(MissionDescription, found) as string;
                    if (string.IsNullOrWhiteSpace(liveTitle))
                        liveTitle = Get(MissionTitle, found) as string;
                    if (!string.IsNullOrWhiteSpace(liveTitle))
                        _titles[id] = liveTitle;
                }
                string title;
                if (!_titles.TryGetValue(id, out title))
                    title = id.Replace("QUEST_", "").Replace("_", " ");
                result.Add(new QuestView
                {
                    Id = id,
                    Title = title,
                    Progress = current,
                    Target = target,
                    Completed = completed
                });
            }
            return result;
        }

        private static object Get(FieldInfo field, object instance)
        {
            if (field == null || instance == null && !field.IsStatic) return null;
            try { return field.GetValue(instance); }
            catch (Exception) { return null; }
        }

        private static object Get(PropertyInfo property, object instance)
        {
            if (property == null || instance == null && !property.GetGetMethod(true).IsStatic)
                return null;
            try { return property.GetValue(instance, null); }
            catch (Exception) { return null; }
        }
    }
}
