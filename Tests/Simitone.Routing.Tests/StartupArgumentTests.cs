using Simitone.Windows;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>Command line switches (used by shortcuts and by a launcher).</summary>
    public static class StartupArgumentTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("switches: the flags a launcher passes are all understood", () =>
            {
                var a = StartupArguments.Parse(new[] { "w", "-ogl", "-3d", "-aa", "-hz144", "-lang1", "-nosound", "-touch", "-ide", "-jit" });
                Check(a.UseDirectX == false, "ogl");
                Check(a.ThreeD == true && a.AntiAlias == true && a.NoSound == true && a.Touch == true && a.Ide && a.Jit, "flags");
                Check(a.RefreshRate == 144 && a.Language == 1, "values");
                Check(a.Ignored.Count == 1 && a.Ignored[0] == "w", "only the bare 'w' is reported as ignored");
            });

            yield return ("switches: nothing given leaves every setting alone", () =>
            {
                var a = StartupArguments.Parse(new string[0]);
                Check(a.UseDirectX == null && a.ThreeD == null && a.AntiAlias == null && a.RefreshRate == null && a.Language == null, "all unset");
            });

            yield return ("switches: empty and malformed arguments no longer throw", () =>
            {
                var a = StartupArguments.Parse(new[] { "", "-", "-hz", "-hzfast", "-lang", "-lang999", "-hz0", null });
                Check(a.RefreshRate == null && a.Language == null, "no values taken");
                Check(StartupArguments.Parse(null).Ignored.Count == 0, "null args");
            });

            yield return ("switches: dx and dx11 select DirectX, gl and ogl select OpenGL, last one wins", () =>
            {
                Check(StartupArguments.Parse(new[] { "-dx11" }).UseDirectX == true, "dx11");
                Check(StartupArguments.Parse(new[] { "-dx", "-gl" }).UseDirectX == false, "gl after dx");
            });

            yield return ("switches: -smoke values split by cmd.exe", () =>
            {
                var a = StartupArguments.Parse(new[] { "-smoke", "-smokeminutes", "480", "-smokeseed", "7", "-smokelots", "21", "22", "-smokeall" });
                Check(a.Smoke && a.SmokeMinutes == 480 && a.SmokeSeed == 7 && a.SmokeAll, "values");
                Check(a.SmokeLots != null && a.SmokeLots.Length == 2 && a.SmokeLots[0] == 21 && a.SmokeLots[1] == 22, "lots");
                Check(a.Ignored.Count == 0, "nothing ignored");
            });

            yield return ("switches: -smoke options", () =>
            {
                var a = StartupArguments.Parse(new[] { "-smoke", "-smokeminutes=30", "-smokelots=21,x,22", "-smokeseed=7" });
                Check(a.Smoke && a.SmokeMinutes == 30 && a.SmokeSeed == 7, "values");
                Check(a.SmokeLots != null && a.SmokeLots.Length == 2 && a.SmokeLots[0] == 21 && a.SmokeLots[1] == 22, "lots");
                Check(a.Ignored.Count == 0, "nothing ignored");
                var bad = StartupArguments.Parse(new[] { "-smokeminutes=abc", "-smokelots=zz", "-smokefoo" });
                Check(bad.Smoke && bad.SmokeMinutes == null && bad.SmokeLots == null && bad.Ignored.Count == 3, "malformed values are ignored, still a smoke run");
                Check(!StartupArguments.Parse(new[] { "-3d" }).Smoke, "off by default");
                var visual = StartupArguments.Parse(new[] { "-smokevisual" });
                Check(!a.SmokeVisual && visual.Smoke && visual.SmokeVisual && visual.Ignored.Count == 0, "headless unless -smokevisual");
                Check(StartupArguments.Parse(new[] { "-smokeall" }).SmokeAll && !a.SmokeAll, "-smokeall");
            });

            yield return ("switches: -path is left for PathArgument", () =>
            {
                var a = StartupArguments.Parse(new[] { "-pathC:\\Sims", "-path=D:\\Sims" });
                Check(a.Ignored.Count == 0, "not reported");
            });
        }
    }
}
