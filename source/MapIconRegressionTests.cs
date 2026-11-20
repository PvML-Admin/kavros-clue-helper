using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KavrosClueHelper
{
    public static class MapIconRegressionTests
    {
        private static int assertions;
        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("FAILED: " + message);
        }
        public static int Run(string report)
        {
            try
            {
                string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                if (!File.Exists(Path.Combine(assets, "compass", "world.png"))) assets = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "assets"));
                if (!File.Exists(Path.Combine(assets, "compass", "world.png"))) assets = "/workspace/assets";
                Check(File.Exists(Path.Combine(assets, "compass", "world.png")), "world map is available for the map tests");
                Rectangle steady = new Rectangle(10, 20, 200, 80);
                bool show, hide, move, paint, known; Rectangle placed;
                GuidanceDrawing.Present(false, false, Rectangle.Empty, true, steady, true, out show, out hide, out move, out paint, out known, out placed);
                Check(show && !hide && move && paint, "the first puzzle frame is drawn once");
                bool show2, hide2, move2, paint2, known2; Rectangle placed2;
                GuidanceDrawing.Present(true, known, placed, true, steady, false, out show2, out hide2, out move2, out paint2, out known2, out placed2);
                Check(!show2 && !hide2 && !move2 && !paint2, "an unchanged puzzle is not hidden, shown, moved, or cleared");
                float[] fourK = PuzzleDetector.SearchOrder(2160);
                Check(fourK.Length >= 12 && Math.Abs(fourK[0] - 2F) < 0.01F, "a 4K frame searches the 2x interface scale first");
                Check(PuzzleDetector.SearchOrder(1080)[0] == 1F, "a 1080p frame searches the native template first");
                string preferred = LodestoneIcons.Resolve(assets, "Lumbridge lodestone");
                Check(preferred != null && preferred.Replace('\\', '/').Contains("/map-icons/lodestones/lumbridge.png"), "lodestone icons prefer assets/map-icons/lodestones");
                string compass = LodestoneIcons.CompassFile(assets);
                Check(compass.Replace('\\', '/').EndsWith("/map-icons/compass.png"), "compass image path is assets/map-icons/compass.png");
                Check(File.Exists(compass), "compass icon is installed");
                using (Bitmap icon = new Bitmap(compass))
                {
                    Check(icon.GetPixel(0, 0).A == 0 && icon.GetPixel(icon.Width - 1, icon.Height - 1).A == 0, "compass outside the circle is transparent");
                    Check(icon.GetPixel(icon.Width / 2, icon.Height / 2).A == 255, "compass centre is opaque");
                }
                using (Bitmap canvas = new Bitmap(80, 40))
                using (Graphics g = Graphics.FromImage(canvas))
                    LodestoneIcons.DrawCompass(g, SystemFonts.DefaultFont, new Rectangle(0, 0, 80, 40), Path.Combine(Path.GetTempPath(), "kavros-no-compass"));
                foreach (string line in File.ReadAllLines(Path.Combine(assets, "compass", "positions.tsv")))
                {
                    string[] fields = line.Split('\t');
                    if (fields.Length < 3 || fields[0].Length == 0) continue;
                    string file = LodestoneIcons.Resolve(assets, fields[0]);
                    Check(file != null && file.Replace('\\', '/').Contains("/map-icons/lodestones/"), "icon file for " + fields[0]);
                }
                Rectangle box = new Rectangle(8, 43, 444, 139);
                float span = Math.Max(72, Math.Max(20, 28) * 3);
                PointF screen;
                bool lumbridge = LodestoneIcons.InView(box, 3212, 3219, span, 0, 0, 3233, 3221, out screen);
                Check(lumbridge, "Lumbridge lodestone is inside the Lumbridge Castle grounds view");
                bool lunar = LodestoneIcons.InView(box, 3212, 3219, span, 0, 0, 2088, 3911, out screen);
                Check(!lunar, "Lunar Isle lodestone is outside the Lumbridge Castle grounds view");
                MasterClue charm = MasterClues.ById(240);
                Check(charm.Travel.Contains("Fastest:") && charm.Travel.Contains("Gnome Course") && charm.Travel.Contains("Alternatively:") && !charm.Travel.Contains("[VERIFY]"), "charm sprite route");
                using (MasterClueView view = new MasterClueView())
                {
                    view.SetBounds(0, 0, 460, 480);
                    view.SetClue(charm, assets);
                    Check(view.MapPath.Replace('\\', '/').EndsWith("/compass/world.png"), "master emote map is the world map");
                    Check(view.MapPath.IndexOf("master-maps", StringComparison.OrdinalIgnoreCase) < 0, "master emote map is not the local square");
                }
                var routes = File.ReadAllLines(Path.Combine(assets, "data", "scan-routes.tsv")).Select(l => l.Split('\t')).Where(f => f.Length == 2).ToDictionary(f => int.Parse(f[0]), f => f[1]);
                Check(routes[358] == "Not available on Kavros.", "Mos Le'Harmless is not available on Kavros");
                Check(routes[363].Contains("Monsters > Yaks") && routes[363].Contains("rope bridge") && routes[363].IndexOf("boat", StringComparison.OrdinalIgnoreCase) < 0, "Jatizso route uses the yak pen");
                foreach (MasterClue clue in MasterClues.All)
                {
                    Check(clue.Travel.Contains("Fastest:") && !clue.Travel.Contains("[VERIFY]") && !clue.Travel.Contains("Note:"), "master route " + clue.Id);
                    if (clue.Travel.IndexOf("Fairy ring", StringComparison.OrdinalIgnoreCase) >= 0)
                        Check(clue.Travel.Contains("Portable fairy ring can be made in ;;ig. An unlimited version can be bought for 5 Kavros dollars."), "fairy tooltip on master " + clue.Id);
                }
                foreach (var pair in routes)
                {
                    if (pair.Key == 358) continue;
                    Check(pair.Value.Contains("Fastest:") && !pair.Value.Contains("[VERIFY]") && !pair.Value.Contains("Note:"), "scan route " + pair.Key);
                    if (pair.Value.IndexOf("Fairy ring", StringComparison.OrdinalIgnoreCase) >= 0 || pair.Value.IndexOf("fairy ring", StringComparison.OrdinalIgnoreCase) >= 0)
                        Check(pair.Value.Contains("Portable fairy ring can be made in ;;ig. An unlimited version can be bought for 5 Kavros dollars."), "fairy tooltip on scan " + pair.Key);
                }
                var vision = new ScanVision(assets);
                ScanRegion mos = vision.Regions.Single(r => r.Id == 358);
                Check(mos.Route == "Not available on Kavros.", "loaded Mos Le'Harmless route");
                Check(vision.MatchEliteText("Scan this area. The orb is in range. 27 paces around Mos Le'Harmless.") == null, "the reader does not offer Mos Le'Harmless");
                ScanRegion falador = vision.MatchEliteText("Scan this area. The orb is in range. 22 paces around Falador.");
                Check(falador != null && falador.Id == 364, "other elite scans are still offered");
                var clues = new ReferenceClueReader(assets);
                Check(clues.MatchText("R AK MI").Card.Id == 335, "R AK MI is Karim");
                Check(clues.MatchText("IMKROM").Card.Id == 340, "IM KROM is Rommik");
                Check(clues.MatchText("Beckon at Varrock Dig Site, near the eastern winch. Bow or curtsy before you talk to me. Have nothing equipped at all when you do.").Card.Id == 7, "eastern winch beckon");
                Check(clues.MatchText("Cheer in the Ogre pen in the Training Camp. Show you are angry before you talk to me. Equip a Green Dragonhide Body, green dragonhide chaps and a steel sq shield.").Card.Id == 256, "ogre pen cheer");
                Check(clues.MatchText("Brush off the sand and dig in the quarry. There is a wheely handy barrow to the east. Don't worry, permission has been granite to dig there.").Card.Id == 217, "granite quarry");
                string khazard = clues.Clues.Single(c => c.Card.Id == 96).Card.Travel;
                Check(khazard.Contains("Lunar spellbook > Khazard Teleport") && khazard.Contains("Alternatively: Yanille lodestone.") && khazard.Contains("Ardougne Monastery") && khazard.Contains("Run east to Port Khazard and search the crate near the cart."), "Port Khazard card");
                File.AppendAllText(report, "Map icon checks passed: " + assertions + "\n");
                return 0;
            }
            catch (Exception ex)
            {
                File.AppendAllText(report, ex.Message + "\n");
                return 1;
            }
        }
    }
}
