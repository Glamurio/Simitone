using System;
using System.IO;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Which game install and which saves Simitone is using, for Options > Settings > Debug. Set once at start-up.
    /// </summary>
    public static class GameSourceInfo
    {
        public static string GamePath { get; private set; }
        /// <summary>How the folder was chosen: command line, settings or auto-detected.</summary>
        public static string Origin { get; private set; }
        public static bool IsLegacyCollection { get; private set; }

        public static void Set(string path, string origin, bool legacy)
        {
            GamePath = path;
            Origin = origin;
            IsLegacyCollection = legacy;
        }

        public static string InstallKind => GamePath == null ? "" : (IsLegacyCollection ? "Steam Legacy Collection" : "classic install");

        /// <summary>The folder Simitone actually reads and writes neighbourhoods in (it works on a copy of the original saves).</summary>
        public static string SimitoneUserData => Path.Combine(FSO.Common.FSOEnvironment.UserDir ?? "", "UserData");

        /// <summary>Where Simitone's copy of the neighbourhood was made from, or a note if it can't be told.</summary>
        public static string SaveCopySource()
        {
            try
            {
                var dir = SimitoneUserData;
                if (!Directory.Exists(dir)) return "not created yet (made when a neighbourhood is first opened)";
                var marker = Path.Combine(dir, "simitone-import-source.txt");
                if (File.Exists(marker)) return File.ReadAllText(marker).Trim();
                return "unknown (copied by an older Simitone; use Re-import saves to record it)";
            }
            catch (Exception) { return "unknown"; }
        }
    }
}
