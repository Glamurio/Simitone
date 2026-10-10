using System;
using System.Runtime.InteropServices;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Keeps the mouse inside the game window (Windows only) so edge scrolling works at every edge and the cursor can't
    /// wander onto another monitor in fullscreen. Released whenever the window loses focus.
    /// </summary>
    public static class MouseConfine
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
        [DllImport("user32.dll")] private static extern bool ClipCursor(ref RECT rect);
        [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr rect);

        private static bool Active;
        private static RECT LastRect;

        public static void Update(IntPtr window, bool confine)
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                if (!confine || window == IntPtr.Zero)
                {
                    if (Active) { ClipCursor(IntPtr.Zero); Active = false; }
                    return;
                }
                if (!GetClientRect(window, out var client)) return;
                var topLeft = new POINT { X = client.Left, Y = client.Top };
                var bottomRight = new POINT { X = client.Right, Y = client.Bottom };
                ClientToScreen(window, ref topLeft);
                ClientToScreen(window, ref bottomRight);
                var rect = new RECT { Left = topLeft.X, Top = topLeft.Y, Right = bottomRight.X, Bottom = bottomRight.Y };
                //re-applied only when the window moved or was resized.
                if (Active && rect.Left == LastRect.Left && rect.Top == LastRect.Top && rect.Right == LastRect.Right && rect.Bottom == LastRect.Bottom) return;
                ClipCursor(ref rect);
                LastRect = rect;
                Active = true;
            }
            catch (Exception) { Active = false; }
        }
    }
}
