using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// Read-only adapter. Daily objectives must come from the game's quest state:
    /// never infer progress from damage, drops or client-side event counting.
    /// Quest types have not been pinned down for the 0.2.3 game binary, so all
    /// reflection probes are guarded; F9 produces a detailed API report.
    /// </summary>
    internal sealed class DailyQuestReader
    {
        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const int MaxDepth = 5;
        private const int MaxNodes = 700;
        private readonly ManualLogSource _log;
        private readonly Type[] _questTypes;
        private readonly Dictionary<Type, MemberInfo[]> _members = new Dictionary<Type, MemberInfo[]>();
        private bool _reportedDiscovery;
        private bool _discovered;
        private readonly List<LiveSource> _sources = new List<LiveSource>();
        private readonly List<QuestView> _empty = new List<QuestView>(0);
        private sealed class LiveSource
        {
            public MemberInfo Member;
            public Component Instance;
            public string Path;
            public bool Daily;
            public object Read() => Instance != null ? (object)Instance : GetValue(Member, null);
        }
        public void ResetSession()
        {
            _discovered = false;
            _sources.Clear();
            _reportedDiscovery = false;
        }


        public DailyQuestReader(ManualLogSource log)
        {
            _log = log;
            Type[] types;
            try { types = typeof(FirstPersonController).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
            // Also include non-Quest managers (e.g. profile/save managers) that
            // directly own daily-quest collections. Do not traverse unrelated game systems.
            _questTypes = types.Where(t => t != null &&
                (Contains(t.Name, "quest") || Contains(t.Name, "mission") ||
                 HasDailyState(t)))
                .OrderBy(t => t.Name).ToArray();
            _log.LogInfo("Detected " + _questTypes.Length + " game types with Quest/Mission in their names. F9 exports their API.");
        }

        public List<QuestView> ReadDailyQuests()
        {
            if (!_discovered) DiscoverSources();
            if (_sources.Count == 0) return _empty;
            var result = new Dictionary<string, QuestView>(StringComparer.OrdinalIgnoreCase);
            int budget = 200;
            foreach (LiveSource source in _sources)
            {
                object root = source.Read();
                if (root == null) continue;
                Walk(root, source.Path, source.Daily, 0,
                    new HashSet<object>(ReferenceComparer.Instance), result, ref budget);
                if (budget <= 0) break;
            }
            return result.Values.OrderBy(v => v.Completed).ThenBy(v => v.Title).ToList();
        }

        private void DiscoverSources()
        {
            // Expensive reflection and FindObjectsOfTypeAll run only once per
            // connected session. Subsequent reads use only validated roots.
            _discovered = true;
            int budget = MaxNodes;
            foreach (Type type in _questTypes)
            {
                if (!IsRootType(type) || IsDefinitionType(type)) continue;
                foreach (MemberInfo member in Members(type))
                {
                    if (!IsStatic(member) || !IsStateMember(member.Name) ||
                        IsDefinitionName(member.Name) || IsDefinitionType(MemberType(member)))
                        continue;
                    object root = GetValue(member, null);
                    if (root == null) continue;
                    bool daily = Contains(type.Name, "daily") || Contains(member.Name, "daily");
                    var probe = new Dictionary<string, QuestView>(StringComparer.OrdinalIgnoreCase);
                    Walk(root, type.Name + "." + member.Name, daily, 0,
                        new HashSet<object>(ReferenceComparer.Instance), probe, ref budget);
                    if (probe.Count > 0)
                        _sources.Add(new LiveSource { Member = member,
                            Path = type.Name + "." + member.Name, Daily = daily });
                    if (budget <= 0 || _sources.Count >= 8) break;
                }
                if (budget <= 0 || _sources.Count >= 8) break;
                if (!typeof(Component).IsAssignableFrom(type) ||
                    !(Contains(type.Name, "manager") || Contains(type.Name, "controller") ||
                      Contains(type.Name, "system") || Contains(type.Name, "tracker")))
                    continue;
                UnityEngine.Object[] instances;
                try { instances = Resources.FindObjectsOfTypeAll(type); }
                catch { continue; }
                int checkedCount = 0;
                foreach (UnityEngine.Object instance in instances)
                {
                    var component = instance as Component;
                    if (component == null || !component.gameObject.scene.IsValid()) continue;
                    if (++checkedCount > 3) break;
                    var probe = new Dictionary<string, QuestView>(StringComparer.OrdinalIgnoreCase);
                    bool daily = Contains(type.Name, "daily");
                    Walk(component, type.Name + "[scene]", daily, 0,
                        new HashSet<object>(ReferenceComparer.Instance), probe, ref budget);
                    if (probe.Count > 0)
                        _sources.Add(new LiveSource { Instance = component,
                            Path = type.Name + "[scene]", Daily = daily });
                    if (budget <= 0 || _sources.Count >= 8) break;
                }
                if (budget <= 0 || _sources.Count >= 8) break;
            }
            _log.LogInfo("Daily quest discovery: " + _sources.Count +
                " live sources cached. No scene-wide searches during refresh.");
            if (_sources.Count == 0 && !_reportedDiscovery)
            {
                _reportedDiscovery = true;
                _log.LogWarning("No verified live daily quest counters found. " +
                    "Definitions are hidden. Press F9 to export quest API diagnostics.");
            }
        }

        private static bool IsDefinitionName(string name) =>
            Contains(name, "database") || Contains(name, "definition") ||
            Contains(name, "catalog") || Contains(name, "template") ||
            Contains(name, "prefab") || Contains(name, "asset");

        private static bool IsDefinitionType(Type type) =>
            type != null && (typeof(ScriptableObject).IsAssignableFrom(type) ||
                IsDefinitionName(type.Name) ||
                type.Name.EndsWith("SO", StringComparison.OrdinalIgnoreCase));

        private static bool HasDailyState(Type type)
        {
            try
            {
                return type.GetFields(MemberFlags).Any(f => Contains(f.Name, "daily") &&
                    (Contains(f.Name, "quest") || Contains(f.Name, "mission"))) ||
                    type.GetProperties(MemberFlags).Any(p => Contains(p.Name, "daily") &&
                    (Contains(p.Name, "quest") || Contains(p.Name, "mission")));
            }
            catch { return false; }
        }

        private static bool IsRootType(Type type)
        {
            if (type.IsGenericTypeDefinition || type.IsAbstract && !type.IsSealed) return false;
            string n = type.Name;
            return Contains(n, "manager") || Contains(n, "system") ||
                   Contains(n, "tracker") || Contains(n, "controller") ||
                   Contains(n, "menu") || Contains(n, "progress") ||
                   Contains(n, "quest") || Contains(n, "mission");
        }

        private void Walk(object value, string path, bool dailyPath, int depth,
            HashSet<object> visited, Dictionary<string, QuestView> result, ref int budget)
        {
            if (value == null || depth > MaxDepth || --budget < 0) return;
            Type type = value.GetType();
            if (IsDefinitionType(type)) return;
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal)) return;
            if (type.IsValueType && !Contains(type.FullName, "Mission") && !Contains(type.FullName, "Quest")) return;
            if (!type.IsValueType && !visited.Add(value)) return;

            if (value is IDictionary map)
            {
                int count = 0;
                foreach (DictionaryEntry entry in map)
                {
                    if (++count > 64) break;
                    Walk(entry.Value, path + "[]", dailyPath, depth + 1, visited, result, ref budget);
                }
                return;
            }
            if (value is IEnumerable list)
            {
                int count = 0;
                try
                {
                    foreach (object item in list)
                    {
                        if (++count > 64) break;
                        Walk(item, path + "[]", dailyPath, depth + 1, visited, result, ref budget);
                    }
                }
                catch { }
                return;
            }

            bool isDaily = dailyPath || Contains(type.Name, "daily") &&
                (Contains(type.Name, "quest") || Contains(type.Name, "mission")) ||
                Flag(value, "IsDaily", "IsDailyQuest", "IsDailyMission", "Daily") ||
                EnumSaysDaily(value, "Type", "MissionType", "QuestType", "Category", "Frequency");

            QuestView quest = TryQuest(value, isDaily);
            if (quest != null)
            {
                string key = !string.IsNullOrEmpty(quest.Id) ? quest.Id : quest.Title;
                if (!result.TryGetValue(key, out QuestView old) || Score(quest) > Score(old))
                    result[key] = quest;
            }

            // Never walk the world graph; only follow quest-state and model containers.
            if (depth >= MaxDepth) return;
            foreach (MemberInfo member in Members(type))
            {
                if (IsStatic(member) || !IsStateMember(member.Name)) continue;
                Type mt = MemberType(member);
                if (mt == null || mt == typeof(string) || mt.IsPrimitive || mt.IsEnum ||
                    typeof(Delegate).IsAssignableFrom(mt)) continue;
                object child = GetValue(member, value);
                if (child == null || ReferenceEquals(child, value)) continue;
                bool childDaily = isDaily || Contains(member.Name, "daily");
                Walk(child, path + "." + member.Name, childDaily, depth + 1,
                    visited, result, ref budget);
                if (budget <= 0) return;
            }
        }

        private static int Score(QuestView q) => (q.Progress.HasValue ? 2 : 0) + (q.Target.HasValue ? 2 : 0) + (q.Completed ? 1 : 0);

        private QuestView TryQuest(object value, bool isDaily)
        {
            if (!isDaily) return null;
            Type type = value.GetType();
            if (IsDefinitionType(type)) return null;
            string typeName = type.Name;
            // A daily collection may contain Mission/Quest instances, and some
            // games store quest progress as a class with an Objective suffix.
            if (!Contains(typeName, "quest") && !Contains(typeName, "mission") &&
                !Contains(typeName, "objective") && !Contains(typeName, "task")) return null;
            if (Contains(typeName, "menu") || Contains(typeName, "button") ||
                Contains(typeName, "window") || Contains(typeName, "panel") ||
                typeof(Component).IsAssignableFrom(type)) return null;

            string title = AsText(Named(value, "DisplayName", "QuestName", "MissionName", "Title",
                "LocalizedName", "Description", "Name", "ObjectiveName", "Text"));
            if (string.IsNullOrWhiteSpace(title) || title.Length > 160) return null;
            if (title == typeName || title.StartsWith(type.FullName + " ", StringComparison.Ordinal)) return null;

            var progress = Number(Named(value, "CurrentProgress", "Progress", "ProgressAmount",
                "CurrentAmount", "CurrentCount", "CurrentValue", "Value", "CompletedCount"));
            var target = Number(Named(value, "RequiredProgress", "TargetProgress", "GoalProgress",
                "MaxProgress", "RequiredAmount", "TargetAmount", "RequiredCount", "TargetCount",
                "RequiredValue", "TargetValue", "Goal", "Target", "AmountRequired", "Requirement"));
            bool completed = Flag(value, "IsCompleted", "Completed", "IsComplete", "Complete",
                "IsClaimed", "Claimed") ||
                EnumHas(value, "Status", "Completed", "Complete", "Claimed", "Finished");

            // Some quests store the counter on a nested object.
            object nested = Named(value, "ProgressData", "QuestProgress", "MissionProgress", "ObjectiveProgress");
            if (nested != null && !ReferenceEquals(nested, value))
            {
                if (!progress.HasValue)
                    progress = Number(Named(nested, "CurrentProgress", "Progress", "Amount", "Count", "Value"));
                if (!target.HasValue)
                    target = Number(Named(nested, "RequiredProgress", "TargetProgress", "Target", "Required", "Goal"));
            }
            if (target.HasValue && target.Value > 0 && progress.HasValue && progress.Value >= target.Value)
                completed = true;

            // Never display quest definitions (the 37 database templates shown
            // by v0.1.0); only live current / required counters qualify.
            if (!progress.HasValue || !target.HasValue || target.Value <= 0)
                return null;
            if (title.StartsWith("DAILY_", StringComparison.OrdinalIgnoreCase))
                title = HumanizeKey(title);
            string id = AsText(Named(value, "Id", "ID", "QuestId", "MissionId", "MissionID", "QuestID"));
            return new QuestView { Id = id, Title = title, Progress = progress, Target = target, Completed = completed };
        }

        private static string HumanizeKey(string key)
        {
            string[] pieces = key.Substring(6).Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            string label = string.Join(" ", pieces).ToLowerInvariant();
            return label.Length == 0 ? key : char.ToUpperInvariant(label[0]) + label.Substring(1);
        }

        private MemberInfo[] Members(Type t)
        {
            if (_members.TryGetValue(t, out MemberInfo[] members)) return members;
            try
            {
                members = t.GetMembers(MemberFlags)
                    .Where(x => x is FieldInfo || x is PropertyInfo p && p.GetIndexParameters().Length == 0)
                    .Where(x => !Contains(x.Name, "k__BackingField"))
                    .ToArray();
            }
            catch { members = new MemberInfo[0]; }
            _members[t] = members;
            return members;
        }

        private object Named(object source, params string[] names)
        {
            if (source == null) return null;
            foreach (string n in names)
            {
                // Prefer explicit case-insensitive matches, never substring guesses
                // when reading numerical counters.
                foreach (MemberInfo m in Members(source.GetType()))
                {
                    if (IsStatic(m) || !string.Equals(m.Name, n, StringComparison.OrdinalIgnoreCase)) continue;
                    object result = GetValue(m, source);
                    if (result != null) return result;
                }
            }
            return null;
        }

        private bool Flag(object obj, params string[] names)
        {
            object v = Named(obj, names);
            return v is bool b && b;
        }
        private bool EnumSaysDaily(object obj, params string[] names)
        {
            object value = Named(obj, names);
            return value != null && Contains(value.ToString(), "daily");
        }
        private bool EnumHas(object obj, string name, params string[] values)
        {
            object result = Named(obj, name);
            return result != null && values.Any(v => string.Equals(result.ToString(), v, StringComparison.OrdinalIgnoreCase));
        }
        private static int? Number(object raw)
        {
            if (raw == null || raw is bool || raw is Enum) return null;
            try
            {
                if (raw is int n) return n;
                if (raw is long lng && lng >= int.MinValue && lng <= int.MaxValue) return (int)lng;
                if (raw is float f && !float.IsNaN(f)) return (int)Math.Round(f);
                if (raw is double d && !double.IsNaN(d)) return (int)Math.Round(d);
                if (raw is short sh) return sh;
                if (raw is byte b) return b;
                if (raw is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            }
            catch { }
            return null;
        }
        private static string AsText(object value)
        {
            if (value is string s) return s.Trim();
            if (value is Enum e) return e.ToString();
            return null;
        }
        private static bool Contains(string s, string part) =>
            !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsStateMember(string s) =>
            Contains(s, "daily") || Contains(s, "mission") || Contains(s, "quest") ||
            Contains(s, "objective") || Contains(s, "task") || Contains(s, "progress") ||
            Contains(s, "instance") || Contains(s, "current") || Contains(s, "active") ||
            Contains(s, "available") || Contains(s, "entries") || Contains(s, "data") ||
            Contains(s, "records") || Contains(s, "save") || string.Equals(s, "All", StringComparison.OrdinalIgnoreCase);
        private static bool IsStatic(MemberInfo m) =>
            m is FieldInfo f ? f.IsStatic : m is PropertyInfo p && p.GetGetMethod(true)?.IsStatic == true;
        private static Type MemberType(MemberInfo m) =>
            m is FieldInfo f ? f.FieldType : m is PropertyInfo p ? p.PropertyType : null;
        private static object GetValue(MemberInfo m, object instance)
        {
            try
            {
                if (m is FieldInfo f) return f.GetValue(instance);
                if (m is PropertyInfo p && p.GetGetMethod(true) != null) return p.GetValue(instance, null);
            }
            catch { }
            return null;
        }

        public string DumpDiagnostics()
        {
            var s = new StringBuilder();
            s.AppendLine("Stonewards Quest/Mission API diagnostics v0.1.0");
            s.AppendLine("Date UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            s.AppendLine("Types discovered: " + _questTypes.Length);
            s.AppendLine("Report includes type/member names and shallow quest state; it does not modify data.");
            s.AppendLine();
            foreach (Type t in _questTypes)
            {
                s.AppendLine("TYPE " + t.FullName + " : " + t.BaseType?.Name);
                foreach (MemberInfo m in Members(t).OrderBy(m => m.Name))
                {
                    Type mt = MemberType(m);
                    s.AppendLine("  " + (IsStatic(m) ? "static " : "       ") +
                        (m is FieldInfo ? "field " : "prop  ") +
                        (mt == null ? "?" : mt.Name) + " " + m.Name);
                }
                if (IsRootType(t))
                {
                    foreach (MemberInfo m in Members(t))
                    {
                        if (!IsStatic(m) || !IsStateMember(m.Name)) continue;
                        var value = GetValue(m, null);
                        if (value != null) Describe(s, "  STATE " + m.Name, value, 0, new HashSet<object>(ReferenceComparer.Instance));
                    }
                    if (typeof(Component).IsAssignableFrom(t) &&
                        (Contains(t.Name, "manager") || Contains(t.Name, "controller") ||
                         Contains(t.Name, "menu") || Contains(t.Name, "system") ||
                         Contains(t.Name, "tracker")))
                    {
                        UnityEngine.Object[] found;
                        try { found = Resources.FindObjectsOfTypeAll(t); } catch { found = null; }
                        if (found != null)
                        {
                            int count = 0;
                            foreach (var item in found)
                            {
                                var cmp = item as Component;
                                if (cmp == null || !cmp.gameObject.scene.IsValid()) continue;
                                Describe(s, "  SCENE", item, 0, new HashSet<object>(ReferenceComparer.Instance));
                                if (++count >= 2) break;
                            }
                        }
                    }
                }
                s.AppendLine();
                if (s.Length > 500000) { s.AppendLine("[Report truncated]"); break; }
            }
            string path = Path.Combine(Paths.ConfigPath, "StonewardsDailyQuestTracker-diagnostic.txt");
            File.WriteAllText(path, s.ToString(), Encoding.UTF8);
            return path;
        }

        private void Describe(StringBuilder s, string label, object value, int level, HashSet<object> visited)
        {
            if (value == null || level > 3 || s.Length > 450000) return;
            Type t = value.GetType();
            if (value is string str) { s.AppendLine(label + " = \"" + Short(str) + "\""); return; }
            if (t.IsPrimitive || t.IsEnum || value is decimal)
            { s.AppendLine(label + " = " + value); return; }
            if (!t.IsValueType && !visited.Add(value)) return;
            s.AppendLine(label + " -> " + t.FullName);
            if (value is IEnumerable items)
            {
                try
                {
                    int count = 0;
                    foreach (object item in items)
                    {
                        if (++count > 12) break;
                        Describe(s, label + "[" + (count - 1) + "]", item, level + 1, visited);
                    }
                }
                catch { }
                return;
            }
            int fields = 0;
            foreach (MemberInfo m in Members(t))
            {
                if (IsStatic(m) || !IsStateMember(m.Name) &&
                    !Contains(m.Name, "name") && !Contains(m.Name, "title") &&
                    !Contains(m.Name, "target") && !Contains(m.Name, "required") &&
                    !Contains(m.Name, "count") && !Contains(m.Name, "amount") &&
                    !Contains(m.Name, "complete") && !Contains(m.Name, "type") &&
                    !Contains(m.Name, "category") && !Contains(m.Name, "id")) continue;
                if (++fields > 30) break;
                object child = GetValue(m, value);
                if (child == null) continue;
                string next = label + "." + m.Name;
                if (level < 3 && (IsStateMember(m.Name) || child is IEnumerable && !(child is string)))
                    Describe(s, next, child, level + 1, visited);
                else if (child is string || child.GetType().IsPrimitive || child is Enum)
                    s.AppendLine(next + " = " + Short(child.ToString()));
                else s.AppendLine(next + " -> " + child.GetType().Name);
            }
        }
        private static string Short(string s) => s != null && s.Length > 150 ? s.Substring(0, 150) : s;

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
