using System;
using UnityEditor;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Grouped, foldable layout of the ProjectX/Water material properties. Shared by the material inspector
    /// (<see cref="WaterShaderGUI"/>) and the water object's inspector (<see cref="WaterSurfaceEditor"/>).
    /// </summary>
    static class WaterInspector
    {
        public const string RippleSection = "Character Ripples";
        public const string ShoreSection = "Shore Glow";

        struct Prop
        {
            public string name, label, tooltip, showIf;

            public Prop(string name, string label, string tooltip = null, string showIf = null)
            {
                this.name = name;
                this.label = label;
                this.tooltip = tooltip;
                this.showIf = showIf;
            }
        }

        static readonly (string title, Prop[] props)[] sections =
        {
            ("Color & Clarity", new[]
            {
                new Prop("_BaseColor", "Deep Color", "Water color where the bottom is no longer visible."),
                new Prop("_ShallowColor", "Shallow Tint", "Tint of the bottom seen through the water."),
                new Prop("_Absorption", "Murkiness", "How quickly the water loses clarity with depth."),
                new Prop("_Refraction", "Refraction", "How much waves distort the bottom."),
            }),
            ("Reflection", new[]
            {
                new Prop("_ReflectionStrength", "Strength"),
                new Prop("_FresnelPower", "Grazing Falloff", "Higher keeps the reflection strong only near the horizon."),
                new Prop("_F0", "Top-Down Reflectance", "Reflection strength when looking straight down into the water."),
                new Prop("_Smoothness", "Smoothness", "Sharpness of the sky reflection and sun glints."),
                new Prop("_Specular", "Sun Glint"),
                new Prop("_SSR", "Reflect Scene Objects", "Reflects characters, rocks and hills (from what is on screen)."),
                new Prop("_SSRDistance", "Reflection Distance", showIf: "_SSR"),
                new Prop("_SSRThickness", "Object Thickness", "Higher fills holes in the reflection but may add false hits.", "_SSR"),
            }),
            ("Wind Waves", new[]
            {
                new Prop("_WaveStrength", "Strength"),
                new Prop("_WaveScale", "Frequency", "Higher makes smaller waves."),
                new Prop("_WaveSpeed", "Speed"),
            }),
            (RippleSection, new[]
            {
                new Prop("_RippleStrength", "Visibility", "Look only; the wave simulation itself is set below."),
            }),
            ("Shallow Foam", new[]
            {
                new Prop("_FoamColor", "Color (A = amount)"),
                new Prop("_FoamDistance", "Foam Depth", "Water shallower than this against the bottom or objects gets foam."),
            }),
            (ShoreSection, new[]
            {
                new Prop("_ShoreColor", "Color & Intensity", "HDR: intensity above 1 makes it glow."),
                new Prop("_ShoreLine", "Contact Line Width"),
                new Prop("_ShoreRingCount", "Ring Count"),
                new Prop("_ShoreRingWidth", "Ring Width"),
                new Prop("_ShoreRingSpeed", "Ring Speed", "Negative moves the rings inward."),
                new Prop("_ShoreWobble", "Wave Wobble"),
                new Prop("_ShoreGain", "Thin Object Boost", "Raise if reeds barely glow."),
            }),
        };

        static MaterialProperty Find(MaterialProperty[] properties, string name) =>
            Array.Find(properties, p => p.name == name);

        /// <param name="drawExtra">Called inside each open section with its title, to append non-material settings.</param>
        public static void Draw(MaterialEditor editor, MaterialProperty[] properties, Action<string> drawExtra = null)
        {
            foreach (var (title, props) in sections)
            {
                string key = "ProjectX.Water." + title;
                bool open = EditorGUILayout.BeginFoldoutHeaderGroup(SessionState.GetBool(key, true), title);
                SessionState.SetBool(key, open);
                if (open)
                {
                    EditorGUI.indentLevel++;
                    foreach (var p in props)
                    {
                        var property = Find(properties, p.name);
                        if (property == null)
                            continue;
                        if (p.showIf != null)
                        {
                            var condition = Find(properties, p.showIf);
                            if (condition != null && condition.floatValue < 0.5f)
                                continue;
                        }
                        editor.ShaderProperty(property, new GUIContent(p.label, p.tooltip));
                    }
                    drawExtra?.Invoke(title);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(4);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }
    }

    class WaterShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            WaterInspector.Draw(materialEditor, properties);
        }
    }
}
