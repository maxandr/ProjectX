using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectX
{
    /// <summary>
    /// Click or drag on the time ruler of an Animator transition's timeline (in the Inspector) to move the preview
    /// there. Unity only lets you drag the small playhead marker and pans the view on the rest of the ruler; the
    /// transition's own handles (the blue range and its thumbs) still edit the transition as before.
    /// </summary>
    [InitializeOnLoad]
    static class TransitionTimelineScrub
    {
        const float RulerHeight = 18f; // TimelineControl's header row
        const float ThumbWidth = 9f;

        static readonly System.Type TransitionPreviewType = EditorInternals.EditorType("UnityEditor.TransitionPreview");

        static IMGUIContainer container;
        static object timeline;
        static object timeControl;

        static TransitionTimelineScrub()
        {
            if (TransitionPreviewType == null)
                return;
            EditorApplication.delayCall += HookInspectors;
            Selection.selectionChanged += HookInspectors;
        }

        /// <summary>Registering the same callbacks again is ignored, so new Inspector windows just get hooked too.</summary>
        static void HookInspectors()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (!EditorInternals.IsInspector(window))
                    continue;
                var root = window.rootVisualElement;
                root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                root.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            }
        }

        static void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !TryStart(evt.target as VisualElement, evt.position))
                return;
            // Captured pointer events go straight to the container, so listen there as well as on the root.
            container.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            container.RegisterCallback<PointerUpEvent>(OnPointerUp);
            container.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            container.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        // Keeps IMGUI from also seeing the click as a pan of the ruler.
        static void OnMouseDown(MouseDownEvent evt)
        {
            if (container != null)
                evt.StopImmediatePropagation();
        }

        static void OnPointerMove(PointerMoveEvent evt)
        {
            if (container == null)
                return;
            if ((evt.pressedButtons & 1) == 0) // released somewhere the up event never reached
                Stop(evt.pointerId);
            else
                Scrub(container.WorldToLocal(evt.position));
            evt.StopImmediatePropagation();
        }

        static void OnPointerUp(PointerUpEvent evt)
        {
            if (container == null || evt.button != 0)
                return;
            Stop(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        static void OnCaptureOut(PointerCaptureOutEvent evt) => Stop(evt.pointerId);

        static void Stop(int pointerId)
        {
            if (container == null)
                return;
            var imgui = container;
            container = null;
            timeline = null;
            timeControl = null;
            imgui.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            imgui.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            imgui.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            if (imgui.HasPointerCapture(pointerId))
                imgui.ReleasePointer(pointerId);
        }

        static bool TryStart(VisualElement target, Vector2 position)
        {
            var imgui = target as IMGUIContainer ?? target?.GetFirstAncestorOfType<IMGUIContainer>();
            if (imgui == null)
                return false;
            Vector2 mouse = imgui.WorldToLocal(position);

            foreach (var preview in EditorInternals.FindInEditors(TransitionPreviewType, 1))
            {
                var line = EditorInternals.Get(preview, "m_Timeline");
                var avatar = EditorInternals.Get(preview, "m_AvatarPreview");
                if (line == null || avatar == null)
                    continue;
                var rect = (Rect)EditorInternals.Get(line, "m_Rect");
                if (!new Rect(rect.x, rect.y, rect.width, RulerHeight).Contains(mouse) || OnTransitionHandles(line, rect, mouse.x - rect.x))
                    continue;

                container = imgui;
                timeline = line;
                timeControl = EditorInternals.Get(avatar, "timeControl");
                EditorInternals.Set(timeControl, "playing", false);
                Scrub(mouse);
                return true;
            }
            return false;
        }

        static bool OnTransitionHandles(object line, Rect rect, float x)
        {
            var timeArea = EditorInternals.Get(line, "m_TimeArea");
            var local = new Rect(0f, 0f, rect.width, rect.height);
            float start = (float)EditorInternals.Call(timeArea, "TimeToPixel", (float)EditorInternals.Get(line, "TransitionStartTime"), local);
            float stop = (float)EditorInternals.Call(timeArea, "TimeToPixel", (float)EditorInternals.Get(line, "TransitionStopTime"), local);
            return x > start - ThumbWidth && x < stop + ThumbWidth;
        }

        static void Scrub(Vector2 mouse)
        {
            var rect = (Rect)EditorInternals.Get(timeline, "m_Rect");
            var timeArea = EditorInternals.Get(timeline, "m_TimeArea");
            float time = (float)EditorInternals.Call(timeArea, "PixelToTime", mouse.x - rect.x, new Rect(0f, 0f, rect.width, rect.height));
            float stopTime = (float)EditorInternals.Get(timeControl, "stopTime");
            EditorInternals.Set(timeControl, "currentTime", Mathf.Clamp(time, 0f, stopTime));
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
