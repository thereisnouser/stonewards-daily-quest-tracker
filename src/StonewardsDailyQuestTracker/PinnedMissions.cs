using System;
using System.Collections;
using System.Collections.Generic;
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

        public PinnedMissions(ConfigEntry<string> saved, ManualLogSource log)
        {
            _saved = saved;
            _log = log;
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
                        pin.style.right = 56;
                        pin.style.top = 15;
                        pin.style.width = 51;
                        pin.style.height = 30;
                        pin.style.unityTextAlign = TextAnchor.MiddleCenter;
                        pin.style.fontSize = 17;
                        pin.style.borderLeftWidth = 1;
                        pin.style.borderRightWidth = 1;
                        pin.style.borderTopWidth = 1;
                        pin.style.borderBottomWidth = 1;
                        pin.style.borderLeftColor = new Color(.72f, .64f, .48f);
                        pin.style.borderRightColor = new Color(.72f, .64f, .48f);
                        pin.style.borderTopColor = new Color(.72f, .64f, .48f);
                        pin.style.borderBottomColor = new Color(.72f, .64f, .48f);
                        pin.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                        pin.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
                        pin.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                        row.Add(pin);
                    }
                    _buttons[row] = pin;
                }
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
        }

        private void SetPinAppearance(Button pin, string id)
        {
            bool selected = _ids.Contains(id);
            pin.text = selected ? "ON" : "PIN";
            pin.style.color = selected ? new Color(.47f, .87f, .56f) :
                new Color(.98f, .79f, .51f);
            pin.style.backgroundColor = selected
                ? new Color(.17f, .30f, .23f, .98f)
                : new Color(.29f, .18f, .19f, .96f);
            pin.tooltip = selected ? "Remove from tracker" :
                _ids.Count >= MaxPins ? "Five missions maximum" : "Track this mission in HUD";
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
