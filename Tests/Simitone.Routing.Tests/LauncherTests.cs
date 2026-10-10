using Simitone.Launcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Simitone.Routing.Tests
{
    /// <summary>The launcher edits the game's own ini files: it must keep the other lines and use the game's keys and defaults.</summary>
    public static class LauncherTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        private static string FindRepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
                if (File.Exists(path)) return path;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>The { "Key", "value" } defaults listed in a game settings source file.</summary>
        private static Dictionary<string, string> GameDefaults(string file)
        {
            var result = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\\{\\s*\"(\\w+)\"\\s*,\\s*\"([^\"]*)\"\\s*\\}"))
                result[m.Groups[1].Value] = m.Groups[2].Value;
            return result;
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("launcher ini: set replaces in place, adds missing keys, keeps every other line", () =>
            {
                var ini = IniFile.FromText("x.ini", "# header\nShimmy=True\nOther=1\n\nPanSpeed=2\n");
                ini.Set("Shimmy", "False");
                ini.Set("NoSound", "True");
                var lines = ini.Text().Replace("\r\n", "\n").Split('\n');
                Check(lines[0] == "# header" && lines[1] == "Shimmy=False" && lines[2] == "Other=1" && lines[4] == "PanSpeed=2", "lines kept in order");
                Check(lines.Contains("NoSound=True"), "missing key added");
                Check(ini.Get("Shimmy", "?") == "False" && ini.Get("Absent", "fallback") == "fallback", "get");
            });

            yield return ("launcher ini: the last duplicate wins, as in the game", () =>
            {
                var ini = IniFile.FromText("x.ini", "A=1\nA=2\n");
                Check(ini.Get("A", null) == "2", "last wins");
                ini.Set("A", "3");
                Check(ini.Text().Replace("\r\n", "\n") == "A=1\nA=3\n\n" || ini.Get("A", null) == "3", "set changes the winning line");
            });

            yield return ("launcher settings: Fullscreen is stored inverted as Windowed", () =>
            {
                var def = LauncherSettings.All().First(d => d.Key == "Windowed");
                Check(LauncherSettings.ValueFor(def, 1) == "False", "fullscreen on = Windowed False");
                Check(LauncherSettings.ChoiceFor(def, "True") == 0, "Windowed True shows fullscreen off");
                Check(LauncherSettings.ChoiceFor(def, null) == 0, "default is windowed");
            });

            yield return ("launcher settings: unknown stored values fall back to the default choice", () =>
            {
                var def = LauncherSettings.All().First(d => d.Key == "LightingMode");
                Check(LauncherSettings.ChoiceFor(def, "9") == 3, "default is High");
                Check(LauncherSettings.ChoiceFor(def, "1") == 1, "known value");
            });

            yield return ("launcher settings: every key and default matches the game's own settings", () =>
            {
                var simitone = FindRepoFile("Client", "Simitone", "Simitone.Client", "Utils", "SimitoneSettings.cs");
                var config = FindRepoFile("FreeSO", "TSOClient", "FSO.UI", "GlobalSettings.cs");
                Check(simitone != null && config != null, "game settings sources not found from " + AppContext.BaseDirectory);
                var game = new Dictionary<IniTarget, Dictionary<string, string>>
                {
                    { IniTarget.Simitone, GameDefaults(simitone) },
                    { IniTarget.Config, GameDefaults(config) },
                };
                foreach (var def in LauncherSettings.All())
                {
                    Check(game[def.Target].TryGetValue(def.Key, out var gameDefault), def.Key + " is not a setting of the game");
                    Check(string.Equals(gameDefault, def.Default, StringComparison.OrdinalIgnoreCase), def.Key + " default " + def.Default + " but the game uses " + gameDefault);
                    Check(def.Choices.Length == def.Values.Length, def.Key + " choices and values differ in length");
                    Check(def.Values.Any(v => string.Equals(v, def.Default, StringComparison.OrdinalIgnoreCase)), def.Key + " default is not one of its values");
                }
            });

            yield return ("launcher arguments: extras are split and quoted, smoke test adds its switches first", () =>
            {
                Check(LaunchArguments.Build("", false, 0) == "", "nothing");
                Check(LaunchArguments.Build("-3d -hz144", false, 0) == "-3d -hz144", "plain");
                Check(LaunchArguments.Build("-path \"D:\\My Games\\The Sims\"", false, 0) == "-path \"D:\\My Games\\The Sims\"", "quoted path survives");
                Check(LaunchArguments.Build("-aa", true, 30) == "-smoke -smokeminutes=30 -aa", "smoke");
                Check(LaunchArguments.Split("  a   b  ").SequenceEqual(new[] { "a", "b" }), "extra spaces");
            });
        }
    }
}
