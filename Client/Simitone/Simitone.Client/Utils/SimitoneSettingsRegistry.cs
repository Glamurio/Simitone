using FSO.Client;
using FSO.HIT;
using FSO.HIT.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// One row in the settings dialog. A setting is a list of choices (a toggle is Off/On) plus a getter and setter of
    /// the chosen index. The setter is responsible for applying and saving the value.
    /// </summary>
    public class SimitoneSettingDef
    {
        public string Section;
        public string Label;
        /// <summary>One line shown under the label.</summary>
        public string Help;
        /// <summary>Takes effect after restarting Simitone.</summary>
        public bool RestartRequired;
        /// <summary>A Simitone behaviour change compared to the original game (as opposed to an engine/display option).</summary>
        public bool Departure;
        public string[] Choices;
        public Func<int> Get;
        public Action<int> Set;
        /// <summary>For action rows (a button, no value): run when clicked.</summary>
        public Action Action;
        public string ActionLabel;
        /// <summary>Optional: the action button's text, re-read after each click (for actions that toggle something).</summary>
        public Func<string> ActionState;
        /// <summary>Optional: the description, re-read each time it is shown (for rows that display a current value).</summary>
        public Func<string> HelpState;

        public static readonly string[] OffOn = new string[] { "Off", "On" };
    }

    /// <summary>
    /// Every setting the dialog shows: Simitone's own (SimitoneSettings) and the existing FreeSO ones Simitone uses
    /// (GlobalSettings). New features add their switch here.
    /// </summary>
    public static class SimitoneSettingsRegistry
    {
        public const string SIMULATION = "Simulation";
        public const string CONTROLS = "Interface";
        public const string SOUND = "Sound";
        public const string DISPLAY = "Display";
        public const string DEBUG = "Debug";

        public static readonly string[] Sections = new string[] { SIMULATION, CONTROLS, SOUND, DISPLAY, DEBUG };

        /// <summary>Set by the client to write the diagnostics report (needs the running VM).</summary>
        public static Func<string> WriteDiagnostics;
        /// <summary>Set by the platform start-up: shows a folder picker, returns the chosen folder or null.</summary>
        public static Func<string> BrowseGameFolder;
        public static Func<string, bool> IsValidGameFolder;
        private static string GameFolderMessage;

        private static SimitoneSettings S => SimitoneSettings.Default;
        private static GlobalSettings G => GlobalSettings.Default;

        private static void SaveS()
        {
            S.Save();
            S.ApplyToEngine();
        }

        private static SimitoneSettingDef Toggle(string section, string label, string help, Func<bool> get, Action<bool> set,
            bool restart = false, bool departure = false)
        {
            return new SimitoneSettingDef()
            {
                Section = section, Label = label, Help = help, RestartRequired = restart, Departure = departure,
                Choices = SimitoneSettingDef.OffOn,
                Get = () => get() ? 1 : 0,
                Set = (i) => set(i == 1)
            };
        }

        private static SimitoneSettingDef Volume(string label, Func<int> get, Action<int> set, HITVolumeGroup group)
        {
            return new SimitoneSettingDef()
            {
                Section = SOUND, Label = label,
                Choices = Enumerable.Range(0, 11).Select(x => (x * 10) + "%").ToArray(),
                Get = () => Math.Max(0, Math.Min(10, get())),
                Set = (i) =>
                {
                    set(i);
                    G.Save();
                    HITVM.Get()?.SetMasterVolume(group, i / 10f);
                }
            };
        }

        //labels match FSO.Files.Formats.IFF.Chunks.STRLangCode 1-20
        private static readonly string[] Languages = new string[]
        {
            "English (US)", "English (UK)", "French", "German", "Italian", "Spanish", "Dutch", "Danish", "Swedish",
            "Norwegian", "Finnish", "Hebrew", "Russian", "Portuguese", "Japanese", "Polish", "Simplified Chinese",
            "Traditional Chinese", "Thai", "Korean"
        };

        private static readonly int[] RefreshRates = new int[] { 30, 60, 75, 100, 120, 144, 165, 240 };

        public static List<SimitoneSettingDef> All()
        {
            var list = new List<SimitoneSettingDef>();

            //--- simulation (Simitone changes to the original behaviour)
            list.Add(Toggle(SIMULATION, "Shimmy through narrow gaps",
                "Sims side-step and pets squeeze between objects instead of failing to route.",
                () => S.Shimmy, (v) => { S.Shimmy = v; SaveS(); }, departure: true));
            list.Add(Toggle(SIMULATION, "QoL fixes",
                "All of Simitone's corrections to the original game in one switch: routing (own wait time per destination, doors, walking around standing Sims, freeing stuck Sims, smarter approach spots, avoiding unreachable objects), "
                + "action queues (cancelling pet actions, no freeze, keeping the queue when an object vanishes), free will (respects cooldowns, avoids failed objects, counts each action once, respects conversations) "
                + "and object data fixes (trash and dirt piles no longer multiply). Objects already loaded keep their old behaviour until the next lot.",
                () => S.QoLFixes,
                (v) =>
                {
                    S.QoLFixes = v;
                    //turning it on re-enables every part, so an old simitone.ini with one part off cannot leave it half on.
                    if (v) S.RoutingFixes = S.QueueFixes = S.DynamicObstacles = S.Unstick = S.ApproachPositions = S.ObjectSelection
                        = S.AutonomyFixes = S.AutonomyCountOnce = S.QueueRecovery = S.Conversations = true;
                    SaveS();
                }, departure: true));

            //--- camera and controls
            list.Add(Toggle(CONTROLS, "Edge scrolling", "Scroll the lot when the mouse touches the screen edge.",
                () => G.EdgeScroll, (v) => { G.EdgeScroll = v; G.Save(); }));
            list.Add(Toggle(CONTROLS, "Camera shortcuts",
                "Arrows or WASD pan, Q/E (or , and .) rotate, C centres, F follows the Sim, Backspace goes back, F5-F7 views (Ctrl saves).",
                () => S.CameraShortcuts, (v) => { S.CameraShortcuts = v; S.Save(); }, departure: true));
            list.Add(new SimitoneSettingDef()
            {
                Section = CONTROLS, Label = "Keyboard pan speed",
                Help = "How fast the arrow keys and WASD move the camera.",
                Choices = new string[] { "Slow", "Normal", "Fast", "Very fast" },
                Get = () => Math.Max(0, Math.Min(3, S.PanSpeed)),
                Set = (i) => { S.PanSpeed = i; S.Save(); }
            });
            list.Add(Toggle(CONTROLS, "Stepped wheel zoom (2D)",
                "The mouse wheel moves one view distance per notch, like the original game, instead of zooming smoothly.",
                () => S.SteppedZoom, (v) => { S.SteppedZoom = v; S.Save(); }, departure: false));
            list.Add(new SimitoneSettingDef()
            {
                Section = CONTROLS, Label = "Keep mouse in the window",
                Help = "Stops the mouse leaving the game (so edge scrolling works at every edge). Released when you switch to another program.",
                Choices = new string[] { "Never", "Fullscreen only", "Always" },
                Get = () => Math.Max(0, Math.Min(2, S.ConfineMouse)),
                Set = (i) => { S.ConfineMouse = i; S.Save(); }
            });
            list.Add(Toggle(CONTROLS, "Catalog search and wheel scrolling",
                "Search box in the buy catalog; the mouse wheel scrolls the catalog when over it. Reopen the catalog to apply.",
                () => S.CatalogSearch, (v) => { S.CatalogSearch = v; S.Save(); }, departure: true));
            list.Add(Toggle(CONTROLS, "Undo in buy and build mode",
                "Ctrl+Z undoes buying, moving and rotating objects (full refund), Ctrl+Y redoes. Cleared in live mode.",
                () => S.BuildUndo, (v) => { S.BuildUndo = v; S.Save(); }, departure: true));
            list.Add(Toggle(CONTROLS, "Say why actions were dropped",
                "A short note under the queue when an action is dropped (can't start, object gone).",
                () => S.QueueNotices, (v) => { S.QueueNotices = v; S.Save(); }, departure: true));

            //--- sound
            list.Add(Volume("Music volume", () => G.MusicVolume, (v) => G.MusicVolume = (byte)v, HITVolumeGroup.MUSIC));
            list.Add(Volume("Sound effects volume", () => G.FXVolume, (v) => G.FXVolume = (byte)v, HITVolumeGroup.FX));
            list.Add(Volume("Voices volume", () => G.VoxVolume, (v) => G.VoxVolume = (byte)v, HITVolumeGroup.VOX));
            list.Add(Volume("Ambience volume", () => G.AmbienceVolume, (v) => G.AmbienceVolume = (byte)v, HITVolumeGroup.AMBIENCE));
            list.Add(Toggle(SOUND, "Disable all sound", "Starts Simitone without audio (same as -nosound).",
                () => S.NoSound, (v) => { S.NoSound = v; S.Save(); }, restart: true));

            //--- display
            list.Add(Toggle(DISPLAY, "Fullscreen", "Also toggled with Alt+Enter.",
                () => !G.Windowed,
                (v) =>
                {
                    var gdm = GameFacade.GraphicsDeviceManager;
                    if (gdm != null && gdm.IsFullScreen != v) gdm.ToggleFullScreen();
                    G.Windowed = !v;
                    G.Save();
                }));
            list.Add(new SimitoneSettingDef()
            {
                Section = DISPLAY, Label = "Frame rate", Help = "Target refresh rate (same as -hz).", RestartRequired = true,
                Choices = RefreshRates.Select(x => x + " Hz").ToArray(),
                Get = () => Math.Max(0, Array.IndexOf(RefreshRates, G.TargetRefreshRate)),
                Set = (i) => { G.TargetRefreshRate = RefreshRates[i]; G.Save(); }
            });
            list.Add(Toggle(DISPLAY, "3D mode", "Use the 3D renderer (same as -3d). F12 switches 3D/hybrid in game.",
                () => S.Enable3D, (v) => { S.Enable3D = v; S.Save(); }, restart: true));
            list.Add(new SimitoneSettingDef()
            {
                Section = DISPLAY, Label = "Renderer", Help = "DirectX or OpenGL (same as -dx / -gl). Windows only.", RestartRequired = true,
                Choices = new string[] { "OpenGL", "DirectX" },
                Get = () => S.UseDirectX ? 1 : 0,
                Set = (i) => { S.UseDirectX = i == 1; S.Save(); }
            });
            list.Add(Toggle(DISPLAY, "Anti-aliasing", "Smooth edges (same as -aa).",
                () => S.AntiAlias, (v) => { S.AntiAlias = v; S.Save(); }, restart: true));
            list.Add(new SimitoneSettingDef()
            {
                Section = DISPLAY, Label = "Lighting quality", Help = "Lower settings are faster on old graphics cards.", RestartRequired = true,
                Choices = new string[] { "Off", "Low", "Medium", "High" },
                Get = () => Math.Max(0, Math.Min(3, S.LightingMode)),
                Set = (i) => { S.LightingMode = i; S.Save(); }
            });
            list.Add(Toggle(DISPLAY, "Advanced shaders", "More detailed lighting and shading.",
                () => S.ComplexShaders, (v) => { S.ComplexShaders = v; S.Save(); }, restart: true));
            list.Add(Toggle(DISPLAY, "Lot transitions", "Animated transitions when loading lots.",
                () => S.EnableTransitions, (v) => { S.EnableTransitions = v; S.Save(); }, restart: true));
            list.Add(Toggle(DISPLAY, "Touch interface", "Mobile-style controls and on-screen keyboard (same as -touch).",
                () => S.TouchUI, (v) => { S.TouchUI = v; S.Save(); }, restart: true));
            list.Add(new SimitoneSettingDef()
            {
                Section = DISPLAY, Label = "Language", Help = "Game text language (same as -lang).", RestartRequired = true,
                Choices = Languages,
                Get = () => Math.Max(0, Math.Min(Languages.Length - 1, G.LanguageCode - 1)),
                Set = (i) => { G.LanguageCode = (byte)(i + 1); G.Save(); }
            });

            list.Add(new SimitoneSettingDef()
            {
                Section = CONTROLS, Label = "Re-import saves from The Sims",
                Help = "Simitone works on its own copy of your neighbourhoods. This sets the copy aside (renamed, not deleted) and imports your original game's saves again.",
                RestartRequired = true,
                ActionLabel = "Re-import",
                ActionState = () => SaveImport.Pending ? "Will re-import on restart (click to cancel)" : "Re-import",
                Action = () => { if (SaveImport.Pending) SaveImport.Cancel(); else SaveImport.Request(); }
            });

            //--- debug
            list.Add(new SimitoneSettingDef()
            {
                Section = DEBUG, Label = "Game folder", RestartRequired = true,
                Help = "The Sims 1 install Simitone uses.",
                HelpState = () => "Game folder: " + (GameSourceInfo.GamePath ?? "none")
                    + "\nType: " + GameSourceInfo.InstallKind + ", chosen by " + GameSourceInfo.Origin
                    + (string.IsNullOrEmpty(S.GamePath) ? "" : "\nSaved for next start: " + S.GamePath)
                    + "\n(-path on the command line wins over this setting.)"
                    + (GameFolderMessage == null ? "" : "\n" + GameFolderMessage),
                ActionLabel = "Change...",
                ActionState = () => string.IsNullOrEmpty(S.GamePath) ? "Change..." : "Change... (custom)",
                Action = () =>
                {
                    var chosen = BrowseGameFolder?.Invoke();
                    if (chosen == null) return;
                    if (IsValidGameFolder != null && !IsValidGameFolder(chosen))
                    {
                        GameFolderMessage = "Not changed: " + chosen + " has no GameData/Behavior.iff.";
                        return;
                    }
                    GameFolderMessage = "Saved: " + chosen + " (used from the next start).";
                    S.GamePath = chosen;
                    S.Save();
                }
            });
            list.Add(new SimitoneSettingDef()
            {
                Section = DEBUG, Label = "Reset game folder", RestartRequired = true,
                Help = "Forget the saved game folder and auto-detect the install again at the next start.",
                ActionLabel = "Reset",
                Action = () => { S.GamePath = ""; GameFolderMessage = "Cleared; the install is auto-detected at the next start."; S.Save(); }
            });
            list.Add(new SimitoneSettingDef()
            {
                Section = DEBUG, Label = "Saves in use",
                Help = "Where your neighbourhoods are.",
                HelpState = () => "Simitone works on its own copy of your neighbourhoods:\n" + GameSourceInfo.SimitoneUserData
                    + "\nCopied from: " + GameSourceInfo.SaveCopySource()
                    + "\nThe original game's saves are never changed.",
                ActionLabel = "Open folder",
                Action = () =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GameSourceInfo.SimitoneUserData) { UseShellExecute = true }); }
                    catch (Exception) { }
                }
            });
            list.Add(Toggle(DEBUG, "Draw routes", "Show route rectangles, paths and shimmy gaps (also: draw_routes cheat).",
                () => S.DrawRoutes, (v) => { S.DrawRoutes = v; SaveS(); }));
            list.Add(Toggle(DEBUG, "Record diagnostics", "Keep each Sim's recent route events and why actions ended.",
                () => S.Diagnostics, (v) => { S.Diagnostics = v; SaveS(); }));
            list.Add(new SimitoneSettingDef()
            {
                Section = DEBUG, Label = "Write diagnostics report", Help = "Saves a report to Documents/Simitone/diagnostics (also: write_routes cheat).",
                ActionLabel = "Write",
                Action = () => WriteDiagnostics?.Invoke()
            });

            return list;
        }
    }
}
