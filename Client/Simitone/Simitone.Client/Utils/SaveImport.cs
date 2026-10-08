using FSO.Common;
using System;
using System.IO;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Simitone keeps its own copy of the neighbourhoods (its lot saves aren't compatible with the original game), made
    /// from the original game's saves the first time each one is used. Once made it is never refreshed, so saves the
    /// player makes in the original game afterwards don't show up. This lets the player ask for a fresh copy.
    /// The existing copies are renamed, never deleted.
    /// </summary>
    public static class SaveImport
    {
        private static string FlagPath => Path.Combine(FSOEnvironment.UserDir, "reimport-saves.flag");

        public static bool Pending => File.Exists(FlagPath);

        public static void Request()
        {
            File.WriteAllText(FlagPath, "Simitone will set aside its neighbourhood copies and import them again from the original game on the next start.");
        }

        public static void Cancel()
        {
            if (File.Exists(FlagPath)) File.Delete(FlagPath);
        }

        /// <summary>Call at start-up, before anything loads a neighbourhood. Returns the number of folders set aside.</summary>
        public static int ApplyPending()
        {
            if (!Pending) return 0;
            int moved = 0;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try
            {
                foreach (var dir in Directory.GetDirectories(FSOEnvironment.UserDir, "UserData*"))
                {
                    var name = Path.GetFileName(dir);
                    if (name.Contains(".backup-")) continue;
                    Directory.Move(dir, dir + ".backup-" + stamp);
                    moved++;
                }
                Cancel();
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not set aside Simitone's neighbourhood copies: " + e.Message);
            }
            return moved;
        }
    }
}
