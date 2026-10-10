using FSO.SimAntics.Seasons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Routing.Tests
{
    /// <summary>The calendar to season mapping and the seasonal weather generator (pure functions, no graphics).</summary>
    public static class SeasonTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        private static Season Of(int month, int day, int length = 90, int year = 1997)
        {
            return SeasonModel.SeasonOf(year, month, day, length);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("seasons: standard length is three months per season starting in March", () =>
            {
                Check(Of(3, 1) == Season.Spring && Of(5, 30) == Season.Spring, "spring is months 3-5");
                Check(Of(6, 1) == Season.Summer && Of(8, 30) == Season.Summer, "summer is months 6-8");
                Check(Of(9, 1) == Season.Autumn && Of(11, 30) == Season.Autumn, "autumn is months 9-11");
                Check(Of(12, 1) == Season.Winter && Of(1, 1) == Season.Winter && Of(2, 30) == Season.Winter, "winter is 12, 1 and 2");
            });

            yield return ("seasons: winter continues over the new year", () =>
            {
                Check(SeasonModel.SeasonOf(1997, 12, 30, 90) == Season.Winter && SeasonModel.SeasonOf(1998, 1, 1, 90) == Season.Winter, "dec to jan");
                Check(SeasonModel.AbsoluteDay(1998, 1, 1) - SeasonModel.AbsoluteDay(1997, 12, 30) == 1, "consecutive days");
            });

            yield return ("seasons: a 30 day season makes every month a season (compact year)", () =>
            {
                Check(Of(3, 15, 30) == Season.Spring && Of(4, 1, 30) == Season.Summer && Of(5, 30, 30) == Season.Autumn
                    && Of(6, 1, 30) == Season.Winter && Of(7, 1, 30) == Season.Spring, "month = season");
                Check(Of(1, 1, 30) == Season.Autumn && Of(2, 1, 30) == Season.Winter, "the cycle also runs backwards from March");
            });

            yield return ("seasons: a 10 day season is a compact month", () =>
            {
                Check(Of(3, 1, 10) == Season.Spring && Of(3, 10, 10) == Season.Spring, "days 1-10 of March");
                Check(Of(3, 11, 10) == Season.Summer && Of(3, 21, 10) == Season.Autumn && Of(4, 1, 10) == Season.Winter && Of(4, 11, 10) == Season.Spring, "next three seasons then spring again");
            });

            yield return ("seasons: the cycle fits whole years at every offered length", () =>
            {
                foreach (var l in SeasonModel.SeasonLengths)
                    for (int d = 0; d < SeasonModel.DaysPerYear; d++)
                        Check(SeasonModel.SeasonOf(d, l) == SeasonModel.SeasonOf(d + SeasonModel.DaysPerYear, l), "length " + l + " day " + d);
            });

            yield return ("seasons: lengths are normalised to an offered value", () =>
            {
                Check(SeasonModel.NormaliseLength(0) == 90 && SeasonModel.NormaliseLength(-5) == 90, "unusable = standard");
                Check(SeasonModel.NormaliseLength(12) == 10 && SeasonModel.NormaliseLength(100) == 90 && SeasonModel.NormaliseLength(30) == 30, "nearest");
                Check(SeasonModel.LengthFromIndex(0) == 10 && SeasonModel.LengthFromIndex(4) == 90 && SeasonModel.LengthFromIndex(99) == 90 && SeasonModel.LengthFromIndex(-1) == 90, "index");
                Check(SeasonModel.SeasonLengths.All(l => 90 % l == 0), "every offered length divides 90");
            });

            yield return ("seasons: the first day of a season is day 0 of it and the day before is another season", () =>
            {
                foreach (var l in SeasonModel.SeasonLengths)
                    foreach (Season s in Enum.GetValues(typeof(Season)))
                    {
                        var d = SeasonModel.FirstDayOfSeason(s, l);
                        Check(SeasonModel.SeasonOf(d, l) == s && SeasonModel.DayInSeason(d, l) == 0, "start of " + s + " at " + l);
                        Check(SeasonModel.SeasonOf(d - 1, l) != s, "day before " + s + " at " + l);
                        Check(SeasonModel.DayInSeason(d - 1, l) == l - 1, "last day index at " + l);
                    }
            });

            yield return ("seasons: out of range dates from a save are clamped", () =>
            {
                Check(SeasonModel.DayOfYear(0, 0) == 0 && SeasonModel.DayOfYear(13, 31) == 359 && SeasonModel.DayOfYear(-4, 99) == 29, "clamped");
                Check(SeasonModel.MonthName(0) == "January" && SeasonModel.MonthName(12) == "December" && SeasonModel.MonthName(99) == "December", "month names");
            });

            yield return ("season weather: each season only rolls its own kinds and the odds sum to 100", () =>
            {
                foreach (Season s in Enum.GetValues(typeof(Season)))
                {
                    var total = Enum.GetValues(typeof(SeasonWeatherKind)).Cast<SeasonWeatherKind>().Sum(k => SeasonWeather.Chance(s, k));
                    Check(total == 100, s + " sums to " + total);
                }
                Check(SeasonWeather.Chance(Season.Winter, SeasonWeatherKind.Snow) > 50, "winter commonly snows");
                Check(SeasonWeather.Chance(Season.Winter, SeasonWeatherKind.Rain) == 0 && SeasonWeather.Chance(Season.Winter, SeasonWeatherKind.Heatwave) == 0, "no rain or heat in winter");
                Check(SeasonWeather.Chance(Season.Summer, SeasonWeatherKind.Storm) > 0 && SeasonWeather.Chance(Season.Summer, SeasonWeatherKind.Heatwave) > 0, "summer has storms and heatwaves");
                Check(SeasonWeather.Chance(Season.Autumn, SeasonWeatherKind.Rain) > 0 && SeasonWeather.Chance(Season.Autumn, SeasonWeatherKind.Fog) > 0, "autumn has rain and fog");
                Check(SeasonWeather.Chance(Season.Spring, SeasonWeatherKind.Rain) > 0 && SeasonWeather.Chance(Season.Spring, SeasonWeatherKind.Snow) == 0, "spring has rain but no snow");
            });

            yield return ("season weather: the forecast is deterministic and follows the table", () =>
            {
                foreach (Season s in Enum.GetValues(typeof(Season)))
                {
                    var counts = new Dictionary<SeasonWeatherKind, int>();
                    const int N = 20000;
                    for (long slot = 0; slot < N; slot++)
                    {
                        var a = SeasonWeather.Forecast(s, slot);
                        var b = SeasonWeather.Forecast(s, slot);
                        Check(a.Kind == b.Kind && a.Intensity == b.Intensity, "same slot, same weather");
                        Check(a.Intensity >= 0 && a.Intensity <= 100, "intensity range");
                        Check(SeasonWeather.Chance(s, a.Kind) > 0, s + " produced " + a.Kind);
                        Check((a.Kind == SeasonWeatherKind.Clear) == (a.Intensity == 0), "clear is the only zero-intensity weather");
                        counts[a.Kind] = (counts.ContainsKey(a.Kind) ? counts[a.Kind] : 0) + 1;
                    }
                    foreach (var kv in counts)
                    {
                        var pct = kv.Value * 100.0 / N;
                        Check(Math.Abs(pct - SeasonWeather.Chance(s, kv.Key)) < 3.0, s + " " + kv.Key + " was " + pct.ToString("0.0") + "%");
                    }
                }
            });

            yield return ("season weather: weather changes at most every six hours", () =>
            {
                Check(SeasonWeather.SlotOf(10, 0) == SeasonWeather.SlotOf(10, 5), "00:00-05:59 one slot");
                Check(SeasonWeather.SlotOf(10, 6) == SeasonWeather.SlotOf(10, 11) && SeasonWeather.SlotOf(10, 6) != SeasonWeather.SlotOf(10, 5), "next slot at 06:00");
                Check(SeasonWeather.SlotOf(10, 18) + 1 == SeasonWeather.SlotOf(11, 0), "slots are consecutive across days");
                Check(SeasonWeather.SlotOf(10, 99) == SeasonWeather.SlotOf(10, 23), "bad hour clamped");
            });

            yield return ("season weather: encoding into the weather value", () =>
            {
                Func<SeasonWeatherKind, int, Season, int> enc = (k, i, s) => SeasonWeather.ToWeatherData(new SeasonWeatherState { Kind = k, Intensity = i }, s) & 0xFFFF;
                Check(enc(SeasonWeatherKind.Clear, 0, Season.Summer) == 0x100, "clear is manual with no intensity");
                Check(enc(SeasonWeatherKind.Rain, 40, Season.Spring) == (0x100 | 40), "rain: type 0");
                Check(enc(SeasonWeatherKind.Storm, 90, Season.Summer) == (0x100 | (1 << 11) | 90), "storm adds thunder");
                Check(enc(SeasonWeatherKind.Snow, 60, Season.Winter) == (0x100 | (1 << 9) | 60 | (1 << 13)), "snow: type 1 and snow cover");
                Check(enc(SeasonWeatherKind.Fog, 50, Season.Autumn) == (0x100 | (3 << 9) | 50), "fog: type 3");
                Check(enc(SeasonWeatherKind.Heatwave, 80, Season.Summer) == (0x100 | (1 << 12) | 80), "heatwave bit");
                Check((enc(SeasonWeatherKind.Clear, 0, Season.Winter) & (1 << 13)) != 0, "winter always has snow cover, even when clear");
                Check((enc(SeasonWeatherKind.Rain, 40, Season.Autumn) & (1 << 13)) == 0, "no snow cover outside winter");
                Check((enc(SeasonWeatherKind.Rain, 500, Season.Spring) & 0xFF) == 100, "intensity capped at 100");
            });
        }
    }
}
