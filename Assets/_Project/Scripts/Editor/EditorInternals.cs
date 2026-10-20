using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectX
{
    /// <summary>Reflection helpers for reaching Unity's internal editor objects (previews, timelines).</summary>
    static class EditorInternals
    {
        public const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Type EditorType(string fullName) => typeof(Editor).Assembly.GetType(fullName);

        public static object Get(object owner, string member)
        {
            var type = owner.GetType();
            var field = type.GetField(member, Instance);
            if (field != null)
                return field.GetValue(owner);
            return type.GetProperty(member, Instance)?.GetValue(owner);
        }

        public static void Set(object owner, string member, object value)
        {
            var type = owner.GetType();
            var field = type.GetField(member, Instance);
            if (field != null)
                field.SetValue(owner, value);
            else
                type.GetProperty(member, Instance)?.SetValue(owner, value);
        }

        public static object Call(object owner, string method, params object[] args)
        {
            var types = Array.ConvertAll(args, a => a.GetType());
            return owner.GetType().GetMethod(method, Instance, null, types, null)?.Invoke(owner, args);
        }

        public static bool IsInspector(EditorWindow window)
        {
            for (var type = window?.GetType(); type != null; type = type.BaseType)
                if (type.Name == "InspectorWindow")
                    return true;
            return false;
        }

        /// <summary>Objects of <paramref name="type"/> held by any live Editor, directly or up to <paramref name="depth"/> helper objects down.</summary>
        public static IEnumerable<object> FindInEditors(Type type, int depth)
        {
            if (type == null)
                yield break;
            foreach (var editor in Resources.FindObjectsOfTypeAll<Editor>())
                foreach (var found in Find(editor, type, depth))
                    yield return found;
        }

        static IEnumerable<object> Find(object owner, Type type, int depth)
        {
            for (var t = owner.GetType(); t != null && t != typeof(ScriptableObject); t = t.BaseType)
                foreach (var field in t.GetFields(Instance | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.IsValueType || field.FieldType == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                        continue;
                    var value = field.GetValue(owner);
                    if (value == null)
                        continue;
                    if (type.IsInstanceOfType(value))
                        yield return value;
                    else if (depth > 0 && !(value is IEnumerable) && !(value is Delegate))
                        foreach (var nested in Find(value, type, depth - 1))
                            yield return nested;
                }
        }
    }
}
