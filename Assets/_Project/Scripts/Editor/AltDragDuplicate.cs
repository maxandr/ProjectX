using UnityEditor;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Unreal-style Alt-drag duplicate: Alt + left-drag on a transform handle in the Scene view duplicates the
    /// selection (like Ctrl+D) and drags the copies, leaving the originals in place. The whole action is one undo
    /// step. Alt + left-drag anywhere else still orbits the view.
    /// </summary>
    [InitializeOnLoad]
    static class AltDragDuplicate
    {
        static bool dragging;
        static int undoGroup;

        static AltDragDuplicate()
        {
            SceneView.beforeSceneGui += OnSceneGui;
        }

        static void OnSceneGui(SceneView view)
        {
            Event e = Event.current;
            // A mouse-up that never reached the Scene view (button released elsewhere) must not leave Alt stripped.
            if (dragging && (e.type == EventType.MouseMove || (e.type == EventType.MouseDown && GUIUtility.hotControl == 0)))
                dragging = false;
            if (!dragging)
            {
                if (e.type != EventType.MouseDown || e.button != 0 || !e.alt || !CanStart())
                    return;
                dragging = true;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Alt-Drag Duplicate");
                undoGroup = Undo.GetCurrentGroup();
                Unsupported.DuplicateGameObjectsUsingPasteboard();
            }

            // Hide Alt from the Scene view so the handle takes the drag instead of the orbit tool.
            e.modifiers &= ~EventModifiers.Alt;

            if (e.rawType == EventType.MouseUp && e.button == 0)
            {
                dragging = false;
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        // The mouse must be on a handle (nearestControl comes from the Layout pass before this MouseDown).
        static bool CanStart() =>
            GUIUtility.hotControl == 0
            && HandleUtility.nearestControl != 0
            && Selection.transforms.Length > 0
            && Tools.current is Tool.Move or Tool.Rotate or Tool.Scale or Tool.Transform;
    }
}
