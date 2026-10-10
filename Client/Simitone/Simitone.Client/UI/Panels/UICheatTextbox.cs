using FSO.Client;
using FSO.Client.GameContent;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Panels
{
    public class UICheatTextbox : UIContainer
    {
        private Dictionary<string, VMCheatContext.VMCheatType> cheatDefinitions = new Dictionary<string, VMCheatContext.VMCheatType>()
        {
            { "moveobjects", VMCheatContext.VMCheatType.MoveObjects},
            { "motherlode", VMCheatContext.VMCheatType.Budget },
            { "klapaucius", VMCheatContext.VMCheatType.Budget },
            { "rosebud", VMCheatContext.VMCheatType.Budget },
            // gives the user the submitted amount of money
            { "giveMoney", VMCheatContext.VMCheatType.Budget }
        };

        private UITextBox baseTextbox;
        private Texture2D baseTexture;
        private VM ts1VM;

        /// <summary>
        /// An empty UICheatTextbox
        /// </summary>
        public UICheatTextbox(FSO.SimAntics.VM vm) : this(vm, "")
        {
            
        }
        /// <summary>
        /// A UICheatTextbox with text
        /// </summary>
        /// <param name="initialText"></param>
        public UICheatTextbox(FSO.SimAntics.VM vm, string initialText)
        {
            ts1VM = vm;
            baseTextbox = new UITextBox()
            {
                CurrentText = initialText,
                FlashOnEmpty = true,
                MaxLines = 1,
                //Tooltip = "Cheaters never win",
                //FrameColor = Color.Transparent,
                ID = "UICheatTextboxBase",
                
            };
            baseTexture = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
            baseTexture.SetData(new Color[] { new Color((byte)67, (byte)93, (byte)90, (byte)255) });
            baseTextbox.SetBackgroundTexture(baseTexture, 0, 0, 0, 0);
            Size = new Vector2(200, 23);
            baseTextbox.SetSize(200, 23);
            Add(baseTextbox);
        }
        public override void Update(UpdateState state)
        {
            base.Update(state);
            var pressedKeys = state.KeyboardState.GetPressedKeys();         
            //not sure if ts1 allowed you to use right ctrl or right shift but i dont discriminate
            if ((pressedKeys.Contains(Keys.LeftControl) || pressedKeys.Contains(Keys.RightControl))
                && (pressedKeys.Contains(Keys.LeftShift) || pressedKeys.Contains(Keys.RightShift))
                && state.NewKeys.Contains(Keys.C)) //prevent change over multiple frames
            {
                Visible = !Visible;
                if (!Visible) state.InputManager.SetFocus(null);
                else state.InputManager.SetFocus(baseTextbox);
            }
            baseTextbox.Visible = Visible;
            //focus is not cleared when a text box is hidden; release it so hotkeys work again straight away.
            if (!Visible && state.InputManager?.GetFocus() == baseTextbox) state.InputManager.SetFocus(null);
            if (Visible)
            {
                if (state.NewKeys.Contains(Keys.Enter))
                {
                    commandEntered(baseTextbox.CurrentText, out bool shouldHide);
                    Visible = !shouldHide;
                }
            }               
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commandString"></param>
        private void commandEntered(string commandString, out bool shouldHide)
        {
            shouldHide = true;
            if (string.IsNullOrWhiteSpace(commandString)) return; // a blank textbox should close after hitting enter -- even if a command was never run.
            if (tryClientCommand(commandString))
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Click);
                return;
            }

            // Handle weather commands separately (they don't use VMCheatContext)
            if (trimRepetitions(commandString).StartsWith("weather", StringComparison.OrdinalIgnoreCase))
            {
                HandleWeatherCommand(commandString);
                baseTextbox.CurrentText = "";
                return;
            }

            if (trimRepetitions(commandString).StartsWith("season", StringComparison.OrdinalIgnoreCase))
            {
                HandleSeasonCommand(commandString);
                baseTextbox.CurrentText = "";
                return;
            }

            var cheat = new VMNetCheatCmd();
            var context = new VMCheatContext();
            var repetitions = getRepetitions(commandString);
            switch (trimRepetitions(commandString))
            {
                //These three are special cheatcodes that don't really match the parameterized cheats e.g. moveobjects on
                case "klapaucius":
                case "rosebud":
                    context.Amount = (int)VMCheatContext.BudgetCheatPresetAmount.KLAPAUCIUS;
                    context.CheatBehavior = VMCheatContext.VMCheatType.Budget;
                    break;
                case "motherlode":
                    context.Amount = (int)VMCheatContext.BudgetCheatPresetAmount.MOTHERLODE;
                    context.CheatBehavior = VMCheatContext.VMCheatType.Budget;
                    break;
                default: context = parseCommandString(commandString); break;
            }
            cheat.Context = context;
            shouldHide = false;
            if (cheat.Context == null) // the command was not recognized
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error); // in TS1 this was a dialog but a sound may be less intrusive
                return;
            }
            context.Repetitions = (byte)repetitions;
            var sndEvent = UISounds.Error;
            if (context.CheatBehavior != VMCheatContext.VMCheatType.InvalidCheat)
            {
                ts1VM.SendCommand(cheat);
                switch (context.CheatBehavior) // sound feedback
                {
                    case VMCheatContext.VMCheatType.Budget: sndEvent = UISounds.BuyPlace; break;
                    default: sndEvent = UISounds.Click; break;
                }
                shouldHide = true;
            }
            FSO.HIT.HITVM.Get().PlaySoundEvent(sndEvent);
        } 

        /// <summary>
        /// Client commands that are not sent to the VM as cheats. The toggles change (and save) the same settings as
        /// Options > Settings:
        ///   draw_routes [on|off]  - draw route rectangles, paths and shimmy pinches (same name as the original game's cheat)
        ///   shimmy [on|off]       - let Sims side-step through narrow gaps between objects
        ///   diagnostics [on|off]  - record route events and why actions ended
        ///   write_routes          - write the diagnostics report to Documents/Simitone/diagnostics
        /// Without on/off the setting is toggled.
        /// </summary>
        private bool tryClientCommand(string commandString)
        {
            var parts = commandString.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            bool? value = null;
            if (parts.Length > 1)
            {
                if (parts[1] == "on") value = true;
                else if (parts[1] == "off") value = false;
            }
            var settings = Simitone.Client.Utils.SimitoneSettings.Default;
            switch (parts[0])
            {
                case "draw_routes":
                    settings.DrawRoutes = value ?? !settings.DrawRoutes;
                    break;
                case "shimmy":
                    settings.Shimmy = value ?? !settings.Shimmy;
                    break;
                case "diagnostics":
                    settings.Diagnostics = value ?? !settings.Diagnostics;
                    break;
                case "write_routes":
                    var path = Simitone.Client.Utils.DiagnosticsWriter.Write(ts1VM);
                    ShowDiagnosticsWritten(path);
                    return true;
                default:
                    return false;
            }
            settings.Save();
            settings.ApplyToEngine();
            return true;
        }

        public static void ShowDiagnosticsWritten(string path)
        {
            Simitone.Client.UI.Panels.UIMobileAlert alert = null;
            alert = new Simitone.Client.UI.Panels.UIMobileAlert(new UIAlertOptions()
            {
                Title = "Diagnostics",
                Message = (path != null) ? ("Report written to:\n" + path) : "No lot is loaded, or the report could not be written.",
                Buttons = UIAlertButton.Ok((b) => alert.Close())
            });
            UIScreen.GlobalShowDialog(alert, true);
        }

        private String trimRepetitions(string input)
        {
            return input.Replace(";", "").Replace("!", "");
        }

        private int getRepetitions(string input)
        {
            var repetitions = input.Count(x => x == '!');
            if (repetitions == input.Count(x => x == ';'))
                return repetitions;
            return 0;
        }

        /// <summary>
        /// Creates a VMCheatContext which includes repetitions, parameters, and CheatBehavior
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private VMCheatContext parseCommandString(string command)
        {
            if (command.Length == 0)
                return null;
            var baseCmd = command;

            int NumberOfParameters = 1; // in the future if we want multiple parameters per cheat...
            
            string[] parameters = new string[NumberOfParameters];
            if (command.Contains(' ')) {
                baseCmd = command.Substring(0, command.IndexOf(' '));
                string parameterString = command.Substring(command.IndexOf(' ') + 1);
                for (int i = 0; i < NumberOfParameters; i++)
                {
                    var individualParameter = parameterString;
                    if (string.IsNullOrWhiteSpace(individualParameter))
                        break;
                    if (individualParameter.Contains(' '))
                    {
                        individualParameter = individualParameter.Substring(0, individualParameter.IndexOf(' '));
                        parameters[i] = individualParameter;
                        parameterString = parameterString.Substring(parameterString.IndexOf(' ') + 1);
                        continue;
                    }                    
                    parameters[i] = individualParameter;
                    break;
                }
            }
            trimRepetitions(baseCmd);
            if (!cheatDefinitions.TryGetValue(baseCmd, out VMCheatContext.VMCheatType cheatType))
            {
                // cheat not defined in cheatDefinitions
                return null;
            }
            VMCheatContext context = new VMCheatContext()
            {
                CheatBehavior = cheatType,                
            };
            foreach(var parameter in parameters)
            {
                switch (parameter)
                {
                    case "on": context.Modifier = true; break; // set modifier true
                    case "off": context.Modifier = false; break; // set modifer false
                    default:
                        if (int.TryParse(parameter, out int amount)) //check if the parameter is a number
                            context.Amount = amount; // if it is amount is set
                        break;
                }
            }
            return context;
        }

        /// <summary>"season spring|summer|autumn|winter": moves the calendar to the first day of that season (at the current season length).</summary>
        private void HandleSeasonCommand(string command)
        {
            var parts = command.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[1] == "cycle")
            {
                //"season cycle [seconds] [days]" / "season cycle off": steps through the seasons (or days) on a timer, forcing weather
                bool off = parts.Length >= 3 && parts[2] == "off";
                float seconds = 5;
                if (parts.Length >= 3 && !off && !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds)) seconds = 5;
                Simitone.Client.Utils.SeasonDriver.CycleSeconds = off ? 0 : Math.Max(0.5f, seconds);
                Simitone.Client.Utils.SeasonDriver.CycleDays = Array.IndexOf(parts, "days") >= 2;
                Simitone.Client.Utils.SeasonDriver.ManualOverride = false;
                Simitone.Client.Utils.SeasonDriver.ForceReapply = true;
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Click);
                return;
            }
            FSO.SimAntics.Seasons.Season season;
            if (parts.Length < 2 || ts1VM?.Context?.Clock == null || !Enum.TryParse(parts[1], true, out season) || !Enum.IsDefined(typeof(FSO.SimAntics.Seasons.Season), season))
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error);
                return;
            }
            var length = FSO.SimAntics.Seasons.SeasonModel.LengthFromIndex(Simitone.Client.Utils.SimitoneSettings.Default.SeasonLength);
            var day = FSO.SimAntics.Seasons.SeasonModel.FirstDayOfSeason(season, length);
            ts1VM.Context.Clock.Month = day / FSO.SimAntics.Seasons.SeasonModel.DaysPerMonth + 1;
            ts1VM.Context.Clock.DayOfMonth = day % FSO.SimAntics.Seasons.SeasonModel.DaysPerMonth + 1;
            Simitone.Client.Utils.SeasonDriver.ManualOverride = false;
            Simitone.Client.Utils.SeasonDriver.ForceReapply = true;
            FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Click);
        }

        private void HandleWeatherCommand(string command)
        {
            var parts = command.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error);
                return;
            }

            string weatherType = parts[1];
            int intensity = 50;

            if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedIntensity))
            {
                intensity = Math.Clamp(parsedIntensity, 0, 100);
            }

            short weatherData = 0;

            switch (weatherType)
            {
                case "rain":
                    weatherData = (short)((1 << 8) | (0 << 9) | intensity);
                    break;
                case "storm":
                case "thunder":
                    if (parts.Length < 3) intensity = 75;
                    weatherData = (short)((1 << 8) | (1 << 11) | (0 << 9) | intensity);
                    break;
                case "snow":
                    weatherData = (short)((1 << 8) | (1 << 9) | intensity);
                    break;
                case "fog":
                    if (parts.Length < 3) intensity = 60;
                    weatherData = (short)((1 << 8) | (3 << 9) | intensity);
                    break;
                case "heat":
                case "heatwave":
                    if (parts.Length < 3) intensity = 80;
                    weatherData = (short)((1 << 8) | (1 << 12) | intensity);
                    break;
                case "hail":
                    weatherData = (short)((1 << 8) | (2 << 9) | intensity);
                    break;
                case "clear":
                    weatherData = (short)(1 << 8);
                    break;
                case "auto":
                    weatherData = 0;
                    break;
                default:
                    FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error);
                    return;
            }

            //a chosen weather stays until "weather auto"; auto hands the sky back to the seasons (or the original random weather).
            Simitone.Client.Utils.SeasonDriver.ManualOverride = weatherType != "auto";
            Simitone.Client.Utils.SeasonDriver.ForceReapply = weatherType == "auto";
            if (ts1VM?.Context?.Blueprint?.Weather != null)
            {
                ts1VM.Context.Blueprint.Weather.SetWeather(weatherData);
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Click);
            }
            else
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error);
            }
        }
    }
}
