using Simitone.Client.Utils;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>The order and bookkeeping of build/buy undo. Entries that touch the VM are exercised by hand (docs/testing).</summary>
    public static class UndoStackTests
    {
        private class Fake : BuildUndoStack.Entry
        {
            public string Name; public List<string> Log; public bool Works = true;
            public override bool Undo(FSO.SimAntics.VM vm) { if (Works) Log.Add("undo " + Name); return Works; }
            public override bool Redo(FSO.SimAntics.VM vm) { if (Works) Log.Add("redo " + Name); return Works; }
        }

        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("undo goes newest first, redo reverses it, a new change clears redo", () =>
            {
                var log = new List<string>();
                var stack = new BuildUndoStack();
                stack.Push(new Fake { Name = "a", Log = log });
                stack.Push(new Fake { Name = "b", Log = log });
                Check(stack.Undo(null) && stack.Undo(null) && !stack.Undo(null), "two undos then none");
                Check(string.Join(",", log) == "undo b,undo a", string.Join(",", log));
                Check(stack.Redo(null), "redo a");
                stack.Push(new Fake { Name = "c", Log = log });
                Check(!stack.CanRedo, "a new change clears redo");
                Check(stack.CanUndo, "c and a can be undone");
            });
            yield return ("an entry that no longer applies is skipped, not stuck", () =>
            {
                var log = new List<string>();
                var stack = new BuildUndoStack();
                stack.Push(new Fake { Name = "ok", Log = log });
                stack.Push(new Fake { Name = "gone", Log = log, Works = false });
                Check(stack.Undo(null), "falls through to the entry below");
                Check(string.Join(",", log) == "undo ok", string.Join(",", log));
                Check(!stack.CanUndo, "the dead entry was dropped");
            });
            yield return ("history is capped", () =>
            {
                var stack = new BuildUndoStack();
                var log = new List<string>();
                for (int i = 0; i < 150; i++) stack.Push(new Fake { Name = i.ToString(), Log = log });
                int n = 0;
                while (stack.Undo(null)) n++;
                Check(n == 100, "kept " + n);
            });
            yield return ("quick roof slider changes become one undo", () =>
            {
                var stack = new BuildUndoStack();
                stack.Push(new BuildUndoStack.RoofEntry { OldPitch = 0.5f, NewPitch = 0.6f, OldStyle = 3, NewStyle = 3 });
                stack.Push(new BuildUndoStack.RoofEntry { OldPitch = 0.6f, NewPitch = 0.7f, OldStyle = 3, NewStyle = 3 });
                stack.Push(new BuildUndoStack.RoofEntry { OldPitch = 0.7f, NewPitch = 0.9f, OldStyle = 3, NewStyle = 3 });
                int n = 0;
                //Undo needs a VM to send the command; count entries through CanUndo instead of running them
                Check(stack.CanUndo, "has an entry");
                var field = typeof(BuildUndoStack).GetField("UndoList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var list = (List<BuildUndoStack.Entry>)field.GetValue(stack);
                n = list.Count;
                Check(n == 1, "merged into " + n);
                var roof = (BuildUndoStack.RoofEntry)list[0];
                Check(roof.OldPitch == 0.5f && roof.NewPitch == 0.9f, $"from {roof.OldPitch} to {roof.NewPitch}");
            });
        }
    }
}
