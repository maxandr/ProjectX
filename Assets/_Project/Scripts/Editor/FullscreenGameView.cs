using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Menu ProjectX/Fullscreen Game View (F11): opens a borderless Game view without its toolbar over the whole
    /// monitor the editor is on; F11 again closes it. Unity has no built-in fullscreen Game view in the editor.
    /// </summary>
    static class FullscreenGameView
    {
        const string MenuPath = "ProjectX/Fullscreen Game View _F11";
        // Marks our popup so F11 still finds it after a domain reload has wiped the static fields.
        const string Title = "Fullscreen Game";

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            System.Type gameViewType = EditorInternals.EditorType("UnityEditor.GameView");
            bool closed = false;
            foreach (EditorWindow open in Resources.FindObjectsOfTypeAll(gameViewType))
                if (open.titleContent.text == Title)
                {
                    open.Close();
                    closed = true;
                }
            if (closed)
                return;

            var window = (EditorWindow)ScriptableObject.CreateInstance(gameViewType);
            window.titleContent = new GUIContent(Title);
            EditorInternals.Set(window, "showToolbar", false);
            if (!TryGetMonitor(out Bounds monitor))
                return;
            float scale = EditorGUIUtility.pixelsPerPoint;
            window.ShowPopup();
            window.position = new Rect(monitor.left / scale, monitor.top / scale, (monitor.right - monitor.left) / scale, (monitor.bottom - monitor.top) / scale);
            window.Focus();
            // Unity fits popups to the work area, so the taskbar stays visible; stretch the native window over it.
            EditorApplication.delayCall += () =>
            {
                if (window == null)
                    return;
                System.IntPtr handle = GetForegroundWindow();
                // Only touch the popup itself, never the main editor window if focus went elsewhere.
                if (!GetWindowRect(handle, out Bounds current) || Mathf.Abs(current.bottom - current.top - window.position.height * scale) > 2f)
                    return;
                SetWindowPos(handle, TopMost, monitor.left, monitor.top, monitor.right - monitor.left, monitor.bottom - monitor.top, ShowWindow);
            };
        }

        // The whole monitor under the main editor window, in pixels.
        static bool TryGetMonitor(out Bounds monitor)
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float scale = EditorGUIUtility.pixelsPerPoint;
            var centre = new Point { x = (int)(main.center.x * scale), y = (int)(main.center.y * scale) };
            var info = new MonitorInfo { size = Marshal.SizeOf<MonitorInfo>() };
            bool found = GetMonitorInfo(MonitorFromPoint(centre, MonitorDefaultToNearest), ref info);
            monitor = info.monitor;
            return found;
        }

        const uint MonitorDefaultToNearest = 2;
        const uint ShowWindow = 0x0040;
        static readonly System.IntPtr TopMost = new(-1);

        [StructLayout(LayoutKind.Sequential)]
        struct Point { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        struct Bounds { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MonitorInfo
        {
            public int size;
            public Bounds monitor, work;
            public uint flags;
        }

        [DllImport("user32.dll")]
        static extern System.IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(System.IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        static extern System.IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(System.IntPtr window, out Bounds rect);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(System.IntPtr window, System.IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
