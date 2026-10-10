using FSO.SimAntics.Primitives;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>Free will candidate filtering. Regression for pets spamming Hide.</summary>
    public static class AutonomyTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("lockout filter is off by default", () =>
            {
                Check(!FSO.SimAntics.VMFeatures.AutonomyLockout, "AutonomyLockout must default to off");
            });

            yield return ("lockout filter off: nothing is skipped", () =>
            {
                Check(!VMFindBestAction.SkipForLockout(false, false, 500), "locked object kept when filter off");
                Check(!VMFindBestAction.SkipForLockout(false, true, 500), "self kept when filter off");
            });

            yield return ("lockout filter on: locked objects skipped, own interactions never", () =>
            {
                Check(VMFindBestAction.SkipForLockout(true, false, 1), "locked object skipped");
                Check(!VMFindBestAction.SkipForLockout(true, false, 0), "unlocked object kept");
                Check(!VMFindBestAction.SkipForLockout(true, true, 500), "own interactions never skipped");
            });
        }
    }
}
