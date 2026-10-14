using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Puts every water setting on the water object itself: its material, grouped (<see cref="WaterInspector"/>),
    /// plus the scene-wide ripple and shore-glow settings from <see cref="WaterRipples"/> / <see cref="WaterContactFoam"/>.
    /// </summary>
    [CustomEditor(typeof(WaterSurface))]
    class WaterSurfaceEditor : Editor
    {
        MaterialEditor materialEditor;

        void OnDisable()
        {
            if (materialEditor != null)
                DestroyImmediate(materialEditor);
        }

        public override void OnInspectorGUI()
        {
            var material = ((WaterSurface)target).GetComponent<Renderer>().sharedMaterial;
            if (material == null || material.shader.name != "ProjectX/Water")
            {
                EditorGUILayout.HelpBox("This object's material does not use the ProjectX/Water shader.", MessageType.Warning);
                return;
            }

            if (materialEditor == null || materialEditor.target != material)
            {
                if (materialEditor != null)
                    DestroyImmediate(materialEditor);
                materialEditor = (MaterialEditor)CreateEditor(material);
            }

            var sharedWith = FindObjectsByType<WaterSurface>(FindObjectsSortMode.None)
                .Where(w => w != target && w.GetComponent<Renderer>().sharedMaterial == material)
                .Select(w => w.name)
                .ToArray();
            string info = $"Material: {material.name}.";
            if (sharedWith.Length > 0)
                info += $" Changes also apply to: {string.Join(", ", sharedWith)}.";
            info += "\nMaterial settings persist after Play Mode; the scene-wide settings in the sections below revert as usual.";
            EditorGUILayout.HelpBox(info, MessageType.None);

            WaterInspector.Draw(materialEditor, MaterialEditor.GetMaterialProperties(new Object[] { material }), DrawSceneSettings);
        }

        static void DrawSceneSettings(string section)
        {
            if (section == WaterInspector.RippleSection)
            {
                DrawComponent<WaterRipples>(so =>
                {
                    EditorGUILayout.PropertyField(so.FindProperty("propagation"),
                        new GUIContent("Wave Speed", "How fast the rings spread (scene-wide)."));
                    EditorGUILayout.PropertyField(so.FindProperty("damping"),
                        new GUIContent("Damping", "Closer to 1 makes the rings last longer (scene-wide)."));
                    EditorGUILayout.PropertyField(so.FindProperty("wakeHeight"),
                        new GUIContent("Wake Height", "Strength of the rings left while wading (scene-wide)."));
                    EditorGUILayout.PropertyField(so.FindProperty("wakeRadius"),
                        new GUIContent("Wake Size (m)", "Size of each wake ring (scene-wide)."));
                    EditorGUILayout.PropertyField(so.FindProperty("wakeSpacing"),
                        new GUIContent("Wake Spacing (m)", "Distance walked between wake rings (scene-wide)."));
                    EditorGUILayout.PropertyField(so.FindProperty("splashHeight"),
                        new GUIContent("Splash Height", "Strength of the splash on jumping in or landing (scene-wide)."));
                    EditorGUILayout.PropertyField(so.FindProperty("splashRadius"),
                        new GUIContent("Splash Size (m)", "Size of the splash (scene-wide)."));
                });
            }
            else if (section == WaterInspector.ShoreSection)
            {
                DrawComponent<WaterContactFoam>(so =>
                {
                    EditorGUILayout.PropertyField(so.FindProperty("spread"),
                        new GUIContent("Glow Spread (m)", "How far the rings reach from an object (scene-wide)."));
                });
            }
        }

        static void DrawComponent<T>(System.Action<SerializedObject> draw) where T : Component
        {
            var component = FindAnyObjectByType<T>();
            if (component == null)
            {
                EditorGUILayout.HelpBox($"No {typeof(T).Name} in the scene. Add it to the Water Ripples object.", MessageType.Info);
                return;
            }
            var so = new SerializedObject(component);
            so.Update();
            draw(so);
            so.ApplyModifiedProperties();
        }
    }
}
