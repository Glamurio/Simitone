using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Utils
{
    /// <summary>
    /// The mouse wheel always zoomed the lot (UILotControlTouchHelper), even with the mouse over the buy/build
    /// catalog, and nothing else could use it (roadmap 11). Scrollable panels register here every frame they are
    /// updated; the lot control routes the wheel to the panel under the mouse instead of zooming.
    /// A panel that stops being updated (hidden, closed) drops out after a frame.
    /// </summary>
    public static class UIWheelRouting
    {
        private class Target
        {
            public UIElement Element;
            public Func<Vector2> Size;
            public Action<int> OnWheel;
            public int Frame;
        }

        private static List<Target> Targets = new List<Target>();
        private static int Frame;

        /// <summary>Call from the scrollable element's Update.</summary>
        public static void Register(UIElement element, Func<Vector2> size, Action<int> onWheel)
        {
            var existing = Targets.Find(x => x.Element == element);
            if (existing == null)
            {
                existing = new Target() { Element = element };
                Targets.Add(existing);
            }
            existing.Size = size;
            existing.OnWheel = onWheel;
            existing.Frame = Frame;
        }

        /// <summary>
        /// Called once per frame by the lot control with the wheel movement (0 if none). Returns true if a panel under
        /// the mouse took it.
        /// </summary>
        public static bool Route(Point mouse, int wheelDelta)
        {
            Frame++;
            Targets.RemoveAll(x => Frame - x.Frame > 2);
            if (wheelDelta == 0) return false;
            foreach (var target in Targets)
            {
                if (!target.Element.Visible) continue;
                var local = target.Element.GlobalPoint(mouse.ToVector2());
                var size = target.Size();
                if (local.X >= 0 && local.Y >= 0 && local.X < size.X && local.Y < size.Y)
                {
                    target.OnWheel(wheelDelta);
                    return true;
                }
            }
            return false;
        }
    }
}
