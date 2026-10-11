using FSO.LotView.Utils;
using FSO.SimAntics.Seasons;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

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
                Check(SeasonalFoliage.Classify("Trees - Prune - Left") == FoliageKind.Blossom, "prune blossoms");
                Check(SeasonalFoliage.Classify("Trees - Superstar - Orange - 4") == FoliageKind.Blossom, "the orange tree blossoms, turns and goes bare");
                Check(SeasonalFoliage.Classify("Trees - Birch") == FoliageKind.Deciduous, "birch");
                Check(SeasonalFoliage.Classify("Trees - Oak - Front") == FoliageKind.Deciduous, "crape myrtle (labelled oak)");
                Check(SeasonalFoliage.Classify("Trees - Unleashed - Willow - 3") == FoliageKind.Deciduous, "willow");
                Check(SeasonalFoliage.Classify("Lamps - Street - Tree - C") == FoliageKind.Deciduous, "the Tree of Paradise has leaves");
                Check(SeasonalFoliage.Classify("Lamps - Street - Gas") == FoliageKind.None, "other street lamps are not trees");
                Check(SeasonalFoliage.Classify("Trees - Pine - Large - Left") == FoliageKind.Evergreen, "pine");
                Check(SeasonalFoliage.Classify("Trees - Barrel Palm - Front") == FoliageKind.Evergreen, "palm");
                Check(SeasonalFoliage.Classify("Shrub - Hedge Low") == FoliageKind.Evergreen, "hedge");
                Check(SeasonalFoliage.Classify("Shrub - Hedge Fame") == FoliageKind.Evergreen, "topiary hedge");
                Check(SeasonalFoliage.Classify("Shrub - Rose Bush") == FoliageKind.Evergreen, "shrubs keep their leaves");
                Check(SeasonalFoliage.Classify("Shrub - Beach - Century - B") == FoliageKind.Evergreen, "beach plants get snow too");
                Check(SeasonalFoliage.Classify("Shrub - Snowy - Tumbleweed") == FoliageKind.Evergreen && SeasonalFoliage.Classify("Shrub - Snowy - Cypress") == FoliageKind.Evergreen, "snowy shrubs");
                Check(SeasonalFoliage.Classify("Shrub - Gypsy") == FoliageKind.Evergreen && SeasonalFoliage.Classify("Trees - Spellbound - Autumn2 Left") == FoliageKind.Evergreen, "spellbound plants get snow");
                Check(SeasonalFoliage.Classify("Trees - Spellbound - Dead - 3") == FoliageKind.Evergreen, "dead trees get snow");
                Check(SeasonalFoliage.Classify("Shrub - Lily Pad") == FoliageKind.None && SeasonalFoliage.Classify("Shrub - Cat Tails") == FoliageKind.None, "water plants");
                Check(SeasonalFoliage.Classify("Trees - Cactus") == FoliageKind.None && SeasonalFoliage.Classify("Trees - Joshua Tree") == FoliageKind.None, "desert plants");
                Check(SeasonalFoliage.Classify("Flowers - Outdoor - Tulips") == FoliageKind.None, "flowers are not foliage");
                Check(SeasonalFoliage.Classify(null) == FoliageKind.None && SeasonalFoliage.Classify("") == FoliageKind.None, "no label");
            });

            yield return ("foliage: Sims Online objects are classified by GUID", () =>
            {
                Check(SeasonalFoliage.Classify("Trees - Cactus", 0x2FF38F53) == FoliageKind.None, "cactus");
                Check(SeasonalFoliage.Classify("Shrub - Juniper Bush", 0x154522AF) == FoliageKind.Evergreen, "juniper");
                Check(SeasonalFoliage.Classify("whatever", 0x15460FCA) == FoliageKind.Evergreen, "sword fern");
                Check(SeasonalFoliage.Classify("Trees - Pine - Snow", 0x56040A7B) == FoliageKind.None, "already snowy");
                Check(SeasonalFoliage.Classify("Trees - Birch", 0x12345678) == FoliageKind.Deciduous, "unknown GUIDs use the label");
            });

            yield return ("foliage: leaf type and fruit follow the object name", () =>
            {
                Check(SeasonalFoliage.LeafOf("Trees - Prune - Rear") == FoliageLeaf.Red, "prune");
                Check(SeasonalFoliage.LeafOf("Trees - Oak - Left") == FoliageLeaf.GreenAndPink && SeasonalFoliage.LeafOf("Trees - Crape Myrtle") == FoliageLeaf.GreenAndPink, "crape myrtle");
                Check(SeasonalFoliage.LeafOf("Trees - Birch") == FoliageLeaf.Green, "birch");
                Check(SeasonalFoliage.HasFruit("Trees - Apple Rear") && SeasonalFoliage.HasFruit("Trees - Superstar - Orange - 2"), "apples and oranges");
                Check(!SeasonalFoliage.HasFruit("Tree - Curb - Cherry") && !SeasonalFoliage.HasFruit("Trees - Birch"), "other trees");
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
                Check(SeasonalFoliage.LookFor(FoliageKind.Evergreen, 3) == FoliageLook.Frosted, "evergreens get snow in winter");
                SeasonalFoliage.SnowOnEvergreens = false;
                Check(SeasonalFoliage.LookFor(FoliageKind.Evergreen, 3) == FoliageLook.Normal, "and none when that is switched off");
                SeasonalFoliage.SnowOnEvergreens = true;
                Check(SeasonalFoliage.HasRealWinter(0x72BDBB7D) && SeasonalFoliage.HasRealWinter(0x3026C759) && !SeasonalFoliage.HasRealWinter(0x358D29CE), "real winter variants: Trees4/Shrubs3 yes, TreesHD stubs no");
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

            yield return ("foliage: bare fruit trees lose their fruit and crape myrtles their flowers", () =>
            {
                byte r = 190, g = 40, b = 40, a = 255; //apple
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Bare, 5, 5, FoliageLeaf.Green, true);
                Check(a == 0, "apple stays in winter");
                r = 230; g = 120; b = 35; a = 255; //orange
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Bare, 5, 5, FoliageLeaf.Green, true);
                Check(a == 0, "orange stays in winter");
                r = 116; g = 71; b = 25; a = 255; //orange-brown bark
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Bare, 5, 5, FoliageLeaf.Green, true);
                Check(a == 255 && r == 116, "bark is not fruit");
                r = 190; g = 40; b = 40; a = 255; //the same red on a tree without fruit
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Bare, 5, 5, FoliageLeaf.Green, false);
                Check(a == 255 && r == 190, "no fruit, nothing removed");
                r = 220; g = 110; b = 140; a = 255; //crape myrtle flower
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Bare, 5, 5, FoliageLeaf.GreenAndPink, false);
                Check(a == 0, "flower stays in winter");
                r = 220; g = 110; b = 140; a = 255;
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Autumn, 5, 5, FoliageLeaf.GreenAndPink, false);
                Check(a == 255 && r >= g && b < 120, "flower turns with the leaves in autumn");
                r = 190; g = 40; b = 40; a = 255;
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Autumn, 5, 5, FoliageLeaf.Green, true);
                Check(a == 255 && r == 190, "apples hang on in autumn");
            });

            yield return ("foliage: red prune leaves are leaves", () =>
            {
                Check(SeasonalFoliage.IsRedLeaf(140, 40, 45) && SeasonalFoliage.IsRedLeaf(90, 20, 25), "crimson");
                Check(!SeasonalFoliage.IsRedLeaf(90, 70, 50) && !SeasonalFoliage.IsRedLeaf(60, 60, 60) && !SeasonalFoliage.IsRedLeaf(30, 90, 30), "bark, grey and green");
                byte r = 140, g = 40, b = 45, a = 255;
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Bare, 1, 1, FoliageLeaf.Red, false);
                //a prune tree must change in winter: either dropped or turned to twig colour
                Check(a == 0 || !SeasonalFoliage.IsRedLeaf(r, g, b), "prune leaf survived winter");
                r = 140; g = 40; b = 45; a = 255;
                SeasonalFoliage.Recolor(ref r, ref g, ref b, ref a, FoliageLook.Spring, 1, 1, FoliageLeaf.Red, false);
                Check(a == 255 && g > r, "prune leaves are green in spring");
            });

            yield return ("foliage: spring blossoms put pink and white clusters among the green", () =>
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
                Check(blossom > 3600 * 0.08 && blossom < 3600 * 0.30, "blossom share out of range: " + blossom);
                Check(green > 3600 * 0.6, "the rest stays leafy: " + green);
            });

            yield return ("snow: lumps are cut from snow along their shaded undersides, edge-touching ones are dropped", () =>
            {
                const int w = 60, h = 30;
                var px = new Color[w * h];
                //two lumps side by side: bright tops (rows 6-9), grey undersides (rows 10-13), touching at x = 30
                for (int y = 6; y < 14; y++)
                    for (int x = 8; x < 52; x++)
                    {
                        byte v = (byte)(y < 10 ? 235 : 150);
                        px[y * w + x] = new Color(v, v, (byte)(v + 4 > 255 ? 255 : v + 4), (byte)255);
                    }
                for (int y = 6; y < 10; y++) px[y * w + 30] = new Color(150, 150, 154, 255); //crease between the two tops
                //a lump touching the frame edge
                for (int y = 20; y < 26; y++) for (int x = 0; x < 8; x++) px[y * w + x] = new Color(240, 240, 240, 255);
                var lumps = SnowLumps.CutLumps(px, w, h, 10);
                Check(lumps.Count == 2, "expected two lumps, got " + lumps.Count);
                Check(lumps.All(l => l.H == 8 && l.W >= 20), "each lump has its top and its underside: " + string.Join(",", lumps.Select(l => l.W + "x" + l.H)));
            });

            yield return ("snow: lumps settle on top surfaces and silhouette tops, not on vertical sides", () =>
            {
                //a box hedge: top face rows 10-29 (z rises going up the screen, as in the game's hedges), front face rows 30-79
                const int w = 80, h = 80;
                var px = new Color[w * h]; var z = new byte[w * h];
                for (int y = 10; y < 80; y++)
                    for (int x = 10; x < 70; x++)
                    {
                        px[y * w + x] = new Color(30, (byte)(100 + (x * 7 + y * 3) % 50), 30, 255);
                        z[y * w + x] = (byte)(y < 30 ? 150 - (y - 10) * 3 : 90 + (y - 30));
                    }
                var lib = new List<SnowLumps.Lump>();
                for (int k = 0; k < 6; k++)
                {
                    int lw = 8 + k, lh = 6;
                    var lp = new Color[lw * lh];
                    for (int i = 0; i < lp.Length; i++) lp[i] = new Color(240, 242, 246, 255);
                    lib.Add(new SnowLumps.Lump { W = lw, H = lh, Area = lw * lh, Px = lp });
                }
                Check(SnowLumps.Apply(px, z, w, h, 3, 42, lib, out var outPx, out var outZ, out var outH), "nothing placed");
                int pad = outH - h;
                Check(pad == 6, "the sprite grows by the tallest lump: " + pad);
                int top = 0, side = 0;
                for (int y = 0; y < outH; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = outPx[y * w + x];
                        if (c.R < 200 || c.A == 0) continue;
                        int oy = y - pad;
                        if (oy < 30) top++; else if (oy >= 36) side++; //rows 30-35: snow hanging over the rim
                    }
                Check(top > 300, "the top face should be snowy: " + top);
                Check(side == 0, "no snow on the vertical front face: " + side);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].A > 0 && outPx[(y + pad) * w + x].A == 0) Check(false, "a pixel of the plant went missing");
                Check(SnowLumps.Apply(px, z, w, h, 3, 42, lib, out var again, out var againZ, out var againH) && again.SequenceEqual(outPx) && againZ.SequenceEqual(outZ), "snow is not deterministic");
            });

            yield return ("bare trees: leaves go, the trunk stays, nothing new appears, same result every time", () =>
            {
                //a 2-tile "tree": a brown trunk (front tile) and a green crown split over two frames (rear tile on top)
                const int w = 60, h = 90;
                byte[] Frame(bool crownOnly)
                {
                    var rgba = new byte[w * h * 4];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int i = (y * w + x) * 4;
                            bool trunk = !crownOnly && x >= 27 && x <= 32 && y >= 30;
                            bool limb = !crownOnly && y >= 25 && y < 60 && Math.Abs((x - 30) - (60 - y) / 2) <= 1;
                            bool crown = (x - 30) * (x - 30) + (y - 30) * (y - 30) * 2 < 600 && !trunk && !limb;
                            if (trunk || limb) { rgba[i] = 110; rgba[i + 1] = 80; rgba[i + 2] = 60; rgba[i + 3] = 255; }
                            else if (crown) { rgba[i] = 40; rgba[i + 1] = (byte)(110 + (x * 5 + y * 3) % 60); rgba[i + 2] = 35; rgba[i + 3] = 255; }
                        }
                    return rgba;
                }
                var a = Frame(false); var b = Frame(true);
                var tiles = new List<BareBranches.BareTile>
                {
                    new BareBranches.BareTile { Rgba = a, W = w, H = h, X = 0, Y = 0 },
                    new BareBranches.BareTile { Rgba = b, W = w, H = h, X = 0, Y = -20 }
                };
                var sk = BareBranches.BuildSkeleton(tiles, 1, FoliageLeaf.Green, false);
                var outA = (byte[])a.Clone();
                BareBranches.Apply(sk, outA, null, w, h, 0, 0, false, FoliageLeaf.Green, false);
                int green = 0, trunkKept = 0;
                for (int i = 0; i < w * h; i++)
                {
                    int r = outA[i * 4], g = outA[i * 4 + 1], bl = outA[i * 4 + 2], al = outA[i * 4 + 3];
                    Check(!(a[i * 4 + 3] == 0 && al > 0), "a transparent pixel became visible");
                    Check(al <= a[i * 4 + 3], "alpha went up");
                    if (al > 0 && g > r + 25 && g > bl + 25) green++;
                    int x = i % w, y = i / w;
                    if (x >= 28 && x <= 31 && y >= 70 && al > 0) trunkKept++;
                }
                Check(green == 0, "leaves left: " + green);
                Check(trunkKept > 40, "the trunk should stay: " + trunkKept);
                var again = (byte[])a.Clone();
                BareBranches.Apply(BareBranches.BuildSkeleton(tiles, 1, FoliageLeaf.Green, false), again, null, w, h, 0, 0, false, FoliageLeaf.Green, false);
                Check(again.SequenceEqual(outA), "bare trees are not deterministic");
            });

            yield return ("foliage: only real trees drop leaves, and only warm saturated pixels are fallen leaves", () =>
            {
                Check(SeasonalFoliage.IsTree("Trees - Birch") && SeasonalFoliage.IsTree("Tree - Curb - Cherry") && SeasonalFoliage.IsTree("Trees - Oak - Left"), "trees");
                Check(!SeasonalFoliage.IsTree("Shrub - Rose Bush") && !SeasonalFoliage.IsTree("Lamps - Street - Tree - A") && !SeasonalFoliage.IsTree("Trees - Cactus") && !SeasonalFoliage.IsTree(null), "not trees");
                Check(SeasonalFoliage.IsFallenLeaf(240, 140, 20) && SeasonalFoliage.IsFallenLeaf(230, 190, 30) && SeasonalFoliage.IsFallenLeaf(200, 40, 30), "orange, yellow, red");
                Check(!SeasonalFoliage.IsFallenLeaf(120, 120, 125) && !SeasonalFoliage.IsFallenLeaf(40, 110, 40) && !SeasonalFoliage.IsFallenLeaf(60, 50, 45), "grey trunk, green leaf, dark bark");
            });
        }
    }
}
