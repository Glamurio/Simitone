using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Diagnostics;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Primitives;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Start-up option -smoke: plays every lot of the neighbourhood in turn at top speed, gives the Sims random things to do,
    /// answers dialogs, and writes a report of every exception, load problem and crash to Documents/Simitone/smoke/reports.
    /// Nothing in the player's own saves is touched (Program points the user folder at Documents/Simitone/smoke, which gets a fresh
    /// copy of the game's saves). Meant to be run unattended; see tools/smoke/run-smoke.cmd.
    /// </summary>
    public class SmokeTest
    {
        public static SmokeTest Active;

        private enum Phase { Waiting, NextLot, Loading, Running, Finished }

        public static int Minutes = 240;
        public static int[] OnlyLots = null;
        public static int Seed = 1234;

        private const int LOAD_TIMEOUT_SECONDS = 180;
        private const int STALL_SECONDS = 45;
        private const int MAX_LOT_SECONDS = 900;

        private Phase State = Phase.Waiting;
        private readonly Random Rand;
        private readonly string ReportPath;
        private readonly Stopwatch Total = Stopwatch.StartNew();
        private readonly List<LotResult> Results = new List<LotResult>();
        private List<int> Houses;
        private int Index = -1;
        private int WaitFrames;

        //per lot
        private LotResult Current;
        private Stopwatch LotTimer;
        private long StartTicks;
        private long LastTicks;
        private Stopwatch StallTimer;
        private int FramesUntilAction;
        private int FramesUntilDialog = -1;
        private VMDialogInfo PendingDialog;
        private bool Hooked;

        private class LotResult
        {
            public int House;
            public string Family;
            public string Status = "OK";
            public double Seconds;
            public long GameMinutes;
            public int Avatars, Objects, LoadErrors, Interactions, Dialogs;
            public long MemoryMB;
            public List<string> Problems = new List<string>();
            public Dictionary<string, int> Exceptions = new Dictionary<string, int>();
            public int RouteHardFails;
        }

        public SmokeTest()
        {
            Rand = new Random(Seed);
            var dir = Path.Combine(FSOEnvironment.UserDir, "reports");
            Directory.CreateDirectory(dir);
            ReportPath = Path.Combine(dir, $"smoke-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(ReportPath, $"Simitone smoke test, started {DateTime.Now:s}\r\n{GameSourceInfo.Describe()}\r\nMinutes per lot: {Minutes}, seed {Seed}\r\n\r\n");
            VMDiagnostics.OnActionEnded += ActionEnded;
            Simitone.Client.UI.Panels.UILotControl.ShowSimanticsExceptions = false;
        }

        public string Report { get { return ReportPath; } }

        private void Log(string line)
        {
            try { File.AppendAllText(ReportPath, line + "\r\n"); } catch (Exception) { }
        }

        private void ActionEnded(VMEntity ent, VMActionEnd end)
        {
            if (Current == null || end.Reason != VMActionEndReason.Exception) return;
            var key = $"{end.Name} on object {end.Callee} ({ent?.ToString()})";
            Current.Exceptions.TryGetValue(key, out var n);
            Current.Exceptions[key] = n + 1;
        }

        /// <summary>Called by the game screen every frame.</summary>
        public void Update(TS1GameScreen screen, UpdateState state)
        {
            try
            {
                switch (State)
                {
                    case Phase.Waiting:
                        if (screen.TS1NeighPanel != null && screen.vm == null && ++WaitFrames > 90) BuildHouseList();
                        break;
                    case Phase.NextLot: StartNextLot(screen); break;
                    case Phase.Loading: WaitForLoad(screen); break;
                    case Phase.Running: RunLot(screen); break;
                }
            }
            catch (Exception e)
            {
                Log("HARNESS ERROR in phase " + State + ": " + e);
                if (Current != null) { Current.Status = "HARNESS ERROR"; Current.Problems.Add(e.ToString()); EndLot(screen); }
                else State = Phase.NextLot;
            }
        }

        private void BuildHouseList()
        {
            var neigh = Content.Get().Neighborhood;
            var folder = Path.GetDirectoryName(neigh.GetHousePath(1));
            Houses = new List<int>();
            if (Directory.Exists(folder))
            {
                foreach (var file in Directory.GetFiles(folder, "House*.iff"))
                {
                    var name = Path.GetFileNameWithoutExtension(file).Substring(5);
                    if (int.TryParse(name, out var id) && (OnlyLots == null || OnlyLots.Contains(id))) Houses.Add(id);
                }
            }
            Houses.Sort();
            Log($"Found {Houses.Count} lot(s) in {folder}: {string.Join(", ", Houses)}\r\n");
            State = Phase.NextLot;
        }

        private void StartNextLot(TS1GameScreen screen)
        {
            Index++;
            if (Index >= Houses.Count) { Finish(); return; }
            Current = new LotResult { House = Houses[Index] };
            var family = Content.Get().Neighborhood.GetFamilyForHouse((short)Current.House);
            Current.Family = family == null ? "community / empty lot" : "family " + family.HouseNumber;
            //if the game dies while loading, this is the last line in the report.
            Log($"[{DateTime.Now:T}] LOADING lot {Current.House} ({Current.Family}) ...");
            LotTimer = Stopwatch.StartNew();
            Hooked = false;
            PendingDialog = null;
            FramesUntilDialog = -1;
            screen.SmokeLoad((short)Current.House);
            State = Phase.Loading;
        }

        private void WaitForLoad(TS1GameScreen screen)
        {
            var vm = screen.vm;
            if (vm == null || !vm.Ready)
            {
                if (LotTimer.Elapsed.TotalSeconds > LOAD_TIMEOUT_SECONDS)
                {
                    Current.Status = "FAIL: load timeout";
                    Current.Problems.Add($"lot did not become ready within {LOAD_TIMEOUT_SECONDS}s");
                    EndLot(screen);
                }
                return;
            }
            vm.OnDialog += OnDialog;
            Hooked = true;
            Current.LoadErrors = vm.LoadErrors.Count;
            foreach (var error in vm.LoadErrors.Take(20)) Current.Problems.Add("load error: " + error);
            screen.ChangeSpeedTo(3);
            StartTicks = LastTicks = vm.Context.Clock.Ticks;
            StallTimer = Stopwatch.StartNew();
            FramesUntilAction = 60;
            State = Phase.Running;
        }

        private void OnDialog(VMDialogInfo info)
        {
            if (info == null) { PendingDialog = null; FramesUntilDialog = -1; return; }
            if (info.Title == "SimAntics Exception!")
            {
                var text = (info.Message ?? "").Replace("\r\n", " ");
                if (text.Length > 300) text = text.Substring(0, 300);
                if (Current != null && !Current.Problems.Contains(text) && Current.Problems.Count < 40) Current.Problems.Add("exception dialog: " + text);
                return;
            }
            if (!info.Block) return;
            Current.Dialogs++;
            PendingDialog = info;
            FramesUntilDialog = 20;
        }

        private void RunLot(TS1GameScreen screen)
        {
            var vm = screen.vm;
            if (vm == null) { Current.Status = "FAIL: lot vanished"; EndLot(screen); return; }

            if (FramesUntilDialog >= 0 && --FramesUntilDialog < 0 && PendingDialog != null)
            {
                var type = PendingDialog.Operand == null ? VMDialogType.Message : PendingDialog.Operand.Type;
                byte code = 0;
                if (type == VMDialogType.YesNo) code = (byte)Rand.Next(2);
                else if (type == VMDialogType.YesNoCancel) code = (byte)Rand.Next(3);
                PendingDialog = null;
                vm.SendCommand(new VMNetDialogResponseCmd { ResponseCode = code, ResponseText = type == VMDialogType.NumericEntry ? "1" : "Smoke" });
            }

            var ticks = vm.Context.Clock.Ticks;
            var perMinute = Math.Max(1, vm.Context.Clock.TicksPerMinute);
            Current.GameMinutes = (ticks - StartTicks) / perMinute;

            if (ticks != LastTicks) { LastTicks = ticks; StallTimer.Restart(); }
            else if (StallTimer.Elapsed.TotalSeconds > STALL_SECONDS)
            {
                if (vm.SpeedMultiplier == 0) { screen.ChangeSpeedTo(3); StallTimer.Restart(); }
                else
                {
                    Current.Status = "FAIL: simulation stalled";
                    Current.Problems.Add($"the clock did not advance for {STALL_SECONDS}s at speed {vm.SpeedMultiplier}");
                    EndLot(screen);
                    return;
                }
            }

            if (--FramesUntilAction <= 0)
            {
                FramesUntilAction = 90 + Rand.Next(120);
                if (vm.SpeedMultiplier == 0) screen.ChangeSpeedTo(3);
                RandomInteraction(vm);
            }

            if (Current.GameMinutes >= Minutes) { EndLot(screen); }
            else if (LotTimer.Elapsed.TotalSeconds > MAX_LOT_SECONDS)
            {
                Current.Status = "SLOW: stopped after " + MAX_LOT_SECONDS + "s";
                EndLot(screen);
            }
        }

        private void RandomInteraction(VM vm)
        {
            var avatars = vm.Entities.OfType<VMAvatar>().Where(a => a.Position != LotTilePos.OUT_OF_WORLD && (a.Thread?.Queue?.Count ?? 0) == 0).ToList();
            if (avatars.Count == 0) return;
            var objects = vm.Entities.OfType<VMGameObject>().Where(o => o.Position != LotTilePos.OUT_OF_WORLD && o.Disabled == 0 && !(o is VMAvatar)).ToList();
            if (objects.Count == 0) return;
            var caller = avatars[Rand.Next(avatars.Count)];
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var target = objects[Rand.Next(objects.Count)];
                List<VMPieMenuInteraction> menu;
                try { menu = target.GetPieMenu(vm, caller, false, true); }
                catch (Exception e) { Current.Problems.Add($"pie menu of {target} threw: {e.GetType().Name}: {e.Message}"); continue; }
                if (menu == null || menu.Count == 0) continue;
                var action = menu[Rand.Next(menu.Count)];
                target.PushUserInteraction(action.ID, caller, vm.Context, action.Global, new short[] { action.Param0, 0, 0, 0 });
                Current.Interactions++;
                return;
            }
        }

        private void EndLot(TS1GameScreen screen)
        {
            var vm = screen.vm;
            if (vm != null)
            {
                Current.Avatars = vm.Entities.OfType<VMAvatar>().Count();
                Current.Objects = vm.Entities.Count(x => !(x is VMAvatar));
                Current.RouteHardFails = vm.Entities.OfType<VMAvatar>().Sum(a => a.Diagnostics?.RouteHardFails ?? 0);
                if (Hooked) vm.OnDialog -= OnDialog;
            }
            if (Current.Exceptions.Count > 0 && Current.Status == "OK") Current.Status = "EXCEPTIONS";
            Current.Seconds = LotTimer.Elapsed.TotalSeconds;
            GC.Collect();
            Current.MemoryMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            Results.Add(Current);
            Log(Describe(Current));
            Current = null;
            State = Phase.NextLot;
        }

        private static string Describe(LotResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"lot {r.House} ({r.Family}): {r.Status}");
            sb.AppendLine($"    {r.GameMinutes} game minutes in {r.Seconds:F0}s, {r.Avatars} avatars, {r.Objects} objects, {r.Interactions} random interactions, {r.Dialogs} dialogs answered, memory {r.MemoryMB} MB");
            if (r.LoadErrors > 0) sb.AppendLine($"    {r.LoadErrors} load error(s)");
            if (r.RouteHardFails > 0) sb.AppendLine($"    {r.RouteHardFails} failed route(s) (not an error by itself)");
            foreach (var e in r.Exceptions.OrderByDescending(x => x.Value)) sb.AppendLine($"    EXCEPTION x{e.Value}: {e.Key}");
            foreach (var p in r.Problems) sb.AppendLine("    - " + p);
            return sb.ToString();
        }

        private void Finish()
        {
            State = Phase.Finished;
            var bad = Results.Count(r => r.Status != "OK");
            Log($"=== {Results.Count} lot(s) played in {Total.Elapsed.TotalMinutes:F1} min: {Results.Count - bad} OK, {bad} with problems ===");
            Log("RESULT: " + (bad == 0 ? "PASS" : "FAIL"));
            Environment.Exit(bad == 0 ? 0 : 1);
        }

        /// <summary>Called from the unhandled exception handler: the report names the lot that was running.</summary>
        public void OnCrash(object exception)
        {
            Log($"=== CRASH while {(Current == null ? "between lots" : "playing lot " + Current.House + " (" + Current.Family + ")")}, after {(Current == null ? 0 : Current.GameMinutes)} game minutes ===");
            Log(exception?.ToString() ?? "(no exception)");
            if (Current != null) Log(Describe(Current));
            Log("RESULT: CRASH");
        }
    }
}
