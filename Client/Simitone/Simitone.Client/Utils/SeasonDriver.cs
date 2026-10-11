using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Seasons;
using System;
using System.IO;
using System.Linq;
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

        //what the driver last did, for the season_info diagnostic
        public static string LastAction = "none yet";
        public static int LastChanged;
        public static bool LastRainSeen;
        public static int UpdateCalls;

        /// <summary>Text for the season_info cheat: season state, plants, foliage and sprite counters.</summary>
        private static bool IsPlantish(string label)
        {
            var l = label.ToLowerInvariant();
            return l.StartsWith("plant") || l.StartsWith("flowers") || l.Contains("cactus") || l.Contains("tree") || l.Contains("shrub");
        }

        public static string Report(VM vm, int lengthIndex)
        {
            var c = vm.Context.Clock;
            var len = SeasonModel.LengthFromIndex(lengthIndex);
            var abs = SeasonModel.AbsoluteDay(c.Year, c.Month, c.DayOfMonth);
            int deciduous = 0, blossom = 0, evergreen = 0;
            var seen = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var e in vm.Entities)
            {
                var label = e?.Object?.OBJ?.ChunkLabel;
                var kind = FSO.LotView.Utils.SeasonalFoliage.Classify(label, e?.Object?.OBJ?.GUID ?? 0);
                if (label != null && (kind != FSO.LotView.Utils.FoliageKind.None || IsPlantish(label)))
                {
                    var key = (e.ToString() ?? "?") + " [" + label + "] " + kind;
                    seen[key] = seen.TryGetValue(key, out var n) ? n + 1 : 1;
                }
                switch (kind)
                {
                    case FSO.LotView.Utils.FoliageKind.Deciduous: deciduous++; break;
                    case FSO.LotView.Utils.FoliageKind.Blossom: blossom++; break;
                    case FSO.LotView.Utils.FoliageKind.Evergreen: evergreen++; break;
                }
            }
            return "Date " + c.Year + "-" + c.Month + "-" + c.DayOfMonth + " " + c.Hours + ":00, season " + SeasonModel.SeasonOf(abs, len)
                + ", day " + (SeasonModel.DayInSeason(abs, len) + 1) + " of " + len + "\n"
                + "Driver updates " + UpdateCalls + ", last plant action: " + LastAction + " (" + LastChanged + " plants), raining " + LastRainSeen + "\n"
                + PlantSeasons.Describe(vm) + "\n"
                + "Foliage objects: " + deciduous + " deciduous, " + blossom + " blossom, " + evergreen + " evergreen; world season " + (vm.Context.World?.State?.FoliageSeason) + "\n"
                + "Foliage sprites: requested " + FSO.LotView.Utils.SeasonalSprites.Requests + ", recoloured " + FSO.LotView.Utils.SeasonalSprites.Built
                + ", no frame " + FSO.LotView.Utils.SeasonalSprites.NoFrame + ", no pixels " + FSO.LotView.Utils.SeasonalSprites.NoPixels
                + (FSO.LotView.Utils.SeasonalSprites.NoPixels > 0 ? "\nLast failure: " + FSO.LotView.Utils.SeasonalSprites.LastFailure : "")
                + "\nPlants on this lot (name [label] kind):\n" + string.Join("\n", seen.Select(p => p.Key + " x" + p.Value).Take(24));
        }

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
            CalendarSynced = false;
            CycleWeather = null;
            ManualOverride = false; //a cheat weather belongs to the lot it was typed on
        }

        public static Season SeasonNow(VMClock clock, int lengthIndex)
        {
            return SeasonModel.SeasonOf(clock.Year, clock.Month, clock.DayOfMonth, SeasonModel.LengthFromIndex(lengthIndex));
        }

        public void Update(VM vm, WeatherController weather, bool enabled, int lengthIndex, string calendarPath = null)
        {
            if (weather == null) return;
            UpdateCalls++;
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
            if (calendarPath != null) SyncCalendar(clock, calendarPath);
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

        private bool CalendarSynced;
        private long CalendarDay = long.MinValue;

        /// <summary>
        /// One date for the whole neighbourhood: the first update on a lot takes the date from the neighbourhood's calendar file
        /// (the first lot ever visited writes it), after that the file follows this lot's clock whenever the day changes.
        /// The time of day stays per lot, as in the original game.
        /// </summary>
        private void SyncCalendar(VMClock clock, string path)
        {
            try
            {
                if (!CalendarSynced)
                {
                    CalendarSynced = true;
                    if (File.Exists(path) && SeasonCalendarFile.TryParse(File.ReadAllText(path), out int y, out int m, out int d))
                    {
                        clock.Year = y; clock.Month = m; clock.DayOfMonth = d;
                    }
                    CalendarDay = long.MinValue; //written below if missing or changed
                }
                var day = SeasonModel.AbsoluteDay(clock.Year, clock.Month, clock.DayOfMonth);
                if (day != CalendarDay)
                {
                    CalendarDay = day;
                    File.WriteAllText(path, SeasonCalendarFile.Format(clock.Year, clock.Month, clock.DayOfMonth));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Neighbourhood calendar: " + e.Message);
            }
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
            LastChanged = PlantSeasons.Apply(vm, action);
            LastAction = action + " at " + abs + " h" + hour;
            LastRainSeen = raining;
        }
    }
}
