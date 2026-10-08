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
    ///   Arrow keys      pan (stops following)
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
            var simUI = sim?.WorldUI as AvatarComponent;

            //arrow key panning, at the same speed as edge scrolling.
            var kb = state.KeyboardState;
            var dir = Vector2.Zero;
            if (kb.IsKeyDown(Keys.Up)) dir.Y -= 1;
            if (kb.IsKeyDown(Keys.Down)) dir.Y += 1;
            if (kb.IsKeyDown(Keys.Left)) dir.X -= 1;
            if (kb.IsKeyDown(Keys.Right)) dir.X += 1;
            if (dir != Vector2.Zero)
            {
                var basis = world.GetScrollBasis(true);
                var move = dir.X * basis[0] + dir.Y * basis[1];
                if (dir.X != 0 && dir.Y != 0) move *= new Vector2(1, 0.5f);
                ws.CenterTile += move * 0.0625f * (60f / FSOEnvironment.RefreshRate);
                ws.ScrollAnchor = null;
            }

            //any scroll (edge, drag, keys) clears the anchor: that ends following.
            if (Following && ws.ScrollAnchor == null) Following = false;
            //keep following across Sim switches.
            if (Following && simUI != null && ws.ScrollAnchor != simUI) ws.ScrollAnchor = simUI;

            if (keys.Contains(Keys.C) && !state.CtrlDown && !state.ShiftDown && simUI != null) //Ctrl+Shift+C opens the cheat box
            {
                Previous = Capture(ws, lotControl);
                world.CenterTo(simUI);
            }
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
