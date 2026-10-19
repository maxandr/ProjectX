using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Unreal-style temporary pivot: Alt + middle-mouse drag in the Scene view puts a pivot under the
    /// cursor (snapping to the nearest vertex, else the surface), and the Move/Rotate/Scale tools then
    /// work around it. Nothing on the object changes. The pivot follows the active object and clears
    /// when the selection changes or on Alt+Z.
    /// </summary>
    [InitializeOnLoad]
    static class TempPivot
    {
        const float VertexSnapPixels = 14f;
        static readonly Color MarkerColor = new(1f, 0.55f, 0.1f);

        static Transform owner;
        static Vector3 localPivot;
        static bool active;
        static int dragControl;
        static Vector3 scaleHandle = Vector3.one;

        static TempPivot()
        {
            SceneView.beforeSceneGui += PickPivot;
            SceneView.duringSceneGui += DrawHandles;
            Selection.selectionChanged += Clear;
        }

        [Shortcut("ProjectX/Clear Temp Pivot", typeof(SceneView), KeyCode.Z, ShortcutModifiers.Alt)]
        static void Clear()
        {
            if (!active)
                return;
            active = false;
            owner = null;
            Tools.hidden = false;
            SceneView.RepaintAll();
        }

        static Vector3 WorldPivot => owner.TransformPoint(localPivot);

        // Runs before the Scene view's own camera controls so Alt+MMB doesn't pan the view.
        static void PickPivot(SceneView view)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 2 && e.alt && Selection.activeTransform:
                    dragControl = id;
                    GUIUtility.hotControl = id;
                    SetPivot(e.mousePosition);
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == dragControl && dragControl != 0:
                    SetPivot(e.mousePosition);
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == dragControl && dragControl != 0:
                    GUIUtility.hotControl = 0;
                    dragControl = 0;
                    e.Use();
                    break;
            }
        }

        static void SetPivot(Vector2 mouse)
        {
            Vector3 point;
            if (HandleUtility.FindNearestVertex(mouse, out Vector3 vertex)
                && Vector2.Distance(HandleUtility.WorldToGUIPoint(vertex), mouse) <= VertexSnapPixels)
                point = vertex;
            else if (HandleUtility.PlaceObject(mouse, out Vector3 hit, out _))
                point = hit;
            else
            {
                // Nothing under the cursor: slide along the camera-facing plane through the current pivot.
                Vector3 through = active ? WorldPivot : Selection.activeTransform.position;
                Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
                var plane = new Plane(-ray.direction, through);
                if (!plane.Raycast(ray, out float distance))
                    return;
                point = ray.GetPoint(distance);
            }

            owner = Selection.activeTransform;
            localPivot = owner.InverseTransformPoint(point);
            active = true;
            SceneView.RepaintAll();
        }

        static void DrawHandles(SceneView view)
        {
            if (!active || !owner)
            {
                if (active)
                    Clear();
                return;
            }

            Vector3 pivot = WorldPivot;
            float size = HandleUtility.GetHandleSize(pivot);
            Handles.color = MarkerColor;
            Handles.SphereHandleCap(0, pivot, Quaternion.identity, size * 0.12f, EventType.Repaint);

            bool ours = Tools.current is Tool.Move or Tool.Rotate or Tool.Scale;
            Tools.hidden = ours;
            if (!ours)
                return;

            Quaternion rotation = Tools.pivotRotation == PivotRotation.Local ? owner.rotation : Quaternion.identity;
            Transform[] targets = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);

            switch (Tools.current)
            {
                case Tool.Move:
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 moved = Handles.PositionHandle(pivot, rotation);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObjects(targets, "Move Around Temp Pivot");
                            Vector3 delta = moved - pivot;
                            foreach (Transform t in targets)
                                t.position += delta;
                        }
                        break;
                    }
                case Tool.Rotate:
                    {
                        EditorGUI.BeginChangeCheck();
                        Quaternion rotated = Handles.RotationHandle(rotation, pivot);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObjects(targets, "Rotate Around Temp Pivot");
                            Quaternion delta = rotated * Quaternion.Inverse(rotation);
                            foreach (Transform t in targets)
                            {
                                t.position = pivot + delta * (t.position - pivot);
                                t.rotation = delta * t.rotation;
                            }
                        }
                        break;
                    }
                case Tool.Scale:
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 scaled = Handles.ScaleHandle(scaleHandle, pivot, rotation, size);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObjects(targets, "Scale Around Temp Pivot");
                            Vector3 ratio = Divide(scaled, scaleHandle);
                            Quaternion inverse = Quaternion.Inverse(rotation);
                            foreach (Transform t in targets)
                            {
                                Vector3 offset = Vector3.Scale(inverse * (t.position - pivot), ratio);
                                t.position = pivot + rotation * offset;
                                // Exact for uniform scale, or per-axis in Local mode on the active object.
                                t.localScale = Vector3.Scale(t.localScale, ratio);
                            }
                            scaleHandle = scaled;
                        }
                        if (GUIUtility.hotControl == 0)
                            scaleHandle = Vector3.one;
                        break;
                    }
            }
        }

        static Vector3 Divide(Vector3 a, Vector3 b) => new(
            Mathf.Approximately(b.x, 0f) ? 1f : a.x / b.x,
            Mathf.Approximately(b.y, 0f) ? 1f : a.y / b.y,
            Mathf.Approximately(b.z, 0f) ? 1f : a.z / b.z);
    }
}
