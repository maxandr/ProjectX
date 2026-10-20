using UnityEngine;

namespace ProjectX
{
    /// <summary>Debug: lists the cheat hotkeys in faint text in the top-right corner of the screen.</summary>
    public class CheatsOverlay : MonoBehaviour
    {
        [Range(0f, 1f)] public float opacity = 0.45f;
        [Tooltip("Font size as a fraction of the screen height.")]
        public float fontSize = 0.022f;
        public float margin = 16f;

        GUIStyle style, shadowStyle;
        string text;
        float shownScale = -1f;
        int shownCheckpoints = -1;

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EnsureStyles();
            RefreshText();

            var rect = new Rect(margin, margin, Screen.width - 2f * margin, Screen.height - 2f * margin);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
            GUI.Label(rect, text, style);
        }

        void EnsureStyles()
        {
            int size = Mathf.Max(10, Mathf.RoundToInt(Screen.height * fontSize));
            if (style != null && style.fontSize == size)
                return;

            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = size, wordWrap = false };
            style.normal.textColor = new Color(1f, 1f, 1f, opacity);
            shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, opacity * 0.6f);
        }

        // Rebuilds the string only when what it shows changes, so OnGUI doesn't allocate every frame.
        void RefreshText()
        {
            float scale = Time.timeScale;
            int checkpoints = Checkpoint.Count;
            if (text != null && Mathf.Approximately(scale, shownScale) && checkpoints == shownCheckpoints)
                return;

            shownScale = scale;
            shownCheckpoints = checkpoints;
            string warp = checkpoints > 1 ? $"1–{Mathf.Min(checkpoints, 9)}" : "1";
            text = "CHEATS\n"
                 + $"{warp}  warp to checkpoint\n"
                 + $"Wheel  time scale ({scale:0.00}x)\n"
                 + "Wheel click  time 1x\n"
                 + "F  control the Fox (A/D, Space)\n"
                 + "R  restart game";
        }
    }
}
