using System;

namespace Simitone.Windows.GameLocator
{
    /// <summary>
    /// Reads the game folder from the command line. Accepted spellings: -path"C:\Sims" and -pathC:\Sims (the original
    /// form), -path=C:\Sims, and -path "C:\Sims" (the form people naturally type; with the original parser the
    /// value was a separate argument and was ignored, so Simitone silently used the auto-detected install).
    /// </summary>
    public static class PathArgument
    {
        /// <summary>True if a -path switch is present; value is its folder (null if the switch had none).</summary>
        public static bool TryGet(string[] args, out string value)
        {
            value = null;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.IsNullOrEmpty(arg) || arg[0] != '-' || !arg.Substring(1).StartsWith("path", StringComparison.Ordinal)) continue;
                var rest = arg.Substring(5);
                if (rest.StartsWith("=")) rest = rest.Substring(1);
                if (rest.Length == 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-")) rest = args[i + 1];
                rest = rest.Trim().Trim('"').Trim();
                value = rest.Length == 0 ? null : rest;
                return true;
            }
            return false;
        }

        /// <summary>Forward slashes and a trailing slash, as the content loader expects.</summary>
        public static string Normalise(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/') + "/";
        }
    }
}
