using FSO.Common;
using FSO.SimAntics;
using FSO.SimAntics.Diagnostics;
using System;
using System.IO;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Writes VMDiagnostics reports to Documents/Simitone/diagnostics, for bug reports and for comparing routing
    /// behaviour (the original game has a "write_routes" cheat with the same purpose).
    /// </summary>
    public static class DiagnosticsWriter
    {
        /// <summary>Returns the written file's path, or null if there is no lot loaded or writing failed.</summary>
        public static string Write(VM vm)
        {
            if (vm == null) return null;
            try
            {
                var dir = Path.Combine(FSOEnvironment.UserDir, "diagnostics");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(path, VMDiagnostics.Dump(vm));
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
