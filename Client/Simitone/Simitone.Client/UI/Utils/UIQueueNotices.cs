using FSO.SimAntics;
using FSO.SimAntics.Diagnostics;
using System.Collections.Generic;

namespace Simitone.Client.UI.Utils
{
    /// <summary>
    /// Remembers why recent queued actions ended (from VMDiagnostics.OnActionEnded), so the queue display can say why an
    /// action disappeared instead of it just vanishing (roadmap 5). Only the reasons a player can't otherwise see get a
    /// note: cancelled actions, actions that finished and route failures (which show a balloon) get none.
    /// </summary>
    public static class UIQueueNotices
    {
        private const int MAX_ENTRIES = 64;
        private static readonly object Lock = new object();
        private static Dictionary<(short, ushort), VMActionEndReason> Recent = new Dictionary<(short, ushort), VMActionEndReason>();
        private static Queue<(short, ushort)> Order = new Queue<(short, ushort)>();
        private static bool Hooked;

        /// <summary>Starts listening. The event is raised on the VM thread, so access is locked.</summary>
        public static void Hook()
        {
            if (Hooked) return;
            Hooked = true;
            VMDiagnostics.OnActionEnded += (ent, end) =>
            {
                if (Describe(end.Reason) == null) return;
                lock (Lock)
                {
                    var key = (ent.ObjectID, end.UID);
                    Recent[key] = end.Reason;
                    Order.Enqueue(key);
                    while (Order.Count > MAX_ENTRIES) Recent.Remove(Order.Dequeue());
                }
            };
        }

        /// <summary>The note for an action of this entity that just left the queue, or null if there is nothing to say.</summary>
        public static string Take(VMEntity owner, ushort uid)
        {
            if (owner == null) return null;
            VMActionEndReason reason;
            lock (Lock)
            {
                var key = (owner.ObjectID, uid);
                if (!Recent.TryGetValue(key, out reason)) return null;
                Recent.Remove(key);
            }
            return Describe(reason);
        }

        public static string Describe(VMActionEndReason reason)
        {
            switch (reason)
            {
                case VMActionEndReason.CheckFailedAtStart: return "Can't do that now";
                case VMActionEndReason.CalleeDeleted: return "Object is gone";
                case VMActionEndReason.Pruned: return "Dropped with it";
                case VMActionEndReason.OwnerDeadReset: return "Queue cleared";
                case VMActionEndReason.Exception: return "Error: Sim reset";
                default: return null;
            }
        }
    }
}
