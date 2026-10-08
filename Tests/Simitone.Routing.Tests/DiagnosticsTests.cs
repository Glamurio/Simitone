using FSO.SimAntics.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Routing.Tests
{
    public static class DiagnosticsTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("ring buffer keeps the most recent items, oldest first", () =>
            {
                var b = new VMRingBuffer<int>(3);
                Check(b.Count == 0 && b.InOrder().Count() == 0 && b.Last() == 0, "empty buffer");
                b.Add(1); b.Add(2);
                Check(b.InOrder().SequenceEqual(new[] { 1, 2 }) && b.Last() == 2, "partly filled: " + string.Join(",", b.InOrder()));
                b.Add(3); b.Add(4); b.Add(5);
                Check(b.Count == 3, "count is capped");
                Check(b.InOrder().SequenceEqual(new[] { 3, 4, 5 }) && b.Last() == 5, "wrapped: " + string.Join(",", b.InOrder()));
            });
        }
    }
}
