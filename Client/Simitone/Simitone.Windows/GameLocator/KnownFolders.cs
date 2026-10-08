using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Simitone.Windows.GameLocator
{
    public static class KnownFolders
    {
        private static readonly Guid SavedGamesId = new Guid("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4");

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);

        /// <summary>The Saved Games folder (it can be redirected, so it is asked for rather than assumed), or null.</summary>
        public static string SavedGames()
        {
            try
            {
                if (SHGetKnownFolderPath(SavedGamesId, 0, IntPtr.Zero, out var ptr) == 0)
                {
                    var path = Marshal.PtrToStringUni(ptr);
                    Marshal.FreeCoTaskMem(ptr);
                    return path;
                }
            }
            catch (Exception) { }
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
            return fallback;
        }
    }
}
