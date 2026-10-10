using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Simitone.Launcher
{
    /// <summary>
    /// A minimal editor for the game's own .ini files (simitone.ini, config.ini): "Key=Value" lines, '#' comments.
    /// Only the keys that are set are changed; every other line (and the game's header comment) stays as it was, because the
    /// game writes the whole file back on its own, and a key missing from the file simply takes the game's default.
    /// </summary>
    public class IniFile
    {
        public string Path { get; private set; }
        private readonly List<string> Lines = new List<string>();

        public IniFile(string path)
        {
            Path = path;
            if (File.Exists(path)) Lines.AddRange(File.ReadAllLines(path));
        }

        public static IniFile FromText(string path, string text)
        {
            var ini = new IniFile(path + ".none");
            ini.Lines.Clear();
            if (!string.IsNullOrEmpty(text)) ini.Lines.AddRange(text.Replace("\r\n", "\n").Split('\n'));
            ini.Path = path;
            return ini;
        }

        private static bool TryParse(string line, out string key, out string value)
        {
            key = value = null;
            var clean = line.Trim();
            if (clean.Length == 0 || clean[0] == '#' || clean[0] == '[') return false;
            var split = clean.IndexOf('=');
            if (split < 0) return false;
            key = clean.Substring(0, split).Trim();
            value = clean.Substring(split + 1).Trim();
            return true;
        }

        public string Get(string key, string fallback)
        {
            string found = null;
            foreach (var line in Lines)
                if (TryParse(line, out var k, out var v) && k == key) found = v; //the game takes the last one
            return found ?? fallback;
        }

        public void Set(string key, string value)
        {
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (TryParse(Lines[i], out var k, out _) && k == key) { Lines[i] = key + "=" + value; return; }
            }
            Lines.Add(key + "=" + value);
        }

        public string Text() { return string.Join(Environment.NewLine, Lines) + Environment.NewLine; }

        public void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            File.WriteAllText(Path, Text());
        }
    }

    public enum IniTarget { Simitone, Config }

    /// <summary>One setting of the launcher: where it is stored and what the game's own settings dialog calls it.</summary>
    public class SettingDef
    {
        public IniTarget Target;
        public string Section;
        public string Key;
        public string Label;
        public string Help;
        public string[] Choices;
        /// <summary>The text stored in the ini for each choice (same order).</summary>
        public string[] Values;
        public string Default;
        /// <summary>The ini stores the opposite of what the label says (Fullscreen is stored as Windowed).</summary>
        public bool Inverted;
        public bool IsToggle => Choices.Length == 2 && Choices[0] == "Off";

        public int IndexOf(string stored)
        {
            if (stored == null) stored = Default;
            for (int i = 0; i < Values.Length; i++)
                if (string.Equals(Values[i], stored, StringComparison.OrdinalIgnoreCase)) return i;
            for (int i = 0; i < Values.Length; i++)
                if (string.Equals(Values[i], Default, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }
    }

    public static class LauncherSettings
    {
        public const string SIMULATION = "Simulation";
        public const string INTERFACE = "Interface";
        public const string DISPLAY = "Display";
        public const string SOUND = "Sound";
        public const string DEBUG = "Debug";
        public static readonly string[] Sections = { SIMULATION, INTERFACE, DISPLAY, SOUND, DEBUG };

        private static readonly string[] Off_On = { "Off", "On" };
        private static readonly string[] False_True = { "False", "True" };

        private static SettingDef Toggle(IniTarget t, string section, string key, string label, string help, bool def, bool inverted = false)
        {
            return new SettingDef
            {
                Target = t, Section = section, Key = key, Label = label, Help = help, Choices = Off_On, Values = False_True,
                Default = def ? "True" : "False", Inverted = inverted
            };
        }

        private static SettingDef Choice(IniTarget t, string section, string key, string label, string help, string[] choices, string[] values, string def)
        {
            return new SettingDef { Target = t, Section = section, Key = key, Label = label, Help = help, Choices = choices, Values = values, Default = def };
        }

        private static string[] Numbers(int from, int count) { return Enumerable.Range(from, count).Select(x => x.ToString()).ToArray(); }

        private static readonly string[] Languages =
        {
            "English (US)", "English (UK)", "French", "German", "Italian", "Spanish", "Dutch", "Danish", "Swedish",
            "Norwegian", "Finnish", "Hebrew", "Russian", "Portuguese", "Japanese", "Polish", "Simplified Chinese",
            "Traditional Chinese", "Thai", "Korean"
        };

        private static readonly int[] RefreshRates = { 30, 60, 75, 100, 120, 144, 165, 240 };

        /// <summary>
        /// The same settings, names and descriptions as the game's Options > Settings, for the ones that are read when the game starts
        /// or are worth setting beforehand. Keys are those of simitone.ini / config.ini.
        /// </summary>
        public static List<SettingDef> All()
        {
            var S = IniTarget.Simitone;
            var C = IniTarget.Config;
            var list = new List<SettingDef>();

            list.Add(Toggle(S, SIMULATION, "Shimmy", "Shimmy through narrow gaps",
                "Sims side-step and pets squeeze between objects instead of failing to route.", true));
            list.Add(Toggle(S, SIMULATION, "QoLFixes", "QoL fixes",
                "All of Simitone's corrections to the original game in one switch: routing, action queues, free will and object data fixes "
                + "(trash and dirt piles no longer multiply, fireplaces ignite things far less often). Objects already loaded keep their old behaviour until the next lot.", true));

            list.Add(Toggle(C, INTERFACE, "EdgeScroll", "Edge scrolling", "Scroll the lot when the mouse touches the screen edge.", true));
            list.Add(Toggle(S, INTERFACE, "CameraShortcuts", "Camera shortcuts",
                "Arrows or WASD pan, Q/E (or , and .) rotate, C centres, F follows the Sim, Backspace goes back, F5-F7 views (Ctrl saves).", true));
            list.Add(Choice(S, INTERFACE, "PanningSpeed", "Panning speed", "How fast the arrow keys and WASD move the camera.",
                new[] { "Slow", "Fast", "Very fast" }, Numbers(0, 3), "0"));
            list.Add(Toggle(S, INTERFACE, "SteppedZoom", "Stepped wheel zoom (2D)",
                "The mouse wheel moves one view distance per notch, like the original game, instead of zooming smoothly.", true));
            list.Add(Choice(S, INTERFACE, "ConfineMouse", "Keep mouse in the window",
                "Stops the mouse leaving the game (so edge scrolling works at every edge). Released when you switch to another program.",
                new[] { "Never", "Fullscreen only", "Always" }, Numbers(0, 3), "1"));
            list.Add(Toggle(S, INTERFACE, "CatalogSearch", "Catalog search and wheel scrolling",
                "Search box in the buy catalog; the mouse wheel scrolls the catalog when over it.", true));
            list.Add(Toggle(S, INTERFACE, "BuildUndo", "Undo in buy and build mode",
                "Ctrl+Z undoes buying, moving and rotating objects (full refund), Ctrl+Y redoes. Cleared in live mode.", true));
            list.Add(Toggle(S, INTERFACE, "QueueNotices", "Say why actions were dropped",
                "A short note under the queue when an action is dropped (can't start, object gone).", true));

            list.Add(Toggle(C, DISPLAY, "Windowed", "Fullscreen", "Also toggled with Alt+Enter in game.", true, inverted: true));
            list.Add(Choice(C, DISPLAY, "TargetRefreshRate", "Frame rate", "Target refresh rate (same as -hz).",
                RefreshRates.Select(x => x + " Hz").ToArray(), RefreshRates.Select(x => x.ToString()).ToArray(), "60"));
            list.Add(Toggle(S, DISPLAY, "Enable3D", "3D mode", "Use the 3D renderer (same as -3d). F12 switches 3D/hybrid in game.", false));
            list.Add(Choice(S, DISPLAY, "UseDirectX", "Renderer", "DirectX or OpenGL (same as -dx / -gl).",
                new[] { "OpenGL", "DirectX" }, new[] { "False", "True" }, "True"));
            list.Add(Toggle(S, DISPLAY, "AntiAlias", "Anti-aliasing", "Smooth edges (same as -aa).", false));
            list.Add(Choice(S, DISPLAY, "LightingMode", "Lighting quality", "Lower settings are faster on old graphics cards.",
                new[] { "Off", "Low", "Medium", "High" }, Numbers(0, 4), "3"));
            list.Add(Toggle(S, DISPLAY, "ComplexShaders", "Advanced shaders", "More detailed lighting and shading.", true));
            list.Add(Toggle(S, DISPLAY, "EnableTransitions", "Lot transitions", "Animated transitions when loading lots.", true));
            list.Add(Toggle(S, DISPLAY, "TouchUI", "Touch interface", "Mobile-style controls and on-screen keyboard (same as -touch).", false));
            list.Add(Choice(C, DISPLAY, "LanguageCode", "Language", "Game text language (same as -lang).", Languages, Numbers(1, Languages.Length), "1"));

            var volumes = Enumerable.Range(0, 11).Select(x => (x * 10) + "%").ToArray();
            list.Add(Choice(C, SOUND, "MusicVolume", "Music volume", null, volumes, Numbers(0, 11), "10"));
            list.Add(Choice(C, SOUND, "FXVolume", "Sound effects volume", null, volumes, Numbers(0, 11), "10"));
            list.Add(Choice(C, SOUND, "VoxVolume", "Voices volume", null, volumes, Numbers(0, 11), "10"));
            list.Add(Choice(C, SOUND, "AmbienceVolume", "Ambience volume", null, volumes, Numbers(0, 11), "1"));
            list.Add(Toggle(S, SOUND, "NoSound", "Disable all sound", "Starts Simitone without audio (same as -nosound).", false));

            list.Add(Toggle(S, DEBUG, "DrawRoutes", "Draw routes", "Show route rectangles, paths and shimmy gaps.", false));
            list.Add(Toggle(S, DEBUG, "Diagnostics", "Record diagnostics", "Keeps the data for the diagnostics report (small cost while playing).", true));
            return list;
        }

        /// <summary>The value to store in the ini for a choice, taking an inverted setting into account.</summary>
        public static string ValueFor(SettingDef def, int choiceIndex)
        {
            if (def.Inverted && def.Values.Length == 2) return def.Values[1 - choiceIndex];
            return def.Values[choiceIndex];
        }

        /// <summary>The choice to show for what the ini holds.</summary>
        public static int ChoiceFor(SettingDef def, string stored)
        {
            var index = def.IndexOf(stored);
            return def.Inverted && def.Values.Length == 2 ? 1 - index : index;
        }
    }

    /// <summary>Folders and files the launcher works with.</summary>
    public static class LauncherPaths
    {
        public static string UserDir
        {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Simitone"); }
        }

        /// <summary>The game's executable: next to the launcher, else a saved choice.</summary>
        public static string FindGame(string launcherDir, string savedExe)
        {
            foreach (var name in new[] { "Simitone.exe", "Simitone.Windows.exe" })
            {
                var path = System.IO.Path.Combine(launcherDir, name);
                if (File.Exists(path)) return path;
            }
            return !string.IsNullOrEmpty(savedExe) && File.Exists(savedExe) ? savedExe : null;
        }
    }

    public static class LaunchArguments
    {
        /// <summary>Splits a command line the way Windows does for simple cases: spaces separate, double quotes group.</summary>
        public static List<string> Split(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            var current = new StringBuilder();
            bool quoted = false, any = false;
            foreach (var c in text)
            {
                if (c == '"') { quoted = !quoted; any = true; }
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (any || current.Length > 0) { result.Add(current.ToString()); current.Clear(); any = false; }
                }
                else current.Append(c);
            }
            if (any || current.Length > 0) result.Add(current.ToString());
            return result;
        }

        private static string Quote(string arg)
        {
            return arg.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0 ? "\"" + arg.Replace("\"", "\\\"") + "\"" : arg;
        }

        /// <summary>The command line for the game. The settings above are read by the game from its ini files, so only extras are passed.</summary>
        public static string Build(string extra, bool smoke, int smokeMinutes)
        {
            var args = new List<string>();
            if (smoke)
            {
                args.Add("-smoke");
                if (smokeMinutes > 0) args.Add("-smokeminutes=" + smokeMinutes);
            }
            args.AddRange(Split(extra));
            return string.Join(" ", args.Select(Quote));
        }
    }
}
