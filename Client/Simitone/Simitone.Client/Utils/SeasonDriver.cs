using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Seasons;
using System;
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

        /// <summary>
        /// Debug cycler ("season cycle"): seconds between steps, 0 = off. Each step either jumps to the start of the next season
        /// or moves one day on (CycleDays), and forces the next kind of weather that season can have, so every effect can be seen.
        /// </summary>
        public static float CycleSeconds;
        public static bool CycleDays;

        private long LastSlot = long.MinValue;
        private bool Applied;
        private long LastPlantKey = long.MinValue;
        private bool LastRaining;
        private DateTime LastCycle = DateTime.UtcNow;
        private int CycleStep;
        private SeasonWeatherState? CycleWeather;

        public Season Current { get; private set; }

        /// <summary>True once the driver has set season state on the current lot's weather.</summary>
        public bool Active => Applied;

        /// <summary>Forget the lot being driven (a new lot has a new WeatherController).</summary>
        public void Reset()
        {
            LastSlot = long.MinValue;
            Applied = false;
            LastPlantKey = long.MinValue;
            CycleWeather = null;
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
                    vm.Context.World?.State?.SetFoliageSeason(-1);
                    Reset();
                }
                return;
            }

            var clock = vm.Context.Clock;
            var length = SeasonModel.LengthFromIndex(lengthIndex);
            var abs = SeasonModel.AbsoluteDay(clock.Year, clock.Month, clock.DayOfMonth);

            if (CycleSeconds > 0 && (DateTime.UtcNow - LastCycle).TotalSeconds >= CycleSeconds)
            {
                LastCycle = DateTime.UtcNow;
                var target = CycleDays ? abs + 1 : SeasonModel.NextSeasonStart(abs, length);
                SeasonModel.FromAbsoluteDay(target, out int cy, out int cm, out int cd);
                clock.Year = cy; clock.Month = cm; clock.DayOfMonth = cd;
                abs = target;
                CycleStep++;
                //a lap of four seasons shows one kind of weather per season; the next lap shows the next kind
                CycleWeather = SeasonWeather.Showcase(SeasonModel.SeasonOf(abs, length), CycleDays ? CycleStep : CycleStep / 4);
                LastSlot = long.MinValue;
            }
            if (CycleSeconds <= 0) CycleWeather = null;
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
                var forecast = CycleWeather ?? SeasonWeather.Forecast(season, slot);
                weather.SetWeather(SeasonWeather.ToWeatherData(forecast, season));
            }

            vm.Context.World?.State?.SetFoliageSeason((int)season);
            UpdatePlants(vm, weather, season, abs, clock.Hours, length);
            LastSlot = slot;
            Current = season;
            Applied = true;
        }

        /// <summary>
        /// Rain waters the outdoor plants, winter wilts and then kills them, early spring brings them back (PlantSeasons).
        /// Runs once per game hour, and again when the rain starts or stops.
        /// </summary>
        private void UpdatePlants(VM vm, WeatherController weather, Season season, long abs, int hour, int length)
        {
            bool raining = weather.WeatherType == WeatherType.Rain && weather.WeatherIntensity > 0.05f;
            long key = abs * 24 + hour;
            if (key == LastPlantKey && raining == LastRaining) return;
            LastPlantKey = key;
            LastRaining = raining;
            var action = PlantSeasons.Decide(season, SeasonModel.DayInSeason(abs, length), length, raining);
            PlantSeasons.Apply(vm, action);
        }
    }
}
