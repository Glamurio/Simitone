using FSO.LotView.Model;
using FSO.SimAntics;
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
    /// Not covered yet: selling (needs the object restored with its state), walls, floors, roofs and terrain.
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

            public override bool Undo(VM vm)
            {
                var obj = FindObject(vm, 0, GUID, Position);
                if (obj == null) return false; //the purchase failed, or the object was moved or sold since.
                vm.SendCommand(new VMNetUndoBuyCmd() { ObjectID = obj.ObjectID, GUID = GUID });
                return true;
            }

            public override bool Redo(VM vm)
            {
                vm.SendCommand(new VMNetBuyObjectCmd() { GUID = GUID, dir = Dir, level = Position.Level, x = Position.x, y = Position.y });
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

        public bool CanUndo => UndoList.Count > 0;
        public bool CanRedo => RedoList.Count > 0;

        public void Push(Entry entry)
        {
            UndoList.Add(entry);
            if (UndoList.Count > MAX_ENTRIES) UndoList.RemoveAt(0);
            RedoList.Clear();
        }

        public void Clear()
        {
            UndoList.Clear();
            RedoList.Clear();
        }

        /// <summary>Undoes the most recent action that can still be undone. Returns false if there was none.</summary>
        public bool Undo(VM vm)
        {
            while (UndoList.Count > 0)
            {
                var entry = UndoList[UndoList.Count - 1];
                UndoList.RemoveAt(UndoList.Count - 1);
                if (entry.Undo(vm))
                {
                    RedoList.Add(entry);
                    return true;
                }
            }
            return false;
        }

        public bool Redo(VM vm)
        {
            while (RedoList.Count > 0)
            {
                var entry = RedoList[RedoList.Count - 1];
                RedoList.RemoveAt(RedoList.Count - 1);
                if (entry.Redo(vm))
                {
                    UndoList.Add(entry);
                    return true;
                }
            }
            return false;
        }
    }
}
