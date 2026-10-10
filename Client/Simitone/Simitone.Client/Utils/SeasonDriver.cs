using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Seasons;
using Microsoft.Xna.Framework;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Connects the VM calendar to the lot's weather: every update it works out the season, hands the season tint to the
    /// WeatherController and, whenever a new six hour weather slot starts, sets the weather for that slot. The weather is a
    /// pure function of the date (SeasonWeather), so there is nothing to save.
    /// The weather cheat takes over until "weather auto" is typed (ManualOverride).
    /// </summary>
    public class SeasonDriver
    {
        /// <summary>Set by the weather cheat: a manually chosen weather stays until "weather auto".</summary>
        public static bool ManualOverride;
        /// <summary>Set when the weather cheat hands control back: the next update sets the weather again.</summary>
        public static bool ForceReapply;

        private long LastSlot = long.MinValue;
        private bool Applied;

        public Season Current { get; private set; }

        /// <summary>True once the driver has set season state on the current lot's weather.</summary>
        public bool Active => Applied;

        /// <summary>Forget the lot being driven (a new lot has a new WeatherController).</summary>
        public void Reset()
        {
            LastSlot = long.MinValue;
            Applied = false;
            ManualOverride = false; //a cheat weather belongs to the lot it was typed on
        }

        public static Season SeasonNow(VMClock clock, int lengthIndex)
        {
            return SeasonModel.SeasonOf(clock.Year, clock.Month, clock.DayOfMonth, SeasonModel.LengthFromIndex(lengthIndex));
        }

        public void Update(VM vm, WeatherController weather, bool enabled, int lengthIndex)
        {
            if (weather == null) return;
            if (!enabled)
            {
                if (Applied)
                {
                    //seasons were switched off: remove the tint and hand the weather back to the original automatic mode.
                    weather.SetSeasonTint(Color.White, 0);
                    weather.SetWeather(0);
                    Reset();
                }
                return;
            }

            var clock = vm.Context.Clock;
            var length = SeasonModel.LengthFromIndex(lengthIndex);
            var abs = SeasonModel.AbsoluteDay(clock.Year, clock.Month, clock.DayOfMonth);
            var season = SeasonModel.SeasonOf(abs, length);

            if (!Applied || season != Current)
            {
                SeasonModel.SeasonTint(season, out byte r, out byte g, out byte b, out float amount);
                weather.SetSeasonTint(new Color(r, g, b), amount);
            }

            if (ForceReapply) { ForceReapply = false; LastSlot = long.MinValue; }
            var slot = SeasonWeather.SlotOf(abs, clock.Hours);
            if (!ManualOverride && (slot != LastSlot || !Applied || season != Current))
            {
                var forecast = SeasonWeather.Forecast(season, slot);
                weather.SetWeather(SeasonWeather.ToWeatherData(forecast, season));
            }
            LastSlot = slot;
            Current = season;
            Applied = true;
        }
    }
}
