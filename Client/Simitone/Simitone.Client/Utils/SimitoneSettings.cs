using FSO.Common;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using System.Collections.Generic;
using System.IO;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Simitone's own settings, stored in Documents/Simitone/simitone.ini.
    ///
    /// Kept apart from FreeSO's GlobalSettings (config.ini) because Program.cs overwrites several GlobalSettings
    /// values on every start, and because every Simitone behaviour change must be switchable on its own.
    /// Like GlobalSettings, a setting is a default in _DefaultValues plus a public property of the same name; IniConfig
    /// loads and saves them by reflection (enums don't round-trip, so choices are stored as int).
    /// </summary>
    public class SimitoneSettings : IniConfig
    {
        private static SimitoneSettings _Default;

        public static SimitoneSettings Default
        {
            get
            {
                if (_Default == null) _Default = new SimitoneSettings(Path.Combine(FSOEnvironment.UserDir, "simitone.ini"));
                return _Default;
            }
        }

        public SimitoneSettings(string path) : base(path) { }

        public override string HeadingComment => "Simitone settings. Most can be changed in game (Options > Settings).";

        private Dictionary<string, string> _DefaultValues = new Dictionary<string, string>()
        {
            //simulation changes (VMFeatures)
            { "Shimmy", "true" },
            { "RoutingFixes", "true" },
            { "QueueFixes", "true" },
            { "DynamicObstacles", "true" },
            { "Unstick", "true" },
            { "ApproachPositions", "true" },
            { "ObjectSelection", "true" },
            { "AutonomyFixes", "true" },
            { "AutonomyCountOnce", "true" },
            { "QueueRecovery", "true" },
            { "Conversations", "true" },

            //interface
            { "QueueNotices", "true" },
            { "CameraShortcuts", "true" },
            { "CatalogSearch", "true" },
            { "BuildUndo", "true" },

            //debugging
            { "Diagnostics", "true" },
            { "DrawRoutes", "false" },

            //start-up options (each also has a command line switch, which wins for that run)
            { "Enable3D", "false" },
            { "UseDirectX", "true" },
            { "AntiAlias", "false" },
            { "TouchUI", "false" },
            { "NoSound", "false" },
            { "LightingMode", "3" },
            { "ComplexShaders", "true" },
            { "EnableTransitions", "true" },
        };

        public override Dictionary<string, string> DefaultValues
        {
            get { return _DefaultValues; }
            set { _DefaultValues = value; }
        }

        public bool Shimmy { get; set; }
        public bool RoutingFixes { get; set; }
        public bool QueueFixes { get; set; }
        public bool DynamicObstacles { get; set; }
        public bool Unstick { get; set; }
        public bool ApproachPositions { get; set; }
        public bool ObjectSelection { get; set; }
        public bool AutonomyFixes { get; set; }
        public bool AutonomyCountOnce { get; set; }
        public bool QueueRecovery { get; set; }
        public bool Conversations { get; set; }

        public bool QueueNotices { get; set; }
        public bool CameraShortcuts { get; set; }
        public bool CatalogSearch { get; set; }
        public bool BuildUndo { get; set; }

        public bool Diagnostics { get; set; }
        public bool DrawRoutes { get; set; }

        public bool Enable3D { get; set; }
        public bool UseDirectX { get; set; }
        public bool AntiAlias { get; set; }
        public bool TouchUI { get; set; }
        public bool NoSound { get; set; }
        public int LightingMode { get; set; }
        public bool ComplexShaders { get; set; }
        public bool EnableTransitions { get; set; }

        /// <summary>
        /// Pushes the simulation switches into the engine. Called at start-up and after every change.
        /// </summary>
        public void ApplyToEngine()
        {
            VMFeatures.Shimmy = Shimmy;
            VMFeatures.RoutingFixes = RoutingFixes;
            VMFeatures.QueueFixes = QueueFixes;
            VMFeatures.DynamicObstacles = DynamicObstacles;
            VMFeatures.Unstick = Unstick;
            VMFeatures.ApproachPositions = ApproachPositions;
            VMFeatures.ObjectSelection = ObjectSelection;
            VMFeatures.AutonomyFixes = AutonomyFixes;
            VMFeatures.AutonomyCountOnce = AutonomyCountOnce;
            VMFeatures.QueueRecovery = QueueRecovery;
            VMFeatures.Conversations = Conversations;
            VMFeatures.Diagnostics = Diagnostics;
            VMRoutingFrame.DEBUG_DRAW = DrawRoutes;
        }
    }
}
