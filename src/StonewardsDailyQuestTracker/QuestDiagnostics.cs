using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsDailyQuestTracker
{
    /// <summary>
    /// One-shot, bounded quest API and Missions window snapshot. Never called
    /// from the periodic progress polling path. No gameplay state is modified.
    /// </summary>
    internal static class QuestDiagnostics
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;
        private const int Limit = 230000;
        private static readonly string ReportName =
            "StonewardsDailyQuestTracker-diagnostic.txt";

        public static string Export(string trigger)
        {
            string directory = Paths.ConfigPath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, ReportName);
            var output = new StringBuilder(36000);
            output.AppendLine("Stonewards Daily Quest Tracker - API report v0.1.2");
            output.AppendLine("Triggered by: " + trigger);
            output.AppendLine("Timestamp (UTC): " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            output.AppendLine("BepInEx config directory: " + directory);
            output.AppendLine("GlobalMissionMenu.Instance: " +
                (GlobalMissionMenu.Instance != null ? "available" : "missing"));
            output.AppendLine("GlobalMissionMenu.IsOpen: " +
                (GlobalMissionMenu.Instance != null && GlobalMissionMenu.Instance.IsOpen));
            output.AppendLine("This diagnostic contains runtime quest values and UI labels. Review before sharing.");
            output.AppendLine();

            AppendGameTypes(output);
            AppendMissionMenu(output);

            File.WriteAllText(path, output.ToString(), Encoding.UTF8);
            return path;
        }

        private static void AppendGameTypes(StringBuilder output)
        {
            output.AppendLine("=== Quest/Mission-related game types ===");
            Type[] types;
            try { types = typeof(FirstPersonController).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error)
            {
                types = error.Types.Where(type => type != null).ToArray();
            }
            foreach (Type type in types.Where(type => type != null &&
                (Matches(type.Name) || ContainsDailyMember(type)))
                .OrderBy(type => type.Name))
            {
                if (output.Length >= Limit) break;
                output.AppendLine("TYPE: " + type.FullName +
                                  " : " + (type.BaseType == null ? "<none>" : type.BaseType.Name));
                FieldInfo[] fields;
                PropertyInfo[] properties;
                MethodInfo[] methods;
                try
                {
                    fields = type.GetFields(Flags | BindingFlags.DeclaredOnly);
                    properties = type.GetProperties(Flags | BindingFlags.DeclaredOnly);
                    methods = type.GetMethods(Flags | BindingFlags.DeclaredOnly);
                }
                catch (Exception ex)
                {
                    output.AppendLine("  Reflection error: " + ex.GetType().Name);
                    continue;
                }

                foreach (FieldInfo field in fields.Take(90))
                    output.AppendLine("  FIELD " + (field.IsStatic ? "static " : "") +
                        DescribeType(field.FieldType) + " " + field.Name);
                foreach (PropertyInfo prop in properties.Take(60))
                {
                    if (prop.GetIndexParameters().Length != 0) continue;
                    output.AppendLine("  PROPERTY " + DescribeType(prop.PropertyType) + " " + prop.Name);
                }
                foreach (MethodInfo method in methods.Where(m =>
                    HasToken(m.Name, "daily", "quest", "mission", "progress", "refresh",
                        "complete", "load", "save", "update")).Take(45))
                {
                    string parameters;
                    try
                    {
                        parameters = string.Join(", ", method.GetParameters()
                            .Select(p => DescribeType(p.ParameterType) + " " + p.Name));
                    }
                    catch { parameters = "?"; }
                    output.AppendLine("  METHOD " + DescribeType(method.ReturnType) + " " +
                        method.Name + "(" + parameters + ")");
                }
                output.AppendLine();
            }
        }

        private static void AppendMissionMenu(StringBuilder output)
        {
            output.AppendLine("=== Live GlobalMissionMenu data ===");
            var menu = GlobalMissionMenu.Instance;
            if (menu == null)
            {
                output.AppendLine("No GlobalMissionMenu instance. Open Missions -> Daily and try again.");
                return;
            }

            var visited = new HashSet<object>(ReferenceComparer.Instance);
            DumpObject(output, "GlobalMissionMenu", menu, 0, visited, 0);

            output.AppendLine();
            output.AppendLine("=== UI Toolkit mission labels ===");
            var roots = new List<VisualElement>();
            foreach (FieldInfo field in GetAllFields(menu.GetType()))
            {
                if (field.IsStatic) continue;
                object value = SafeGet(field, menu);
                var element = value as VisualElement;
                if (element != null && !roots.Contains(element))
                    roots.Add(element);
                var document = value as UIDocument;
                if (document != null && document.rootVisualElement != null &&
                    !roots.Contains(document.rootVisualElement))
                    roots.Add(document.rootVisualElement);
            }
            if (roots.Count == 0)
                output.AppendLine("No direct VisualElement/UIDocument fields found on GlobalMissionMenu.");
            int totalElements = 0;
            foreach (VisualElement root in roots.Take(5))
                DumpVisualTree(output, root, 0, ref totalElements);
        }

        private static void DumpObject(StringBuilder output, string path, object value,
            int depth, HashSet<object> visited, int nodes)
        {
            if (value == null || depth > 3 || nodes > 80 || output.Length >= Limit) return;
            Type type = value.GetType();
            output.AppendLine(path + " : " + DescribeType(type));
            if (IsSimple(type))
            {
                output.AppendLine("    VALUE = " + SafeString(value));
                return;
            }
            if (!type.IsValueType && !visited.Add(value)) return;
            if (value is IEnumerable enumerable && !(value is string))
            {
                try
                {
                    int i = 0;
                    foreach (object element in enumerable)
                    {
                        if (i >= 12) { output.AppendLine("  [remaining entries omitted]"); break; }
                        DumpObject(output, path + "[" + i + "]", element, depth + 1, visited, nodes + i + 1);
                        i++;
                    }
                }
                catch (Exception ex) { output.AppendLine("  Enumeration error: " + ex.GetType().Name); }
                return;
            }

            int scanned = 0;
            foreach (FieldInfo field in GetAllFields(type))
            {
                if (field.IsStatic || ++scanned > 55) continue;
                object child = SafeGet(field, value);
                if (child == null) continue;
                string label = path + "." + field.Name;
                Type childType = child.GetType();
                if (IsSimple(childType))
                {
                    output.AppendLine("  " + label + " = " + SafeString(child));
                    continue;
                }
                if (child is UnityEngine.Object unityObject &&
                    !(child is ScriptableObject) && !(child is VisualElement) &&
                    !(child is GlobalMissionMenu))
                {
                    output.AppendLine("  " + label + " : " + DescribeType(childType));
                    continue;
                }
                if (child is VisualElement)
                {
                    var text = child as TextElement;
                    output.AppendLine("  " + label + " : VisualElement (" +
                        (text == null ? "" : "text=" + SafeString(text.text)) + ")");
                    continue;
                }
                if (child is ScriptableObject)
                {
                    output.AppendLine("  " + label + " : ScriptableObject " + DescribeType(childType));
                    continue;
                }
                if (IsRelevant(field.Name, childType) && depth < 3)
                    DumpObject(output, label, child, depth + 1, visited, nodes + scanned);
                else
                    output.AppendLine("  " + label + " : " + DescribeType(childType));
                if (output.Length >= Limit) break;
            }
        }

        private static void DumpVisualTree(StringBuilder output, VisualElement element, int depth,
            ref int total)
        {
            if (element == null || depth > 8 || ++total > 300 || output.Length >= Limit) return;
            var textElement = element as TextElement;
            var progress = element as ProgressBar;
            string text = textElement == null ? "" : " text=" + SafeString(textElement.text);
            string progressValue = progress == null ? "" :
                " progress=" + progress.value.ToString(CultureInfo.InvariantCulture) +
                " low=" + progress.lowValue.ToString(CultureInfo.InvariantCulture) +
                " high=" + progress.highValue.ToString(CultureInfo.InvariantCulture);
            output.AppendLine(new string(' ', depth * 2) + DescribeType(element.GetType()) +
                " name=" + SafeString(element.name) + text + progressValue);
            int count = Math.Min(element.childCount, 60);
            for (int i = 0; i < count; i++)
            {
                if (total >= 300 || output.Length >= Limit) break;
                DumpVisualTree(output, element.ElementAt(i), depth + 1, ref total);
            }
        }

        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            int depth = 0;
            while (type != null && depth++ < 7)
            {
                FieldInfo[] fields;
                try { fields = type.GetFields(Flags | BindingFlags.DeclaredOnly); }
                catch { yield break; }
                foreach (FieldInfo field in fields)
                    yield return field;
                type = type.BaseType;
            }
        }

        private static object SafeGet(FieldInfo field, object value)
        {
            try { return field.GetValue(value); }
            catch { return null; }
        }

        private static bool ContainsDailyMember(Type type)
        {
            try { return type.GetFields(Flags).Any(f => HasToken(f.Name, "dailyquest", "dailymission")); }
            catch { return false; }
        }

        private static bool Matches(string name) =>
            HasToken(name, "quest", "mission", "daily");

        private static bool IsRelevant(string name, Type type) =>
            HasToken(name, "quest", "mission", "daily", "progress", "active", "current",
                "entry", "entries", "task", "objective", "data", "list", "items") ||
            Matches(type.Name) || typeof(IList).IsAssignableFrom(type);

        private static bool IsSimple(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(string) ||
            type == typeof(decimal) || type == typeof(DateTime);

        private static bool HasToken(string value, params string[] tokens)
        {
            foreach (string token in tokens)
                if (value != null &&
                    value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static string DescribeType(Type type) =>
            type == null ? "?" : (type.FullName ?? type.Name);

        private static string SafeString(object value)
        {
            if (value == null) return "<null>";
            string text;
            try { text = Convert.ToString(value, CultureInfo.InvariantCulture); }
            catch { return "<format-error>"; }
            if (text == null) return "<null>";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length > 150 ? text.Substring(0, 150) + "..." : text;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
