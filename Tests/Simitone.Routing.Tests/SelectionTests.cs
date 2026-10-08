using FSO.SimAntics.Engine.Routing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Routing.Tests
{
    /// <summary>
    /// Room reachability and route failure memory, used by object selection and free will (roadmap 4 and 6).
    /// </summary>
    public static class SelectionTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        private static Func<ushort, IEnumerable<ushort>> Graph(params (ushort from, ushort to)[] edges)
        {
            return (room) => edges.Where(x => x.from == room).Select(x => x.to);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("reachability follows portals and stops at rooms with no way in", () =>
            {
                //1 <-> 2 <-> 3 (a house with two doors), 4 walled in, 5 only has a way out (into 3)
                var g = Graph((1, 2), (2, 1), (2, 3), (3, 2), (5, 3));
                var rooms = VMReachability.Flood(1, 6, g);
                Check(rooms.SetEquals(new ushort[] { 1, 2, 3 }), "from 1: " + string.Join(",", rooms));
                Check(VMReachability.Flood(4, 6, g).SetEquals(new ushort[] { 4 }), "walled in room reaches only itself");
                Check(VMReachability.Flood(5, 6, g).SetEquals(new ushort[] { 5, 3, 2, 1 }), "one-way portal is followed");
            });

            yield return ("reachability is unknown (null) from room 0 or a room out of range", () =>
            {
                var g = Graph((1, 2));
                Check(VMReachability.Flood(0, 3, g) == null, "room 0");
                Check(VMReachability.Flood(7, 3, g) == null, "out of range");
                var rooms = VMReachability.Flood(1, 3, Graph((1, 0), (1, 9), (1, 2)));
                Check(rooms.SetEquals(new ushort[] { 1, 2 }), "portals to room 0 or out of range are ignored: " + string.Join(",", rooms));
            });

            yield return ("route failure memory expires after one Sim hour", () =>
            {
                var m = new VMRouteFailMemory();
                m.Add(new short[] { 10, 11 }, 100);
                Check(m.Contains(10, 100) && m.Contains(11, 100 + VMRouteFailMemory.DURATION - 1), "remembered within the hour");
                Check(!m.Contains(12, 100), "other objects are not remembered");
                Check(!m.Contains(10, 100 + VMRouteFailMemory.DURATION), "forgotten after the hour");
                Check(m.Count == 1, "expired entry removed when looked up, count " + m.Count);
                m.Add(new short[] { 11 }, 5000);
                Check(m.Contains(11, 5000 + VMRouteFailMemory.DURATION - 1), "a new failure extends the memory");
            });

            yield return ("route failure memory keeps at most 32 objects, dropping the oldest", () =>
            {
                var m = new VMRouteFailMemory();
                for (short i = 1; i <= 40; i++) m.Add(new short[] { i }, (uint)i);
                Check(m.Count == 32, "count " + m.Count);
                Check(!m.Contains(1, 41) && !m.Contains(8, 41), "oldest dropped");
                Check(m.Contains(9, 41) && m.Contains(40, 41), "newest kept");
            });
        }
    }
}
