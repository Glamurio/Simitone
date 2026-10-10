using Microsoft.Xna.Framework.Input;

namespace Simitone.Client.UI.Utils
{
    /// <summary>
    /// Keys that set the game speed. The number row and the numpad both work (riperiperi/Simitone#37: the numpad did nothing).
    /// P pauses; 0 / numpad 0 advances one frame, which the caller handles because it needs the VM.
    /// </summary>
    public static class GameSpeedKeys
    {
        /// <summary>The speed (0 = paused, 1-3) a key selects, or -1 if the key is not a speed key. Frame advance is not included.</summary>
        public static int SpeedFor(Keys key)
        {
            switch (key)
            {
                case Keys.D1: case Keys.NumPad1: return 1;
                case Keys.D2: case Keys.NumPad2: return 2;
                case Keys.D3: case Keys.NumPad3: return 3;
                case Keys.P: return 0;
                default: return -1;
            }
        }

        public static bool IsFrameAdvance(Keys key)
        {
            return key == Keys.D0 || key == Keys.NumPad0;
        }
    }
}
