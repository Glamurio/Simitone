using FSO.LotView.Utils;
using FSO.SimAntics.Seasons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Routing.Tests
{
    /// <summary>The calendar helpers of the season cycler, what the seasons do to plants, and the seasonal tree recolour (all pure).</summary>
    public static class PlantFoliageTests
    {
        private static void Check(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }

        public static IEnumerable<(string, Action)> All()
        {
            yield return ("cycler: FromAbsoluteDay inverts AbsoluteDay", () =>
            {
                foreach (var (y, m, d) in new[] { (1997, 6, 1), (1997, 12, 30), (1998, 1, 1), (2000, 2, 17), (0, 1, 1) })
                {
                    SeasonModel.FromAbsoluteDay(SeasonModel.AbsoluteDay(y, m, d), out int y2, out int m2, out int d2);
                    Check(y == y2 && m == m2 && d == d2, y + "-" + m + "-" + d + " came back as " + y2 + "-" + m2 + "-" + d2);
                }
            });

            yield return ("cycler: the next season starts on its first day, at every length", () =>
            {
                foreach (var len in SeasonModel.SeasonLengths)
                {
                    for (long day = 700; day < 700 + 400; day += 7)
                    {
                        var next = SeasonModel.NextSeasonStart(day, len);
                        Check(next > day, "moves forward");
                        Check(SeasonModel.DayInSeason(next, len) == 0, "lands on a first day (len " + len + ")");
                        Check(SeasonModel.SeasonOf(next, len) != SeasonModel.SeasonOf(day, len), "season changes (len " + len + ")");
                    }
                }
            });

            yield return ("cycler: four steps visit all four seasons", () =>
            {
                long day = SeasonModel.AbsoluteDay(1997, 6, 4);
                var seen = new HashSet<Season>();
                for (int i = 0; i < 4; i++) { seen.Add(SeasonModel.SeasonOf(day, 30)); day = SeasonModel.NextSeasonStart(day, 30); }
                Check(seen.Count == 4, "all seasons");
            });

            yield return ("cycler: the weather showcase never shows clear skies and covers every other kind", () =>
            {
                foreach (Season s in Enum.GetValues(typeof(Season)))
                {
                    var shown = new HashSet<SeasonWeatherKind>();
                    for (int i = 0; i < 12; i++)
                    {
                        var w = SeasonWeather.Showcase(s, i);
                        Check(w.Kind != SeasonWeatherKind.Clear, "no clear skies in the showcase");
                        Check(SeasonWeather.Chance(s, w.Kind) > 0, s + " cannot have " + w.Kind);
                        shown.Add(w.Kind);
                    }
                    foreach (SeasonWeatherKind k in Enum.GetValues(typeof(SeasonWeatherKind)))
                        if (k != SeasonWeatherKind.Clear && SeasonWeather.Chance(s, k) > 0) Check(shown.Contains(k), s + " never shows " + k);
                }
            });

            yield return ("calendar file: a date round-trips through its text", () =>
            {
                Check(SeasonCalendarFile.Format(1997, 12, 3) == "1997 12 3", "format");
                Check(SeasonCalendarFile.TryParse("1997 12 3", out int y, out int m, out int d) && y == 1997 && m == 12 && d == 3, "parse");
                Check(SeasonCalendarFile.TryParse(" 2001\r\n7 30 \r\n", out y, out m, out d) && y == 2001 && m == 7 && d == 30, "whitespace is ignored");
            });

            yield return ("calendar file: nonsense is rejected so a damaged file cannot set the clock", () =>
            {
                foreach (var bad in new[] { null, "", "   ", "1997 13 1", "1997 0 1", "1997 5 31", "1997 5 0", "1997 5", "a b c", "1997 5 1 2", "-4 5 1" })
                    Check(!SeasonCalendarFile.TryParse(bad, out _, out _, out _), "accepted '" + bad + "'");
            });

            yield return ("plants: only the outdoor flowers are seasonal", () =>
            {
                Check(PlantSeasons.IsOutdoorPlant("Flowers - Outdoor - Tulips"), "tulips");
                Check(PlantSeasons.IsOutdoorPlant("Flowers - Outdoor - Jasmine"), "unleashed jasmine");
                Check(PlantSeasons.IsOutdoorPlant("Flowers - Outdoor - Gypsy2"), "spellbound flowers");
                Check(!PlantSeasons.IsOutdoorPlant("Plant - Floor - Rubber"), "indoor plants stay as they are");
                Check(!PlantSeasons.IsOutdoorPlant("Trees - Apple"), "trees are not flowers");
                Check(!PlantSeasons.IsOutdoorPlant(null), "null label");
            });

            yield return ("plants: winter wilts for the first third and kills after, whatever the weather", () =>
            {
                Check(PlantSeasons.Decide(Season.Winter, 0, 30, false) == PlantAction.Wilt, "day 0 wilts");
                Check(PlantSeasons.Decide(Season.Winter, 9, 30, true) == PlantAction.Wilt, "day 9 of 30 wilts");
                Check(PlantSeasons.Decide(Season.Winter, 10, 30, false) == PlantAction.Kill, "day 10 of 30 kills");
                Check(PlantSeasons.Decide(Season.Winter, 29, 30, true) == PlantAction.Kill, "rain does not save plants in winter");
            });

            yield return ("plants: spring brings them back, then rain waters them", () =>
            {
                Check(PlantSeasons.Decide(Season.Spring, 0, 30, false) == PlantAction.Revive, "first day revives");
                Check(PlantSeasons.Decide(Season.Spring, 9, 30, true) == PlantAction.Revive, "still reviving");
                Check(PlantSeasons.Decide(Season.Spring, 10, 30, true) == PlantAction.Water, "rain waters later in spring");
                Check(PlantSeasons.Decide(Season.Spring, 10, 30, false) == PlantAction.None, "dry spring leaves them to the Sims");
            });

            yield return ("plants: rain waters in summer and autumn, dry weather does nothing", () =>
            {
                Check(PlantSeasons.Decide(Season.Summer, 5, 90, true) == PlantAction.Water, "summer rain");
                Check(PlantSeasons.Decide(Season.Autumn, 5, 90, true) == PlantAction.Water, "autumn rain");
                Check(PlantSeasons.Decide(Season.Summer, 5, 90, false) == PlantAction.None, "summer dry");
                Check(PlantSeasons.Decide(Season.Autumn, 5, 90, false) == PlantAction.None, "autumn dry");
            });

            yield return ("plants: an action only touches plants it would change", () =>
            {
                Check(PlantSeasons.Affects(PlantAction.Water, PlantSeasons.StateHealthy, 40), "dry healthy plant gets water");
                Check(!PlantSeasons.Affects(PlantAction.Water, PlantSeasons.StateHealthy, 100), "full plant is left alone");
                Check(PlantSeasons.Affects(PlantAction.Water, PlantSeasons.StateWilted, 20), "wilted plant gets water");
                Check(!PlantSeasons.Affects(PlantAction.Water, PlantSeasons.StateDead, 0), "dead plants need replanting, not water");
                Check(PlantSeasons.Affects(PlantAction.Wilt, PlantSeasons.StateHealthy, 100) && !PlantSeasons.Affects(PlantAction.Wilt, PlantSeasons.StateDead, 0), "wilt healthy only");
                Check(PlantSeasons.Affects(PlantAction.Kill, PlantSeasons.StateWilted, 25) && !PlantSeasons.Affects(PlantAction.Kill, PlantSeasons.StateDead, 0), "kill the living");
                Check(PlantSeasons.Affects(PlantAction.Revive, PlantSeasons.StateDead, 0) && !PlantSeasons.Affects(PlantAction.Revive, PlantSeasons.StateHealthy, 100), "revive the dead");
                Check(!PlantSeasons.Affects(PlantAction.None, PlantSeasons.StateDead, 0), "none touches nothing");
            });

            yield return ("foliage: trees and shrubs are classified by their object name", () =>
            {
                Check(SeasonalFoliage.Classify("Trees - Apple Front") == FoliageKind.Blossom, "apple blossoms");
                Check(SeasonalFoliage.Classify("Tree - Curb - Cherry") == FoliageKind.Blossom, "cherry blossoms");
                Check(SeasonalFoliage.Classify("Trees - Birch") == FoliageKind.Deciduous, "birch");
                Check(SeasonalFoliage.Classify("Trees - Unleashed - Willow - 3") == FoliageKind.Deciduous, "willow");
                Check(SeasonalFoliage.Classify("Shrub - Rose Bush") == FoliageKind.Deciduous, "rose bush");
                Check(SeasonalFoliage.Classify("Trees - Pine - Large - Left") == FoliageKind.Evergreen, "pine");
                Check(SeasonalFoliage.Classify("Trees - Barrel Palm - Front") == FoliageKind.Evergreen, "palm");
                Check(SeasonalFoliage.Classify("Shrub - Hedge Low") == FoliageKind.Evergreen, "hedge");
                Check(SeasonalFoliage.Classify("Trees - Superstar - Orange - 4") == FoliageKind.Evergreen, "orange tree");
                Check(SeasonalFoliage.Classify("Trees - Spellbound - Dead - 3") == FoliageKind.None, "spellbound trees keep their own look");
                Check(SeasonalFoliage.Classify("Shrub - Beach - Hau") == FoliageKind.None, "beach plants");
                Check(SeasonalFoliage.Classify("Lamps - Street - Tree") == FoliageKind.None, "a tree-shaped lamp is not a tree");
                Check(SeasonalFoliage.Classify("Flowers - Outdoor - Tulips") == FoliageKind.None, "flowers are not foliage");
                Check(SeasonalFoliage.Classify(null) == FoliageKind.None && SeasonalFoliage.Classify("") == FoliageKind.None, "no label");
            });

            yield return ("foliage: every kind has the right look in every season", () =>
            {
                Check(SeasonalFoliage.LookFor(FoliageKind.Blossom, 0) == FoliageLook.Blossom, "apple spring");
                Check(SeasonalFoliage.LookFor(FoliageKind.Deciduous, 0) == FoliageLook.Spring, "birch spring");
                Check(SeasonalFoliage.LookFor(FoliageKind.Evergreen, 0) == FoliageLook.Normal, "pine spring");
                foreach (var k in new[] { FoliageKind.Blossom, FoliageKind.Deciduous, FoliageKind.Evergreen })
                    Check(SeasonalFoliage.LookFor(k, 1) == FoliageLook.Normal, k + " summer is the original art");
                Check(SeasonalFoliage.LookFor(FoliageKind.Blossom, 2) == FoliageLook.Autumn && SeasonalFoliage.LookFor(FoliageKind.Deciduous, 2) == FoliageLook.Autumn, "autumn leaves");
                Check(SeasonalFoliage.LookFor(FoliageKind.Evergreen, 2) == FoliageLook.Normal, "pine autumn");
                Check(SeasonalFoliage.LookFor(FoliageKind.Blossom, 3) == FoliageLook.Bare && SeasonalFoliage.LookFor(FoliageKind.Deciduous, 3) == FoliageLook.Bare, "bare in winter");
                Check(SeasonalFoliage.LookFor(FoliageKind.Evergreen, 3) == FoliageLook.Frosted, "frosted pine");
                Check(SeasonalFoliage.LookFor(FoliageKind.None, 3) == FoliageLook.Normal, "no foliage, no look");
                Check(SeasonalFoliage.LookFor(FoliageKind.Deciduous, -1) == FoliageLook.Normal, "seasons off");
            });

            yield return ("foliage: green pixels are leaves, bark is not", () =>
            {
                Check(SeasonalFoliage.IsLeaf(20, 60, 25) && SeasonalFoliage.IsLeaf(140, 190, 50) && SeasonalFoliage.IsLeaf(50, 110, 90), "greens");
                Check(!SeasonalFoliage.IsLeaf(90, 60, 40) && !SeasonalFoliage.IsLeaf(200, 200, 195) && !SeasonalFoliage.IsLeaf(40, 40, 40), "brown, white and grey");
                Check(!SeasonalFoliage.IsLeaf(200, 40, 40), "the red of an apple");
            });

            yield return ("foliage: recolouring leaves bark and the normal look alone", () =>
            {
                foreach (FoliageLook look in Enum.GetValues(typeof(FoliageLook)))
                {
                    byte r = 90, g = 60, b = 40, a = 255;
                    SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, look, 3, 4);
                    Check(r == 90 && g == 60 && b == 40 && a == 255, "bark changed in " + look);
                }
                byte lr = 20, lg = 90, lb = 25, la = 255;
                SeasonalFoliage.Recolor(ref lr, ref lg, ref lb, ref la, FoliageLook.Normal, 3, 4);
                Check(lr == 20 && lg == 90 && lb == 25 && la == 255, "normal look changed a leaf");
            });

            yield return ("foliage: bare trees keep a share of twig pixels, none of them green", () =>
            {
                int kept = 0, total = 0;
                for (int x = 0; x < 60; x++)
                {
                    for (int y = 0; y < 60; y++)
                    {
                        byte r = 20, g = (byte)(70 + (x + y) % 50), b = 25, a = 255;
                        SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Bare, x, y);
                        total++;
                        if (a == 0) continue;
                        kept++;
                        Check(a == 255, "kept twig pixel must be opaque");
                        Check(!SeasonalFoliage.IsLeaf(r, g, b), "twig pixel " + x + "," + y + " is still green");
                        byte r2 = 20, g2 = (byte)(70 + (x + y) % 50), b2 = 25, a2 = 255;
                        SeasonalFoliage.Recolor(ref r2, ref g2, ref b2, ref a2, FoliageLook.Bare, x, y);
                        Check(r == r2 && g == g2 && b == b2 && a == a2, "not deterministic");
                    }
                }
                double share = kept * 100.0 / total;
                Check(share > 30 && share < 55, "kept share " + share + "% is outside 30-55%");
            });

            yield return ("foliage: autumn leaves are no longer green, and the same pixel always gets the same colour", () =>
            {
                var colours = new HashSet<int>();
                for (int x = 0; x < 40; x++)
                {
                    for (int y = 0; y < 40; y += 3)
                    {
                        byte r = 30, g = 100, b = 30, a = 255;
                        SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Autumn, x, y);
                        Check(a == 255, "autumn keeps the leaf");
                        Check(r >= g, "autumn leaf at " + x + "," + y + " is still green (" + r + "," + g + "," + b + ")");
                        byte r2 = 30, g2 = 100, b2 = 30, a2 = 255;
                        SeasonalFoliage.Recolor(ref r2, ref g2, ref b2, ref a2, FoliageLook.Autumn, x, y);
                        Check(r == r2 && g == g2 && b == b2, "not deterministic");
                        colours.Add(r << 16 | g << 8 | b);
                    }
                }
                Check(colours.Count >= 4, "autumn should mix yellows, oranges and reds, got " + colours.Count + " colours");
            });

            yield return ("foliage: spring blossoms put pink and white among the green", () =>
            {
                int blossom = 0, green = 0;
                for (int x = 0; x < 60; x++)
                {
                    for (int y = 0; y < 60; y++)
                    {
                        byte r = 30, g = 100, b = 30, a = 255;
                        SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Blossom, x, y);
                        if (r > g && b > 150) blossom++; else if (g > r) green++;
                    }
                }
                Check(blossom > 3600 * 0.25 && blossom < 3600 * 0.65, "blossom share out of range: " + blossom);
                Check(green > 3600 * 0.25, "the rest stays leafy: " + green);
            });

            yield return ("foliage: frosted evergreens stay visible, with some white", () =>
            {
                int white = 0;
                for (int x = 0; x < 60; x++)
                {
                    byte r = 20, g = 80, b = 30, a = 255;
                    SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Frosted, x, x * 2);
                    Check(a == 255, "frost does not remove needles");
                    if (r > 170 && g > 170 && b > 170) white++;
                }
                Check(white > 0 && white < 60, "some but not all pixels are snow, got " + white);
            });
        }
    }
}
