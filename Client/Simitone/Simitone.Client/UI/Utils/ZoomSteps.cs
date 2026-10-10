using System;

namespace Simitone.Client.UI.Utils
{
    /// <summary>
    /// The three fixed 2D view distances (far, medium, near), as TargetZoom values. The Steam version moves the mouse
    /// wheel one view distance per notch; Simitone zoomed continuously and then drifted to the nearest distance.
    /// </summary>
    public static class ZoomSteps
    {
        public static readonly float[] Levels = new float[] { 0.25f, 0.5f, 1f };

        /// <summary>The view distance nearest to a zoom value.</summary>
        public static float Nearest(float zoom)
        {
            var best = Levels[0];
            foreach (var level in Levels)
                if (Math.Abs(level - zoom) < Math.Abs(best - zoom)) best = level;
            return best;
        }

        /// <summary>One view distance in (direction &gt; 0) or out (direction &lt; 0) from the nearest one; stops at the ends.</summary>
        public static float Step(float zoom, int direction)
        {
            var index = Array.IndexOf(Levels, Nearest(zoom)) + Math.Sign(direction);
            return Levels[Math.Max(0, Math.Min(Levels.Length - 1, index))];
        }

        /// <summary>
        /// Collects wheel movement and returns -1, 0 or 1 once a whole notch (120 units) has built up, so smooth-scrolling
        /// mice and touchpads give one step per notch's worth of travel instead of one step per event.
        /// </summary>
        public static int Accumulate(ref int accumulated, int wheelDiff)
        {
            if (wheelDiff != 0 && Math.Sign(accumulated) != Math.Sign(wheelDiff)) accumulated = 0; //direction change
            accumulated += wheelDiff;
            if (Math.Abs(accumulated) < 120) return 0;
            var dir = Math.Sign(accumulated);
            accumulated = 0;
            return dir;
        }
    }
}
