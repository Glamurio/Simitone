using FSO.SimAntics.Utils;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>Superstar fame levels (generic Sims calls 34 and 36), using the table from Global.far FameObjectGlobals.iff BCON 8192.</summary>
    public static class FameTests
    {
        private static readonly ushort[] Table = new ushort[] { 0, 50, 75, 125, 250, 375, 525, 700, 825, 1000 };

        private static void Eq(int a, int b, string msg)
        {
            if (a != b) throw new Exception($"{msg}: got {a}, expected {b}");
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("score to level", () =>
            {
                Eq(VMTS1Fame.LevelForScore(0, Table), 0, "no score is no level");
                Eq(VMTS1Fame.LevelForScore(1, Table), 1, "first score is level 1");
                Eq(VMTS1Fame.LevelForScore(49, Table), 1, "49");
                Eq(VMTS1Fame.LevelForScore(50, Table), 2, "50");
                Eq(VMTS1Fame.LevelForScore(524, Table), 6, "524");
                Eq(VMTS1Fame.LevelForScore(1000, Table), 10, "max");
                Eq(VMTS1Fame.LevelForScore(5000, Table), 10, "over max");
                Eq(VMTS1Fame.LevelForScore(500, null), 0, "no table");
            });
            yield return ("promote and demote move one level at a time", () =>
            {
                int level = 0;
                if (!VMTS1Fame.Promote(ref level, 300, Table)) throw new Exception("promote 0 -> 1");
                Eq(level, 1, "after first promote");
                if (!VMTS1Fame.Promote(ref level, 300, Table)) throw new Exception("promote 1 -> 2");
                Eq(level, 2, "after second");
                while (VMTS1Fame.Promote(ref level, 300, Table)) { }
                Eq(level, 5, "300 is worth level 5");
                if (VMTS1Fame.Promote(ref level, 300, Table)) throw new Exception("no promote at the right level");
                if (VMTS1Fame.Demote(ref level, 300, Table)) throw new Exception("no demote at the right level");
                if (!VMTS1Fame.Demote(ref level, 10, Table)) throw new Exception("demote when the score fell");
                Eq(level, 4, "one level down");
            });
            yield return ("limits", () =>
            {
                int level = 10;
                if (VMTS1Fame.Promote(ref level, 1000, Table)) throw new Exception("promote past max");
                level = 0;
                if (VMTS1Fame.Demote(ref level, 0, Table)) throw new Exception("demote below 0");
                level = 3;
                if (!VMTS1Fame.Demote(ref level, 0, Table)) throw new Exception("a Sim with no score drops");
            });
        }
    }
}
