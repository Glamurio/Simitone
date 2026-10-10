using Simitone.Windows.GameLocator;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>The -path switch. Regression: -path "C:\x" (value as the next argument) used to be ignored.</summary>
    public static class PathArgumentTests
    {
        private static void Expect(string[] args, bool present, string value)
        {
            var got = PathArgument.TryGet(args, out var v);
            if (got != present || v != value)
                throw new Exception($"[{string.Join(" | ", args)}] gave {got}/{v ?? "null"}, expected {present}/{value ?? "null"}");
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("-path with the folder as the next argument", () =>
                Expect(new[] { "-path", @"B:\Steam\steamapps\common\The Sims Legacy Collection" }, true, @"B:\Steam\steamapps\common\The Sims Legacy Collection"));
            yield return ("-path=folder and -path\"folder\" and -pathfolder", () =>
            {
                Expect(new[] { @"-path=C:\Sims" }, true, @"C:\Sims");
                Expect(new[] { "-path\"C:\\The Sims\"" }, true, @"C:\The Sims");
                Expect(new[] { @"-pathC:\Sims" }, true, @"C:\Sims");
            });
            yield return ("-path alone, or followed by another switch, has no value", () =>
            {
                Expect(new[] { "-path" }, true, null);
                Expect(new[] { "-path", "-3d" }, true, null);
            });
            yield return ("no -path switch; other switches are not mistaken for it", () =>
            {
                Expect(new[] { "-3d", "-hz60" }, false, null);
                Expect(new string[0], false, null);
            });
            yield return ("normalise gives forward slashes and one trailing slash", () =>
            {
                if (PathArgument.Normalise(@"C:\The Sims\") != "C:/The Sims/") throw new Exception(PathArgument.Normalise(@"C:\The Sims\"));
                if (PathArgument.Normalise("C:/Sims") != "C:/Sims/") throw new Exception("no trailing slash added");
            });
        }
    }
}
