using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Exact Stonewards 0.2.3 daily quest reader.
    /// Uses QuestManager.CurrentDailyQuests as the authoritative selection and
    /// matches each QuestSO to QuestManager.runtimeMissions for current/target.
    /// Field and property handles are resolved once. No scene scanning,
    /// ScriptableObject database enumeration, RPCs or gameplay hooks.
    /// </summary>
    internal sealed class DailyQuestReader
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;

        private static readonly Assembly GameAssembly = typeof(FirstPersonController).Assembly;
        private static readonly Type ManagerType = GameAssembly.GetType("QuestManager");
        private static readonly Type MissionType = GameAssembly.GetType("RuntimeMission");
        private static readonly Type QuestType = GameAssembly.GetType("QuestSO");

        private static readonly FieldInfo ManagerInstance =
            ManagerType?.GetField("Instance", Flags);
        private static readonly PropertyInfo SelectedDaily =
            ManagerType?.GetProperty("CurrentDailyQuests", Flags);
        private static readonly FieldInfo RuntimeMissions =
            ManagerType?.GetField("runtimeMissions", Flags);
        // The game keeps this total separately from its live daily list,
        // which can shrink when a quest is completed or its reward claimed.
        private static readonly FieldInfo DailyQuestCount =
            ManagerType?.GetField("DAILY_QUESTS_COUNT", Flags);
        private static readonly MethodInfo IsQuestCompleted =
            ManagerType != null && QuestType != null
                ? ManagerType.GetMethod("IsQuestCompleted", Flags, null,
                    new[] { QuestType }, null)
                : null;

        private static readonly FieldInfo MissionQuest =
            MissionType?.GetField("QuestSO", Flags);
        private static readonly FieldInfo MissionTypeField =
            MissionType?.GetField("missionType", Flags);
        private static readonly FieldInfo MissionCurrent =
            MissionType?.GetField("currentValue", Flags);
        private static readonly FieldInfo MissionTarget =
            MissionType?.GetField("target", Flags);
        private static readonly FieldInfo MissionReached =
            MissionType?.GetField("targetAlreadyReached", Flags);
        private static readonly PropertyInfo MissionDescription =
            MissionType?.GetProperty("Description", Flags);
        private static readonly PropertyInfo MissionTitle =
            MissionType?.GetProperty("Title", Flags);
        private static readonly FieldInfo QuestId =
            QuestType?.GetField("ID", Flags);

        private readonly ManualLogSource _log;
        private bool _reported;
        public int TotalDailyQuests { get; private set; }
        public bool HasSeenDailyQuests { get; private set; }
        private readonly List<QuestView> _empty = new List<QuestView>(0);

        public DailyQuestReader(ManualLogSource log)
        {
            _log = log;
            if (ManagerInstance == null || SelectedDaily == null ||
                RuntimeMissions == null || MissionQuest == null ||
                MissionTypeField == null || MissionCurrent == null ||
                MissionTarget == null)
            {
                _log.LogError("Stonewards daily quest API fields missing. " +
                    "Please capture F9 diagnostics for the installed game version.");
            }
            else
            {
                _log.LogInfo("Daily quest API bound: QuestManager.CurrentDailyQuests " +
                    "+ runtimeMissions (currentValue / target / missionType).");
            }
        }

        public void ResetSession()
        {
            // No session-persistent quest object references are retained.
            // Important: daily objectives can regenerate at the daily reset.
            _reported = false;
            TotalDailyQuests = 0;
            HasSeenDailyQuests = false;
        }

        public List<QuestView> ReadDailyQuests()
        {
            if (ManagerInstance == null || SelectedDaily == null ||
                RuntimeMissions == null || MissionQuest == null ||
                MissionTypeField == null || MissionCurrent == null ||
                MissionTarget == null)
                return _empty;

            var manager = Get(ManagerInstance, null);
            if (manager == null) return _empty;
            var chosen = Get(SelectedDaily, manager) as IList;
            var live = Get(RuntimeMissions, manager) as IList;
            if (chosen == null || live == null)
            {
                WarnOnce("Daily quest selection or runtime missions are not initialized yet.");
                return _empty;
            }

            // Use the game's declared daily quest count for the denominator.
            // The active list alone is not a reliable total after completion.
            // Retain the largest observed selection as a compatibility fallback.
            object countRaw = Get(DailyQuestCount, null);
            if (countRaw is int declaredCount && declaredCount > 0 && declaredCount <= 64)
                TotalDailyQuests = Math.Max(declaredCount, chosen.Count);
            else
                TotalDailyQuests = Math.Max(TotalDailyQuests, chosen.Count);

            var result = new List<QuestView>(chosen.Count);
            for (int i = 0; i < chosen.Count; i++)
            {
                var quest = chosen[i];
                if (quest == null) continue;
                string id = Get(QuestId, quest) as string;
                object match = null;

                // Runtime missions also contain GLOBAL and RUN objectives.
                // Read only missions selected in CurrentDailyQuests, and
                // distinguish two quests of the same type by QuestSO ID.
                foreach (object mission in live)
                {
                    if (mission == null) continue;
                    object type = Get(MissionTypeField, mission);
                    if (type == null || !string.Equals(type.ToString(), "DAILY",
                        StringComparison.OrdinalIgnoreCase)) continue;

                    object missionQuest = Get(MissionQuest, mission);
                    if (ReferenceEquals(missionQuest, quest))
                    {
                        match = mission;
                        break;
                    }
                    if (missionQuest != null && !string.IsNullOrEmpty(id) &&
                        string.Equals(Get(QuestId, missionQuest) as string, id,
                            StringComparison.Ordinal))
                    {
                        match = mission;
                        break;
                    }
                }

                if (match == null) continue;
                object currentRaw = Get(MissionCurrent, match);
                object targetRaw = Get(MissionTarget, match);
                if (!(currentRaw is float currentValue) ||
                    !(targetRaw is float targetValue) ||
                    float.IsNaN(currentValue) || float.IsNaN(targetValue) ||
                    targetValue <= 0f) continue;

                bool reached = Get(MissionReached, match) is bool hit && hit;
                bool gameCompleted = false;
                if (QuestType != null && QuestType.IsInstanceOfType(quest) &&
                    IsQuestCompleted != null)
                    gameCompleted = GetCompleted(manager, quest);
                bool completed = gameCompleted || reached || currentValue >= targetValue;

                // These getters use the game's actual localized quest strings.
                // No hand-written ID-to-title dictionary is required.
                string title = Get(MissionDescription, match) as string;
                if (string.IsNullOrWhiteSpace(title))
                    title = Get(MissionTitle, match) as string;
                if (string.IsNullOrWhiteSpace(title))
                    title = id ?? "Daily quest";

                result.Add(new QuestView
                {
                    Id = id ?? title,
                    Title = title,
                    Progress = Mathf.Max(0, Mathf.RoundToInt(currentValue)),
                    Target = Mathf.Max(1, Mathf.RoundToInt(targetValue)),
                    Completed = completed
                });
            }

            if (result.Count > 0)
            {
                HasSeenDailyQuests = true;
                _reported = false;
            }
            else if (!HasSeenDailyQuests)
            {
                WarnOnce("No selected daily quests matched live DAILY runtime missions. " +
                    "Try opening Missions / Daily and press F9 if this persists.");
            }
            return result;
        }

        private bool GetCompleted(object manager, object quest)
        {
            try
            {
                return IsQuestCompleted.Invoke(manager, new object[] { quest }) is bool done && done;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void WarnOnce(string message)
        {
            if (_reported) return;
            _reported = true;
            _log.LogWarning(message);
        }

        private static object Get(FieldInfo field, object instance)
        {
            if (field == null) return null;
            try { return field.GetValue(instance); }
            catch (Exception) { return null; }
        }

        private static object Get(PropertyInfo property, object instance)
        {
            if (property == null) return null;
            try { return property.GetValue(instance, null); }
            catch (Exception) { return null; }
        }
    }
}
