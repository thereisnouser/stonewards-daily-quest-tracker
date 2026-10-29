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
        private static readonly FieldInfo ButtonValidationIcon = typeof(GlobalMissionButton).GetField("validationIcon", Flags);
        private static readonly FieldInfo ButtonProgressBar = typeof(GlobalMissionButton).GetField("missionProgressBar", Flags);

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
        // In the game's mission row this centers the pin within the free
        // space between the progress bar and the reward/checkmark column.
        private const float PinRightInset = 260f;
        // Compact circular control; keep the approved brass icon unchanged.
        private const float PinSize = 50f;
        private const float PinGlyphSize = 28f;
        // GlobalMissionButton's visual plaque sits slightly below its layout
        // origin; compensate without hard-coding the button's vertical top.
        private const float PinVisualCenterOffset = 0f;
        private readonly HashSet<Button> _hoveredPins = new HashSet<Button>();
        // Seven rectangular bands approximate a crisp pixel-art circle.
        // Separate outline and inset layers create a stepped gold frame.
        private static readonly Rect[] FrameOuter =
        {
            new Rect(18,0,14,3), new Rect(11,3,28,5),
            new Rect(6,8,38,5), new Rect(1,13,48,24),
            new Rect(6,37,38,5), new Rect(11,42,28,5),
            new Rect(18,47,14,3)
        };
        private static readonly Rect[] FrameInner =
        {
            new Rect(18,3,14,2), new Rect(13,5,24,5),
            new Rect(8,10,34,4), new Rect(3,14,44,22),
            new Rect(8,36,34,4), new Rect(13,40,24,5),
            new Rect(18,45,14,2)
        };
        private sealed class PinFrame
        {
            internal readonly VisualElement[] Outer = new VisualElement[7];
            internal readonly VisualElement[] Inner = new VisualElement[7];
            internal float Size = -1f;
        }
        private readonly Dictionary<Button, PinFrame> _frames =
            new Dictionary<Button, PinFrame>();

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
                _hoveredPins.Clear();
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
                        // Center from the row's actual layout height, not a
                        // fixed Y coordinate (rows can scale with resolution).
                        pin.style.top = 10f; // Fallback until game geometry is measured.
                        pin.style.width = PinSize;
                        pin.style.height = PinSize;
                        float radius = PinSize * .5f;
                        pin.style.borderTopLeftRadius = radius;
                        pin.style.borderTopRightRadius = radius;
                        pin.style.borderBottomLeftRadius = radius;
                        pin.style.borderBottomRightRadius = radius;
                        pin.style.overflow = Overflow.Hidden;
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
                            // Keep the original sprite pixel-sharp but give
                            // it breathing room inside the circular border.
                            float glyphInset = (PinSize - PinGlyphSize) * .5f;
                            glyph.style.left = glyphInset;
                            glyph.style.top = glyphInset;
                            glyph.style.width = PinGlyphSize;
                            glyph.style.height = PinGlyphSize;
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
                        {
                            _hoveredPins.Add(hoveredPin);
                            SetPinAppearance(hoveredPin, (string)hoveredPin.userData, true);
                        });
                        pin.RegisterCallback<PointerLeaveEvent>(evt =>
                        {
                            _hoveredPins.Remove(hoveredPin);
                            SetPinAppearance(hoveredPin, (string)hoveredPin.userData);
                        });
                        pin.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                        pin.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
                        pin.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                        row.Add(pin);
                        // Recenter when the game changes layout (resolution,
                        // panel resize, localization or row style changes).
                        row.RegisterCallback<GeometryChangedEvent>(evt =>
                            CenterPinVertically(row, pin));
                    }
                    _buttons[row] = pin;
                }
                CenterPinVertically(row, pin);
                pin.userData = id;
                SetPinAppearance(pin, id, _hoveredPins.Contains(pin));
            }
        }

        private static void CenterPinVertically(VisualElement row, Button pin)
        {
            if (row == null || pin == null || pin.parent != row || row.panel == null)
                return;

            float height = row.layout.height;
            if (float.IsNaN(height) || height <= 0f) return;

            // Align to the real game's checkmark, or the progress bar when
            // completion is hidden. The row container includes extra padding
            // and is not centered over the visible mission plaque.
            VisualElement check = Get(ButtonValidationIcon, row) as VisualElement;
            VisualElement progress = Get(ButtonProgressBar, row) as VisualElement;
            VisualElement reference = HasVisibleRect(check) ? check :
                HasVisibleRect(progress) ? progress : null;

            float center = height * .5f;
            if (reference != null && row.worldBound.height > 0f)
                center = (reference.worldBound.center.y - row.worldBound.yMin) *
                         height / row.worldBound.height;

            float size = PinSize;
            if (HasVisibleRect(check) && check.worldBound.height >= 35f &&
                check.worldBound.height <= 70f)
                size = Mathf.Round(check.worldBound.height *
                    height / row.worldBound.height);
            size = Mathf.Clamp(size, 42f, 56f);

            pin.style.width = size;
            pin.style.height = size;
            pin.style.top = Mathf.Round(Mathf.Max(0f, center - size * .5f));

            Image glyph = pin.Q<Image>("DailyQuestTrackerPinGlyph");
            if (glyph != null)
            {
                float inset = Mathf.Round((size - PinGlyphSize) * .5f);
                glyph.style.left = inset;
                glyph.style.top = inset;
            }
        }

        private static bool HasVisibleRect(VisualElement element)
        {
            if (element == null || element.panel == null ||
                element.resolvedStyle.display == DisplayStyle.None)
                return false;
            Rect bounds = element.worldBound;
            return !float.IsNaN(bounds.y) && bounds.height > 8f &&
                   !float.IsNaN(bounds.x) && bounds.width > 8f;
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
                    SetPinAppearance(control, key, _hoveredPins.Contains(control));
        }

        private void SetPinAppearance(Button pin, string id, bool hovering = false)
        {
            bool selected = _ids.Contains(id);

            // A permanent, understated warm border helps the pin belong to
            // Stonewards' burgundy-and-gold Missions interface. Hover and
            // pinned states are brighter, but the original art never changes.
            Color edge = hovering ? new Color(1f, .87f, .58f, 1f)
                : selected ? new Color(.98f, .76f, .38f, 1f)
                : new Color(.58f, .47f, .30f, .95f);
            float border = selected || hovering ? 2f : 1f;
            pin.style.borderLeftWidth = border;
            pin.style.borderRightWidth = border;
            pin.style.borderTopWidth = border;
            pin.style.borderBottomWidth = border;
            pin.style.borderLeftColor = edge;
            pin.style.borderRightColor = edge;
            pin.style.borderTopColor = edge;
            pin.style.borderBottomColor = edge;
            pin.style.backgroundColor = hovering
                ? new Color(.24f, .19f, .16f, .88f)
                : selected ? new Color(.30f, .21f, .16f, .94f)
                    : new Color(.14f, .14f, .16f, .72f);

            Image glyph = pin.Q<Image>("DailyQuestTrackerPinGlyph");
            if (glyph != null)
                glyph.tintColor = Color.white;
            else
                pin.style.color = new Color(.98f, .79f, .51f);
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
