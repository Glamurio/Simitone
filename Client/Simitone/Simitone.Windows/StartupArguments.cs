using System.Collections.Generic;

namespace Simitone.Windows
{
    /// <summary>
    /// The command line switches Simitone understands (apart from -path, see PathArgument). A switch that is not present
    /// stays null so the value from simitone.ini applies. Malformed input is ignored instead of throwing: an empty
    /// argument (which a launcher or a shortcut can produce) or "-hz" / "-langX" used to crash the game on start-up.
    /// </summary>
    public class StartupArguments
    {
        public bool Ide;
        public bool? ThreeD;
        public bool? AntiAlias;
        public bool Jit;
        public bool? UseDirectX;
        public bool? Touch;
        public bool? NoSound;
        public byte? Language;
        public int? RefreshRate;
        /// <summary>-smoke: unattended crash test, see Simitone.Client.Utils.SmokeTest.</summary>
        public bool Smoke;
        public int? SmokeMinutes;
        public int? SmokeSeed;
        public int[] SmokeLots;
        /// <summary>-smokevisual: render and run at normal speed 10 instead of the default headless fast mode.</summary>
        public bool SmokeVisual;
        /// <summary>-smokeall: also load lots without a family (load check only).</summary>
        public bool SmokeAll;
        /// <summary>Arguments that were not understood, for the start-up log.</summary>
        public List<string> Ignored = new List<string>();

        public static StartupArguments Parse(string[] args)
        {
            var result = new StartupArguments();
            if (args == null) return result;
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.IsNullOrEmpty(arg) || arg[0] != '-') { if (!string.IsNullOrEmpty(arg)) result.Ignored.Add(arg); continue; }
                var cmd = arg.Substring(1);
                if (cmd.StartsWith("path")) continue; //PathArgument
                if (cmd.StartsWith("smoke"))
                {
                    result.Smoke = true;
                    var rest = cmd.Substring(5);
                    if (rest.Length == 0) continue;
                    var eq = rest.IndexOf('=');
                    var key = eq < 0 ? rest : rest.Substring(0, eq);
                    var val = eq < 0 ? "" : rest.Substring(eq + 1);
                    if (eq < 0 && (key == "minutes" || key == "seed" || key == "lots"))
                    {
                        //cmd.exe (run-smoke.cmd) splits "-smokeminutes=480" and "-smokelots=21,22" at '=' and ',' into separate
                        //arguments, so the values can follow as the next arguments.
                        var parts = new List<string>();
                        while (i + 1 < args.Length && !string.IsNullOrEmpty(args[i + 1]) && args[i + 1][0] != '-' && (key == "lots" || parts.Count == 0))
                            parts.Add(args[++i]);
                        val = string.Join(",", parts);
                    }
                    if (key == "visual") result.SmokeVisual = true;
                    else if (key == "all") result.SmokeAll = true;
                    else if (key == "minutes" && int.TryParse(val, out var m) && m > 0) result.SmokeMinutes = m;
                    else if (key == "seed" && int.TryParse(val, out var sd)) result.SmokeSeed = sd;
                    else if (key == "lots")
                    {
                        var lots = new List<int>();
                        foreach (var part in val.Split(',')) if (int.TryParse(part, out var l)) lots.Add(l);
                        if (lots.Count > 0) result.SmokeLots = lots.ToArray();
                        else result.Ignored.Add(arg);
                    }
                    else result.Ignored.Add(arg);
                }
                else if (cmd.StartsWith("lang"))
                {
                    if (byte.TryParse(cmd.Substring(4), out var lang)) result.Language = lang; else result.Ignored.Add(arg);
                }
                else if (cmd.StartsWith("hz"))
                {
                    if (int.TryParse(cmd.Substring(2), out var hz) && hz > 0) result.RefreshRate = hz; else result.Ignored.Add(arg);
                }
                else
                {
                    switch (cmd)
                    {
                        case "ide": result.Ide = true; break;
                        case "3d": result.ThreeD = true; break;
                        case "aa": result.AntiAlias = true; break;
                        case "jit": result.Jit = true; break;
                        case "dx":
                        case "dx11": result.UseDirectX = true; break;
                        case "gl":
                        case "ogl": result.UseDirectX = false; break;
                        case "touch": result.Touch = true; break;
                        case "nosound": result.NoSound = true; break;
                        default: result.Ignored.Add(arg); break;
                    }
                }
            }
            return result;
        }
    }
}
