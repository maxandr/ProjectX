using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Space in the Inspector (or the detached preview) toggles play/pause of the animation preview, like the ▶
    /// button: animation clips, Animator states and transitions. Unity has no hotkey for it, so this reaches the
    /// internal AvatarPreview.timeControl through reflection.
    /// </summary>
    [InitializeOnLoad]
    static class SpacePlaysPreview
    {
        static SpacePlaysPreview()
        {
            var handler = typeof(EditorApplication).GetField("globalEventHandler", BindingFlags.Static | BindingFlags.NonPublic);
            if (handler == null)
                return;
            var callback = (EditorApplication.CallbackFunction)handler.GetValue(null);
            handler.SetValue(null, callback + OnGlobalEvent);
        }

        static void OnGlobalEvent()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode != KeyCode.Space || e.modifiers != EventModifiers.None)
                return;
            if (EditorGUIUtility.editingTextField || !EditorInternals.IsInspector(EditorWindow.focusedWindow))
                return;

            bool toggled = false;
            foreach (var preview in EditorInternals.FindInEditors(EditorInternals.EditorType("UnityEditor.AvatarPreview"), 2))
            {
                var timeControl = EditorInternals.Get(preview, "timeControl");
                if (timeControl == null)
                    continue;
                EditorInternals.Set(timeControl, "playing", !(bool)EditorInternals.Get(timeControl, "playing"));
                toggled = true;
            }

            if (!toggled)
                return;
            e.Use();
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
