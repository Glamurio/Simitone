using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

namespace Simitone.Windows.GameLocator
{
    /// <summary>
    /// Finding and checking a The Sims 1 install, and where that install keeps the player's saves.
    ///
    /// The classic game (Maxis registry key) keeps saves in its own folder (UserData*). The Steam Legacy Collection
    /// (app 3314060, Sims.exe 1.10.x) keeps them in Saved Games/Electronic Arts/The Sims 25/UserData* and leaves the
    /// install's UserData* as a pristine template (CONFIRMED by inspecting a Steam install after one save).
    /// </summary>
    public static class GameInstall
    {
        public static bool IsValid(string dir)
        {
            try { return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "GameData", "Behavior.iff")); }
            catch (Exception) { return false; }
        }

        /// <summary>The Legacy Collection ships an EA Desktop SDK manifest next to the exe; the classic game never has one.</summary>
        public static bool IsLegacyCollection(string dir)
        {
            try { return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "eadpsdk.json")); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Folders to look in for a player's saves, best first. A folder only counts if it holds a Neighborhood.iff
        /// (checked by the neighbourhood provider); the install itself is always the last resort.
        /// </summary>
        public static List<string> SaveRoots(string installDir)
        {
            var roots = new List<string>();
            if (IsLegacyCollection(installDir))
            {
                var saved = KnownFolders.SavedGames();
                if (saved != null) roots.Add(Path.Combine(saved, "Electronic Arts", "The Sims 25"));
                roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts", "The Sims 25"));
            }
            return roots;
        }

        private static readonly string[] LanguageNames = new string[]
        {
            //index = STRLangCode - 1; names as used by SIMS_LANGUAGE (LIKELY, from Sims.exe strings; numbers are accepted too).
            "USEnglish", "UKEnglish", "French", "German", "Italian", "Spanish", "Dutch", "Danish", "Swedish", "Norwegian",
            "Finnish", "Hebrew", "Russian", "Portuguese", "Japanese", "Polish", "SimplifiedChinese", "TraditionalChinese",
            "Thai", "Korean"
        };

        /// <summary>The language the installed game was set to, as a STRLangCode, or 0 if it can't be told.</summary>
        public static byte InstalledLanguage()
        {
            foreach (var key in new[] { @"HKEY_CURRENT_USER\Software\Electronic Arts\The Sims 25", @"HKEY_LOCAL_MACHINE\SOFTWARE\Maxis\The Sims", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Maxis\The Sims" })
            {
                try
                {
                    var value = Registry.GetValue(key, "SIMS_LANGUAGE", null);
                    if (value == null) continue;
                    if (value is int i && i > 0 && i <= LanguageNames.Length) return (byte)i;
                    var text = value.ToString().Replace(" ", "");
                    if (byte.TryParse(text, out var num) && num > 0 && num <= LanguageNames.Length) return num;
                    var index = Array.FindIndex(LanguageNames, x => x.Equals(text, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0) return (byte)(index + 1);
                }
                catch (Exception) { }
            }
            return 0;
        }
    }
}
