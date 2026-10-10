using FSO.LotView.Model;
using FSO.SimAntics.Primitives;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Utils;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>Regression tests for bugs reported on riperiperi/Simitone.</summary>
    public static class UpstreamIssueTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("#37 numpad 1-3 set the game speed like the number row, numpad 0 advances a frame", () =>
            {
                Check(GameSpeedKeys.SpeedFor(Keys.NumPad1) == 1 && GameSpeedKeys.SpeedFor(Keys.D1) == 1, "speed 1");
                Check(GameSpeedKeys.SpeedFor(Keys.NumPad2) == 2 && GameSpeedKeys.SpeedFor(Keys.D2) == 2, "speed 2");
                Check(GameSpeedKeys.SpeedFor(Keys.NumPad3) == 3 && GameSpeedKeys.SpeedFor(Keys.D3) == 3, "speed 3");
                Check(GameSpeedKeys.SpeedFor(Keys.P) == 0, "pause");
                Check(GameSpeedKeys.SpeedFor(Keys.A) == -1 && GameSpeedKeys.SpeedFor(Keys.NumPad4) == -1, "other keys");
                Check(GameSpeedKeys.IsFrameAdvance(Keys.NumPad0) && GameSpeedKeys.IsFrameAdvance(Keys.D0), "frame advance");
            });

            yield return ("#24 fire never spreads in front of an object that is off the lot", () =>
            {
                foreach (Direction d in new[] { Direction.NORTH, Direction.SOUTH, Direction.EAST, Direction.WEST })
                    Check(VMBurn.FrontOf(LotTilePos.OUT_OF_WORLD, d) == LotTilePos.OUT_OF_WORLD, "off-lot stays off-lot " + d);
            });

            yield return ("#24 the tile in front of an object on the lot is one tile away in its facing direction", () =>
            {
                var p = LotTilePos.FromBigTile(10, 10, 1);
                var s = VMBurn.FrontOf(p, Direction.SOUTH);
                var n = VMBurn.FrontOf(p, Direction.NORTH);
                var e = VMBurn.FrontOf(p, Direction.EAST);
                var w = VMBurn.FrontOf(p, Direction.WEST);
                Check(s.TileX == 10 && s.TileY == 11, "south");
                Check(n.TileX == 10 && n.TileY == 9, "north");
                Check(e.TileX == 11 && e.TileY == 10, "east");
                Check(w.TileX == 9 && w.TileY == 10, "west");
                Check(p.TileX == 10 && p.TileY == 10, "input not modified");
            });
        }
    }
}
