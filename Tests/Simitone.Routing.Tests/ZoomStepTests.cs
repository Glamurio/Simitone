using Simitone.Client.UI.Utils;
using System;
using System.Collections.Generic;

namespace Simitone.Routing.Tests
{
    /// <summary>Stepped 2D wheel zoom.</summary>
    public static class ZoomStepTests
    {
        private static void Eq(float a, float b, string msg)
        {
            if (Math.Abs(a - b) > 1e-6) throw new Exception($"{msg}: {a} != {b}");
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("steps move one view distance and stop at the ends", () =>
            {
                Eq(ZoomSteps.Step(1f, -1), 0.5f, "in to medium");
                Eq(ZoomSteps.Step(0.5f, -1), 0.25f, "medium to far");
                Eq(ZoomSteps.Step(0.25f, -1), 0.25f, "far is the end");
                Eq(ZoomSteps.Step(0.25f, 1), 0.5f, "far to medium");
                Eq(ZoomSteps.Step(1f, 1), 1f, "near is the end");
            });
            yield return ("a zoom between distances (touch pinch) steps from the nearest one", () =>
            {
                Eq(ZoomSteps.Nearest(0.7f), 0.5f, "0.7");
                Eq(ZoomSteps.Nearest(1.8f), 1f, "1.8");
                Eq(ZoomSteps.Step(0.7f, 1), 1f, "0.7 up");
            });
            yield return ("wheel: one step per 120 units, reversing resets", () =>
            {
                int acc = 0;
                if (ZoomSteps.Accumulate(ref acc, 120) != 1) throw new Exception("one notch");
                if (ZoomSteps.Accumulate(ref acc, 40) != 0 || ZoomSteps.Accumulate(ref acc, 40) != 0) throw new Exception("partial");
                if (ZoomSteps.Accumulate(ref acc, 40) != 1) throw new Exception("partials add up");
                ZoomSteps.Accumulate(ref acc, 100);
                if (ZoomSteps.Accumulate(ref acc, -120) != -1) throw new Exception("direction change starts over");
            });
        }
    }
}
