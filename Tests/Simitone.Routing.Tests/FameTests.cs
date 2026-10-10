using FSO.SimAntics.Utils;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>
    /// Superstar fame (generic Sims calls 34/36), against the rules disassembled from Sims.exe (see VMTS1Fame) and the
    /// tables in GameData/fame.iff (STR# 5 score, STR# 4 friend star power, STR# 6 skills), copied here as they ship.
    /// </summary>
    public static class FameTests
    {
        private static VMTS1Fame.Tables Shipped()
        {
            var t = new VMTS1Fame.Tables();
            t.ScoreNeeded = new int[] { 2, 9, 21, 35, 52, 78, 156, 300, 550, 900, 0 };
            t.FriendPowerNeeded = new int[] { 0, 0, 0, 0, 2, 4, 7, 11, 14, 18, 0 };
            var skills = new[] { "0,0,0,0,0,0", "0,0,1,0,0,1", "0,0,2,1,0,2", "0,0,3,2,0,3", "0,0,4,3,0,4", "0,0,6,4,0,4",
                "0,0,6,5,0,6", "0,0,7,6,0,7", "0,0,8,7,0,8", "0,0,10,8,0,9", "0,0,0,0,0,0" };
            for (int i = 0; i < 11; i++) t.SkillsNeeded[i] = Array.ConvertAll(skills[i].Split(','), int.Parse);
            return t;
        }

        private static int[] Skills(int cooking, int repair, int charisma, int body, int logic, int creativity)
            => new[] { cooking * 100, repair * 100, charisma * 100, body * 100, logic * 100, creativity * 100 };

        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static IEnumerable<(string, Action)> All()
        {
            var t = Shipped();
            yield return ("promote needs the score for the current level", () =>
            {
                Check(!VMTS1Fame.CanPromote(0, 1, Skills(0, 0, 0, 0, 0, 0), 0, t), "score 1 at level 0");
                Check(VMTS1Fame.CanPromote(0, 2, Skills(0, 0, 0, 0, 0, 0), 0, t), "score 2 at level 0");
            });
            yield return ("promote needs every skill for the current level", () =>
            {
                Check(!VMTS1Fame.CanPromote(1, 9, Skills(0, 0, 1, 0, 0, 0), 0, t), "creativity 0 < 1");
                Check(VMTS1Fame.CanPromote(1, 9, Skills(0, 0, 1, 0, 0, 1), 0, t), "charisma 1, creativity 1");
                Check(!VMTS1Fame.CanPromote(1, 9, new[] { 0, 0, 199, 0, 0, 99 }, 0, t), "99 points is skill 0");
            });
            yield return ("promote from level 4 needs friends with 4+ star power in total (half counts)", () =>
            {
                var s = Skills(0, 0, 4, 3, 0, 4);
                Check(!VMTS1Fame.CanPromote(4, 52, s, 3, t), "3 * 0.5 < 2");
                Check(VMTS1Fame.CanPromote(4, 52, s, 4, t), "4 * 0.5 >= 2");
            });
            yield return ("no promotion past level 10", () =>
            {
                Check(!VMTS1Fame.CanPromote(10, 1000, Skills(10, 10, 10, 10, 10, 10), 100, t), "level 10");
            });
            yield return ("demote when the score is below the previous level's need", () =>
            {
                Check(!VMTS1Fame.CanDemote(0, 0, t), "level 0");
                Check(!VMTS1Fame.CanDemote(3, 21, t), "21 keeps level 3");
                Check(VMTS1Fame.CanDemote(3, 20, t), "20 drops level 3");
            });
            yield return ("fame friends: both directions, daily value at the threshold", () =>
            {
                var a = new Dictionary<int, List<short>> { { 2, new List<short> { 50, 0, 10 } } };
                var b = new Dictionary<int, List<short>> { { 1, new List<short> { 50 } } };
                Check(VMTS1Fame.AreFameFriends(a, 1, b, 2, 50), "mutual 50");
                b[1][0] = 49;
                Check(!VMTS1Fame.AreFameFriends(a, 1, b, 2, 50), "one side 49");
                Check(!VMTS1Fame.AreFameFriends(a, 1, new Dictionary<int, List<short>>(), 2, 50), "no relationship back");
            });
            yield return ("FCNS constant lookup", () =>
            {
                var fcns = new FSO.Files.Formats.IFF.Chunks.STR();
                fcns.InsertString(0, new FSO.Files.Formats.IFF.Chunks.STRItem { Value = "random selection count: 4" });
                fcns.InsertString(1, new FSO.Files.Formats.IFF.Chunks.STRItem { Value = "friendship threshold: 50" });
                var v = VMTS1Fame.FindConstant(fcns, "friendship threshold");
                Check(v == 50f, "got " + v);
                Check(VMTS1Fame.FindConstant(fcns, "missing") == null, "missing constant");
            });
        }
    }
}
