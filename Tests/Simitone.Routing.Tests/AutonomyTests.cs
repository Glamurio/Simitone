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

            yield return ("off-lot positions (hiding pets) are detected", () =>
            {
                Check(FSO.SimAntics.VMEntity.IsOffLotPosition(FSO.LotView.Model.LotTilePos.OUT_OF_WORLD), "out of world is off lot");
                Check(!FSO.SimAntics.VMEntity.IsOffLotPosition(new FSO.LotView.Model.LotTilePos(40, 40, 1)), "in-world tile is on lot");
            });

            yield return ("catalog picture: exact id wins, a lone catalog-range BMP is the fallback", () =>
            {
                Check(FSO.Content.GameObject.PickCatalogBmpId(2000, new[] { 2000, 2001 }) == 2000, "exact id");
                Check(FSO.Content.GameObject.PickCatalogBmpId(2000, new[] { 2001 }) == 2001, "ChairDiningAOL: strings 2000, picture 2001");
                Check(FSO.Content.GameObject.PickCatalogBmpId(2000, new[] { 2003 }) == 2003, "SofaLoveSeatAOL: strings 2000, picture 2003");
                Check(FSO.Content.GameObject.PickCatalogBmpId(2000, new[] { 2001, 2002 }) == -1, "ambiguous: no guess");
                Check(FSO.Content.GameObject.PickCatalogBmpId(2000, new[] { 4000, 6000 }) == -1, "only non-catalog pictures");
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
