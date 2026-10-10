using FSO.Common;
using FSO.Common.Rendering.Framework.Model;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Simitone.Client.UI.Utils
{
    /// <summary>
    /// Keyboard camera shortcuts for live, buy and build mode (roadmap 13). Simitone had no keyboard panning and centred
    /// on the selected Sim only once per lot load; switching Sims already recentres (VMNetChangeControlCmd).
    ///   Arrow keys, WASD  pan (stops following)
    ///   Q, E            rotate the camera (also , and .)
    ///   C               centre on the selected Sim
    ///   F               follow the selected Sim (toggle; any scrolling stops it)
    ///   Backspace       back to where the camera was before the last jump
    ///   F5, F6, F7      jump to a saved view; Ctrl+F5/F6/F7 saves the current one (this session only)
    /// Keys are free in Simitone (checked against every live-game key handler); callers skip this while typing.
    /// </summary>
    public class UICameraShortcuts
    {
        private struct View
        {
            public Vector2 Center;
            public sbyte Level;
            public WorldRotation Rotation;
            public float Zoom;
        }

        /// <summary>Keyboard pan speed choices (SimitoneSettings.PanSpeed is the index). Edge scrolling is not affected.</summary>
        public static readonly float[] PanSpeeds = new float[] { 0.75f, 1f, 1.5f, 2.25f };
        private static float PanSpeedMultiplier
        {
            get
            {
                var i = Simitone.Client.Utils.SimitoneSettings.Default.PanSpeed;
                return PanSpeeds[System.Math.Max(0, System.Math.Min(PanSpeeds.Length - 1, i))];
            }
        }

        private static readonly Keys[] BookmarkKeys = new Keys[] { Keys.F5, Keys.F6, Keys.F7 };
        private View?[] Bookmarks = new View?[BookmarkKeys.Length];
        private View? Previous;
        private bool Following;

        public void Update(UpdateState state, World world, Panels.UILotControl lotControl)
        {
            if (world == null || lotControl == null) return;
            var ws = world.State;
            var keys = state.NewKeys;
            var sim = lotControl.ActiveEntity as VMAvatar;
            //A hiding pet is in the void (-2048, -2048): never centre or follow the camera onto it.
            var simUI = (sim == null || sim.IsOffLot) ? null : sim.WorldUI as AvatarComponent;

            //arrow key / WASD panning. The direction is normalised on screen, so diagonals are not faster than straight lines.
            var kb = state.KeyboardState;
            var dir = Vector2.Zero;
            var wasd = !state.CtrlDown && !state.AltDown; //Ctrl+S, Ctrl+A etc. belong to other features
            if (kb.IsKeyDown(Keys.Up) || (wasd && kb.IsKeyDown(Keys.W))) dir.Y -= 1;
            if (kb.IsKeyDown(Keys.Down) || (wasd && kb.IsKeyDown(Keys.S))) dir.Y += 1;
            if (kb.IsKeyDown(Keys.Left) || (wasd && kb.IsKeyDown(Keys.A))) dir.X -= 1;
            if (kb.IsKeyDown(Keys.Right) || (wasd && kb.IsKeyDown(Keys.D))) dir.X += 1;
            if (dir != Vector2.Zero)
            {
                var move = world.ScreenToScroll(dir);
                ws.CenterTile += move * 0.0625f * PanSpeedMultiplier * (60f / FSOEnvironment.RefreshRate);
                ws.ScrollAnchor = null;
            }

            //any scroll (edge, drag, keys) clears the anchor: that ends following.
            if (Following && ws.ScrollAnchor == null && simUI != null) Following = false;
            //keep following across Sim switches.
            if (Following && simUI != null && ws.ScrollAnchor != simUI) ws.ScrollAnchor = simUI;

            if (keys.Contains(Keys.C) && !state.CtrlDown && !state.ShiftDown && simUI != null) //Ctrl+Shift+C opens the cheat box
            {
                Previous = Capture(ws, lotControl);
                world.CenterTo(simUI);
            }
            if (Following && simUI == null && sim != null && ws.ScrollAnchor != null) ws.ScrollAnchor = null; //selected Sim went off lot: stay put
            if (keys.Contains(Keys.F) && simUI != null)
            {
                Following = !Following;
                ws.ScrollAnchor = Following ? simUI : null;
            }
            if (keys.Contains(Keys.Back) && Previous != null)
            {
                var back = Previous.Value;
                Previous = Capture(ws, lotControl);
                Restore(back, world, lotControl);
            }
            for (int i = 0; i < BookmarkKeys.Length; i++)
            {
                if (!keys.Contains(BookmarkKeys[i])) continue;
                if (state.CtrlDown) Bookmarks[i] = Capture(ws, lotControl);
                else if (Bookmarks[i] != null)
                {
                    Previous = Capture(ws, lotControl);
                    Restore(Bookmarks[i].Value, world, lotControl);
                }
            }
        }

        private static View Capture(WorldState ws, Panels.UILotControl lotControl)
        {
            return new View() { Center = ws.CenterTile, Level = ws.Level, Rotation = ws.Rotation, Zoom = lotControl.TargetZoom };
        }

        private void Restore(View view, World world, Panels.UILotControl lotControl)
        {
            var ws = world.State;
            Following = false;
            ws.ScrollAnchor = null;
            //the rotation setter reprojects the centre, so rotate first (see WorldState).
            if (ws.Rotation != view.Rotation)
            {
                ws.DisableSmoothRotation = true;
                ws.Rotation = view.Rotation;
                ws.DisableSmoothRotation = false;
            }
            lotControl.TargetZoom = view.Zoom;
            if (ws.Level != view.Level) ws.Level = view.Level;
            ws.CenterTile = view.Center;
        }
    }
}
