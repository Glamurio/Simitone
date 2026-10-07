using FSO.SimAntics.Engine.Routing;
using FSO.SimAntics.Model.Routing;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Routing.Tests
{
    /// <summary>
    /// Geometry tests for VMShimmyPlanner. Coordinates are in 1/16 tile, as in VMRoutingFrame.AttemptWalk.
    /// Each scene is a 10x10 tile room. Most scenes split the room in two with a barrier whose only gap is the
    /// pinch under test, so a route between the halves exists only through that pinch.
    /// </summary>
    public static class ShimmyPlannerTests
    {
        private class Scene
        {
            public List<VMObstacle> Static = new List<VMObstacle>();
            public List<VMShimmyCandidate> Objects = new List<VMShimmyCandidate>();

            public Scene()
            {
                //room bounds, like the four bound obstacles in AttemptWalk
                Static.Add(new VMObstacle(-16, -16, 176, 0));
                Static.Add(new VMObstacle(-16, 160, 176, 176));
                Static.Add(new VMObstacle(-16, -16, 0, 176));
                Static.Add(new VMObstacle(160, -16, 176, 176));
            }

            public VMShimmyCandidate Obj(int x1, int y1, int x2, int y2, object group = null)
            {
                var raw = new VMObstacle(x1, y1, x2, y2);
                var c = new VMShimmyCandidate()
                {
                    Footprint = raw,
                    Inflated = new VMObstacle(x1 - 3, y1 - 3, x2 + 3, y2 + 3),
                    Owner = "obj" + Objects.Count,
                    Group = group
                };
                Objects.Add(c);
                return c;
            }

            public VMShimmyPlanner Planner()
            {
                var set = new VMObstacleSet(Static);
                foreach (var o in Objects) set.Add(o.Inflated);
                return new VMShimmyPlanner(set, Objects);
            }
        }

        //a barrier from the left wall to the right wall, broken only where the two objects meet.
        private static Scene Barrier(int lx1, int ly1, int lx2, int ly2, int rx1, int ry1, int rx2, int ry2)
        {
            var s = new Scene();
            s.Obj(lx1, ly1, lx2, ly2);
            s.Obj(rx1, ry1, rx2, ry2);
            //walls (pre-inflated, like Room.RoutingObstacles) overlapping each object's inflated footprint
            s.Static.Add(new VMObstacle(0, ly1 + 2, lx1, ly1 + 8));
            s.Static.Add(new VMObstacle(rx2, ry2 - 8, 160, ry2 - 2));
            return s;
        }

        private static readonly Point Top = new Point(40, 30);
        private static readonly Point Bottom = new Point(120, 130);

        private static string Describe(List<VMRouteLeg> legs)
        {
            if (legs == null) return "null";
            return string.Join(" | ", legs.Select(l => (l is VMShimmyRouteLeg ? "shimmy " : "rect ") + l.From + "->" + l.To));
        }

        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        private static void ExpectShimmyRoute(VMShimmyPlanner p, Point from, Point to, int shimmies)
        {
            var legs = p.Plan(from, to, 0);
            Check(legs != null, "expected a route, got none");
            Check(legs.Count(x => x is VMShimmyRouteLeg) == shimmies, $"expected {shimmies} shimmy legs: {Describe(legs)}");
            Check(legs.First().From == from && legs.Last().To == to, "route must start and end at the requested points: " + Describe(legs));
            for (int i = 1; i < legs.Count; i++)
                Check(legs[i - 1].To == legs[i].From, "legs must be contiguous: " + Describe(legs));
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("diagonal dining chairs (inflated corners touch) form a pinch", () =>
            {
                var s = Barrier(67, 67, 77, 77, 83, 83, 93, 93);
                var p = s.Planner();
                Check(p.Pinches.Count == 1, "expected one pinch, got " + p.Pinches.Count);
                ExpectShimmyRoute(p, Top, Bottom, 1);
                ExpectShimmyRoute(p, Bottom, Top, 1);
            });

            yield return ("chair diagonal to sofa (inflated footprints overlap) form a pinch", () =>
            {
                var s = Barrier(67, 67, 77, 77, 80, 80, 96, 96);
                var p = s.Planner();
                Check(p.Pinches.Count == 1, "expected one pinch, got " + p.Pinches.Count);
                ExpectShimmyRoute(p, Top, Bottom, 1);
            });

            yield return ("gap wide enough to walk: no pinch, legacy route", () =>
            {
                var s = Barrier(67, 67, 77, 77, 84, 84, 94, 94);
                var p = s.Planner();
                Check(p.Pinches.Count == 0, "expected no pinch");
                var legs = p.Plan(Top, Bottom, 0);
                Check(legs != null && legs.Count == 1 && legs[0] is VMRectRouteLeg, "expected a single rect leg: " + Describe(legs));

                //identical to routing without the planner
                var set = new VMObstacleSet(s.Static);
                foreach (var o in s.Objects) set.Add(o.Inflated);
                var legacy = new VMRectRouter(set).Route(Top, Bottom, 0);
                var a = ((VMRectRouteLeg)legs[0]).Rects.Select(r => r.ParentSource).ToList();
                var b = legacy.Select(r => r.ParentSource).ToList();
                Check(a.SequenceEqual(b), "planner route differs from legacy route");
            });

            yield return ("gap narrower than the minimum: no pinch, no route", () =>
            {
                var s = Barrier(67, 67, 77, 77, 78, 78, 88, 88);
                var p = s.Planner();
                Check(p.Pinches.Count == 0, "expected no pinch");
                Check(p.Plan(Top, Bottom, 0) == null, "expected no route");
            });

            yield return ("third object on a mouth invalidates the pinch", () =>
            {
                var s = Barrier(67, 67, 77, 77, 83, 83, 93, 93);
                s.Static.Add(new VMObstacle(80, 70, 88, 80)); //covers mouth (81,79)
                var p = s.Planner();
                Check(p.Pinches.Count == 0, "expected no pinch, got " + p.Pinches.Count);
                Check(p.Plan(Top, Bottom, 0) == null, "expected no route");
            });

            yield return ("objects in the same multitile group never pinch", () =>
            {
                var s = new Scene();
                var g = new object();
                s.Obj(67, 67, 77, 77, g);
                s.Obj(80, 80, 96, 96, g);
                Check(s.Planner().Pinches.Count == 0, "expected no pinch");
            });

            yield return ("start inside a pinch leaves through the mouth towards the goal", () =>
            {
                var s = Barrier(67, 67, 77, 77, 80, 80, 96, 96);
                var p = s.Planner();
                var start = new Point(78, 78);
                var legs = p.Plan(start, Bottom, 0);
                Check(legs != null, "expected a route");
                Check(legs[0] is VMShimmyRouteLeg && legs[0].From == start, "first leg must shimmy out: " + Describe(legs));
                Check(legs.Count(x => x is VMShimmyRouteLeg) == 1, "must not cross back: " + Describe(legs));
                var back = p.Plan(start, Top, 0);
                Check(back != null && back[0] is VMShimmyRouteLeg && back[0].To != legs[0].To, "other side uses the other mouth: " + Describe(back));
            });

            yield return ("start inside a raw footprint does not escape", () =>
            {
                var s = Barrier(67, 67, 77, 77, 80, 80, 96, 96);
                Check(s.Planner().Plan(new Point(72, 72), Bottom, 0) == null, "expected no route");
            });

            yield return ("pinch far from a direct route is pruned", () =>
            {
                var s = new Scene();
                s.Obj(67, 67, 77, 77);
                s.Obj(83, 83, 93, 93);
                var p = s.Planner();
                Check(p.Pinches.Count == 1, "expected one pinch");
                var legs = p.Plan(new Point(20, 20), new Point(40, 20), 0);
                Check(legs != null && legs.Count == 1 && legs[0] is VMRectRouteLeg, "expected the direct route: " + Describe(legs));
                Check(p.RoutesEvaluated == 1, "expected only the direct route to be evaluated, got " + p.RoutesEvaluated);
            });

            yield return ("short detour is preferred over shimmying", () =>
            {
                //two chairs touching diagonally in the middle of an open room: walking around costs little
                var s = new Scene();
                s.Obj(67, 67, 77, 77);
                s.Obj(83, 83, 93, 93);
                var legs = s.Planner().Plan(new Point(90, 60), new Point(70, 100), 0);
                Check(legs != null && legs.All(x => x is VMRectRouteLeg), "expected walking around: " + Describe(legs));
            });

            yield return ("long detour is replaced by shimmying", () =>
            {
                //like the barrier scenes, but the right wall leaves a gap at the far end of the room
                var s = new Scene();
                s.Obj(67, 67, 77, 77);
                s.Obj(83, 83, 93, 93);
                s.Static.Add(new VMObstacle(0, 69, 67, 75));
                s.Static.Add(new VMObstacle(93, 85, 145, 91));
                var p = s.Planner();
                ExpectShimmyRoute(p, Top, Bottom, 1);
            });

            yield return ("side-by-side chairs (straight gap) form a pinch", () =>
            {
                var s = new Scene();
                s.Obj(67, 67, 77, 77);
                s.Obj(83, 67, 93, 77);
                s.Static.Add(new VMObstacle(0, 69, 67, 75));
                s.Static.Add(new VMObstacle(93, 69, 160, 75));
                var p = s.Planner();
                Check(p.Pinches.Count == 1, "expected one pinch, got " + p.Pinches.Count);
                ExpectShimmyRoute(p, Top, Bottom, 1);
            });

            yield return ("long straight corridor is not a pinch", () =>
            {
                var s = new Scene();
                s.Obj(67, 40, 77, 77);
                s.Obj(83, 40, 93, 77);
                s.Static.Add(new VMObstacle(0, 42, 67, 48));
                s.Static.Add(new VMObstacle(93, 42, 160, 48));
                Check(s.Planner().Pinches.Count == 0, "expected no pinch");
            });

            yield return ("two pinches in sequence", () =>
            {
                var s = Barrier(67, 67, 77, 77, 83, 83, 93, 93);
                //second barrier lower down, with its own pinch
                s.Obj(27, 107, 37, 117);
                s.Obj(43, 123, 53, 133);
                s.Static.Add(new VMObstacle(0, 109, 27, 115));
                s.Static.Add(new VMObstacle(53, 125, 160, 131));
                var p = s.Planner();
                Check(p.Pinches.Count == 2, "expected two pinches, got " + p.Pinches.Count);
                ExpectShimmyRoute(p, Top, new Point(100, 150), 2);
            });

            yield return ("facing: the perpendicular nearest the current heading", () =>
            {
                var east = (float)(Math.PI / 2);
                var f = VMShimmyPlanner.ChooseFacing(east, 0, out var right);
                Check(right && Math.Abs(f) < 1e-5, "moving east while facing north is a step to the right");
                f = VMShimmyPlanner.ChooseFacing(east, (float)Math.PI, out right);
                Check(!right && Math.Abs(Math.Abs(f) - Math.PI) < 1e-5, "moving east while facing south is a step to the left");
                Check(Math.Abs(VMShimmyPlanner.Heading(new Point(0, 0), new Point(10, 0)) - east) < 1e-5, "heading east");
                Check(Math.Abs(VMShimmyPlanner.Heading(new Point(0, 10), new Point(0, 0))) < 1e-5, "heading north is -y");
            });
        }
    }
}
