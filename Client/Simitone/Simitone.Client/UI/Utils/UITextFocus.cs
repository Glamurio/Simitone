using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework.Input;

namespace Simitone.Client.UI.Utils
{
    /// <summary>
    /// Keyboard hotkeys must be ignored while the player types into a text field (the cheat box, alert text entry,
    /// later the catalog search). Text fields read the same key presses the hotkey handlers see, nothing consumes them,
    /// so typing "giveMoney 100" used to change the game speed and Space cycled Sims.
    /// </summary>
    public static class UITextFocus
    {
        /// <summary>
        /// True if a text field that is still on screen has keyboard focus.
        /// FSO.UI never clears focus when a text field is hidden or removed (and doesn't clear Parent on removal), so
        /// GetFocus() alone would block hotkeys for good after the cheat box was used once. Instead, the focused field
        /// and all its ancestors must be visible and still attached, up to the current screen.
        /// </summary>
        private static bool LastButtons;

        /// <summary>
        /// Takes keyboard focus away from a text field when the player clicks anywhere outside it. UITextEdit takes focus
        /// on mouse down but nothing ever gave it back, so after clicking the catalog search box the hotkeys stayed dead
        /// until the box was hidden (switching mode). Call once per frame from the screen update.
        /// </summary>
        public static void ClearOnOutsideClick(UpdateState state)
        {
            var mouse = state.MouseState;
            var down = mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed;
            var pressed = down && !LastButtons;
            LastButtons = down;
            if (!pressed) return;

            var input = state.InputManager ?? GameFacade.Screens?.inputManager;
            var focus = input?.GetFocus() as UITextEdit;
            if (focus == null) return;
            var local = focus.GetMousePosition(mouse);
            if (local.X < 0 || local.Y < 0 || local.X >= focus.Width || local.Y >= focus.Height) input.SetFocus(null);
        }

        public static bool IsTyping(UpdateState state)
        {
            var focus = (state?.InputManager ?? GameFacade.Screens?.inputManager)?.GetFocus() as UITextEdit;
            if (focus == null) return false;
            UIElement elem = focus;
            var screen = GameFacade.Screens?.CurrentUIScreen;
            while (elem != null)
            {
                if (!elem.Visible) return false;
                if (elem == screen) return true;
                var parent = elem.Parent;
                if (parent == null) return false;
                if (!parent.GetChildren().Contains(elem)) return false;
                elem = parent;
            }
            return false;
        }
    }
}
