using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Undo and redo for buy and build mode, phase 1: buying, moving and rotating objects (roadmap 12).
    /// The original game has an undo manager (Sims.exe RTTI names UndoManager and Object/Wall/Floor/... Undoable
    /// classes), so this restores a missing feature rather than adding a new one.
    ///
    /// Entries replay ordinary VM commands, so they go through the same checks as the player's own actions. Objects are
    /// found again by GUID and position when needed (the VM doesn't tell the client which object a buy created, and an
    /// undone-then-redone buy creates a new object ID). An entry whose object is gone or has been moved since is
    /// dropped. The history is per lot and is cleared when leaving buy/build mode, as an undo after living with an
    /// object would be a free refund.
    /// Walls, floors, terrain and the roof are undone by putting back a snapshot of the whole architecture with the money
    /// moved back exactly (VMNetUndoArchitectureCmd), so any tool that changes the architecture is covered.
    /// Not covered yet: selling (needs the object restored with its state).
    /// </summary>
    public class BuildUndoStack
    {
        public abstract class Entry
        {
            public abstract bool Undo(VM vm);
            public abstract bool Redo(VM vm);
        }

        private static VMEntity FindObject(VM vm, short hintID, uint guid, LotTilePos pos)
        {
            var hint = (hintID != 0) ? vm.GetObjectById(hintID) : null;
            if (Matches(hint, guid, pos)) return hint;
            return vm.Entities.FirstOrDefault(x => Matches(x, guid, pos));
        }

        private static bool Matches(VMEntity ent, uint guid, LotTilePos pos)
        {
            if (ent == null || ent.Dead || !(ent is VMGameObject)) return false;
            var group = ent.MultitileGroup;
            if (group == null || group.BaseObject != ent || group.GUID != guid) return false;
            var p = ent.Position;
            return p.x == pos.x && p.y == pos.y && p.Level == pos.Level;
        }

        public class BuyEntry : Entry
        {
            public uint GUID;
            public LotTilePos Position;
            public Direction Dir;
            /// <summary>Objects with this GUID that existed before the purchase, so the new one can be told from them.</summary>
            public HashSet<short> Before;

            public static HashSet<short> IDsWithGUID(VM vm, uint guid)
            {
                return new HashSet<short>(vm.Entities.Where(x => x is VMGameObject && x.MultitileGroup?.GUID == guid).Select(x => x.ObjectID));
            }

            /// <summary>
            /// The object this purchase created. Usually exactly where it was bought, but wall objects (windows, doors)
            /// and objects the game nudged don't always end up at the clicked position, so a new object with the same GUID
            /// close by also counts.
            /// </summary>
            private VMEntity FindBought(VM vm)
            {
                var exact = FindObject(vm, 0, GUID, Position);
                if (exact != null && (Before == null || !Before.Contains(exact.ObjectID))) return exact;
                if (Before == null) return exact;
                VMEntity best = null;
                var bestDist = int.MaxValue;
                foreach (var ent in vm.Entities)
                {
                    if (ent.Dead || !(ent is VMGameObject) || Before.Contains(ent.ObjectID)) continue;
                    var group = ent.MultitileGroup;
                    if (group == null || group.BaseObject != ent || group.GUID != GUID || ent.Position.Level != Position.Level) continue;
                    var dx = ent.Position.x - Position.x;
                    var dy = ent.Position.y - Position.y;
                    var dist = dx * dx + dy * dy;
                    if (dist <= 48 * 48 && dist < bestDist) { best = ent; bestDist = dist; }
                }
                return best;
            }

            public override bool Undo(VM vm)
            {
                var obj = FindBought(vm);
                if (obj == null) return false; //the purchase failed, or the object was moved or sold since.
                vm.SendCommand(new VMNetUndoBuyCmd() { ObjectID = obj.ObjectID, GUID = GUID });
                return true;
            }

            public override bool Redo(VM vm)
            {
                Before = IDsWithGUID(vm, GUID);
                vm.SendCommand(new VMNetBuyObjectCmd() { GUID = GUID, dir = Dir, level = Position.Level, x = Position.x, y = Position.y });
                return true;
            }
        }

        /// <summary>A wall, floor, terrain or other architecture change: swaps whole-lot snapshots (see VMNetUndoArchitectureCmd).</summary>
        public class ArchitectureEntry : Entry
        {
            public byte[] Before;
            public byte[] After; //taken when undone, for redo
            public int Cost;

            public override bool Undo(VM vm)
            {
                After = vm.Context.Architecture.Snapshot();
                vm.SendCommand(new VMNetUndoArchitectureCmd() { Snapshot = Before, Cost = -Cost });
                return true;
            }

            public override bool Redo(VM vm)
            {
                if (After == null) return false;
                vm.SendCommand(new VMNetUndoArchitectureCmd() { Snapshot = After, Cost = Cost });
                return true;
            }
        }

        public class RoofEntry : Entry
        {
            public float OldPitch, NewPitch;
            public uint OldStyle, NewStyle;
            public long LastChangeTicks;

            public override bool Undo(VM vm)
            {
                vm.SendCommand(new VMNetSetRoofCmd() { Pitch = OldPitch, Style = OldStyle });
                return true;
            }

            public override bool Redo(VM vm)
            {
                vm.SendCommand(new VMNetSetRoofCmd() { Pitch = NewPitch, Style = NewStyle });
                return true;
            }
        }

        public class MoveEntry : Entry
        {
            public short ObjectID;
            public uint GUID;
            public LotTilePos From;
            public Direction FromDir;
            public LotTilePos To;
            public Direction ToDir;

            private bool Move(VM vm, LotTilePos at, LotTilePos dest, Direction dir)
            {
                var obj = FindObject(vm, ObjectID, GUID, at);
                if (obj == null) return false;
                ObjectID = obj.ObjectID;
                vm.SendCommand(new VMNetMoveObjectCmd() { ObjectID = obj.ObjectID, dir = dir, level = dest.Level, x = dest.x, y = dest.y });
                return true;
            }

            public override bool Undo(VM vm) => Move(vm, To, From, FromDir);
            public override bool Redo(VM vm) => Move(vm, From, To, ToDir);
        }

        private const int MAX_ENTRIES = 100;
        private List<Entry> UndoList = new List<Entry>();
        private List<Entry> RedoList = new List<Entry>();

        //entries are added from the simulation thread (architecture changes) and read from the UI thread.
        private readonly object Sync = new object();
        //architecture snapshots are whole-lot copies, so keep fewer of them
        private const int MAX_ARCHITECTURE_ENTRIES = 40;

        public bool CanUndo { get { lock (Sync) return UndoList.Count > 0; } }
        public bool CanRedo { get { lock (Sync) return RedoList.Count > 0; } }

        public void Push(Entry entry)
        {
            lock (Sync)
            {
                //dragging the roof slider sends a command per step: one undo should take it back to where the drag began.
                var roof = entry as RoofEntry;
                var last = UndoList.Count > 0 ? UndoList[UndoList.Count - 1] as RoofEntry : null;
                if (roof != null && last != null && System.Diagnostics.Stopwatch.GetTimestamp() - last.LastChangeTicks < System.Diagnostics.Stopwatch.Frequency * 3 / 2)
                {
                    last.NewPitch = roof.NewPitch;
                    last.NewStyle = roof.NewStyle;
                    last.LastChangeTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    RedoList.Clear();
                    return;
                }
                if (roof != null) roof.LastChangeTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                UndoList.Add(entry);
                if (UndoList.Count > MAX_ENTRIES) UndoList.RemoveAt(0);
                if (entry is ArchitectureEntry)
                {
                    var count = 0;
                    for (int i = UndoList.Count - 1; i >= 0; i--)
                    {
                        if (UndoList[i] is ArchitectureEntry && ++count > MAX_ARCHITECTURE_ENTRIES) { UndoList.RemoveAt(i); }
                    }
                }
                RedoList.Clear();
            }
        }

        /// <summary>Connects the stack to the lot's architecture changes (walls, floors, terrain, roof).</summary>
        public void Attach(VM vm, System.Func<bool> enabled)
        {
            vm.OnArchitectureChange = (change) =>
            {
                if (!enabled()) return;
                if (change.Roof)
                {
                    Push(new RoofEntry() { OldPitch = change.OldPitch, NewPitch = change.NewPitch, OldStyle = change.OldStyle, NewStyle = change.NewStyle });
                }
                else if (change.Before != null)
                {
                    Push(new ArchitectureEntry() { Before = change.Before, Cost = change.Cost });
                }
            };
        }

        public void Clear()
        {
            lock (Sync)
            {
                UndoList.Clear();
                RedoList.Clear();
            }
        }

        /// <summary>Undoes the most recent action that can still be undone. Returns false if there was none.</summary>
        public bool Undo(VM vm)
        {
            while (true)
            {
                Entry entry;
                lock (Sync)
                {
                    if (UndoList.Count == 0) return false;
                    entry = UndoList[UndoList.Count - 1];
                    UndoList.RemoveAt(UndoList.Count - 1);
                }
                if (entry.Undo(vm))
                {
                    lock (Sync) RedoList.Add(entry);
                    return true;
                }
            }
        }

        public bool Redo(VM vm)
        {
            while (true)
            {
                Entry entry;
                lock (Sync)
                {
                    if (RedoList.Count == 0) return false;
                    entry = RedoList[RedoList.Count - 1];
                    RedoList.RemoveAt(RedoList.Count - 1);
                }
                if (entry.Redo(vm))
                {
                    lock (Sync) UndoList.Add(entry);
                    return true;
                }
            }
        }
    }
}
