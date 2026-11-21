using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace KavrosClueHelper
{
    public static class RobustnessRegressionTests
    {
        private static int checks;
        private static void Check(bool pass, string message) { checks++; if (!pass) throw new Exception("FAILED: " + message); }

        public static int Run(string report)
        {
            var log = new System.Text.StringBuilder();
            try
            {
                Check(IdleTiming.Interval(false, false, false) == IdleTiming.IdleMilliseconds, "no game and no overlay slows the tick");
                Check(IdleTiming.Interval(true, false, false) == IdleTiming.ActiveMilliseconds, "an attached game keeps the 50ms tick");
                Check(IdleTiming.Interval(false, true, false) == IdleTiming.ActiveMilliseconds, "a visible overlay keeps the 50ms tick");
                Check(IdleTiming.Interval(false, false, true) == IdleTiming.ActiveMilliseconds, "a running read keeps the 50ms tick");

                string crash = Path.Combine(Path.GetTempPath(), "kavros-crash-" + Guid.NewGuid().ToString("N") + ".log");
                CrashLog.Write(new InvalidOperationException("sample failure"), crash);
                CrashLog.Write(null, crash);
                string written = File.ReadAllText(crash);
                Check(written.Contains("sample failure") && written.Contains("InvalidOperationException"), "failures are written to the log");
                File.Delete(crash);

                var positions = CompassSession.ReadPositions(new[]
                {
                    "# comment",
                    "",
                    "Edgeville lodestone\t3067\t3505\t1",
                    "broken line",
                    "Too few\t12",
                    "Bad number\tx\t4\t1",
                    "Varrock lodestone\t3214\t3424\t1.5"
                });
                Check(positions.Count == 2 && positions[0].Name.IndexOf("Edgeville", StringComparison.OrdinalIgnoreCase) >= 0 && positions[0].X == 3067 && positions[0].Y == 3505 && positions[0].Radius == 1, "good lodestone rows survive a bad positions file");
                Check(Math.Abs(positions[1].Radius - 1.5) < 0.0001 && positions[1].X == 3214, "fractional radius still parses");

                var regions = new List<ScanRegion> { new ScanRegion { Id = 36, Name = "Deep Wilderness" }, new ScanRegion { Id = 371, Name = "Heart" } };
                ScanTables.ApplyRoutes(regions, new[]
                {
                    "",
                    "# skip",
                    "nope",
                    "371\tFastest: Beast tab.\nClue: Dig.",
                    "36\tFastest: one\tstill kept",
                    "zz\tignored"
                });
                Check(regions[1].Route == "Fastest: Beast tab.\nClue: Dig.", "a single-tab scan route is unchanged");
                Check(regions[0].Route == "Fastest: one\tstill kept", "an extra tab stays part of the route instead of dropping it");

                RectangleF box;
                Check(ScanTables.TryBox("2088\t3220\t2337\t3478", out box) && box.Left == 2088 && box.Top == 3220 && box.Right == 2337 && box.Bottom == 3478, "scan map box parses");
                Check(!ScanTables.TryBox("1\t2\t3", out box), "a short map box is rejected");
                var spots = ScanTables.ReadSpots(new[] { "2175\t3291\t1", "", "bad", "10\t20" });
                Check(spots.Count == 2 && spots[0].X == 2175 && spots[0].Y == 3291 && spots[1].X == 10, "spot rows skip blanks and keep the rest");

                Check(new DetectedPuzzle { Kind = PuzzleKind.Towers }.Key == "T:?", "a towers read with no clues does not throw");
                Check(new DetectedPuzzle { Kind = PuzzleKind.Scan }.Key == "S:?", "a scan read with no region does not throw");
                Check(new DetectedPuzzle { Kind = PuzzleKind.Compass }.Key == "C:?", "a compass read with no bearing does not throw");
                Check(new DetectedPuzzle { Kind = PuzzleKind.EmoteClue }.Key == "M:?", "a clue read with no card does not throw");
                var solved = new TowerPuzzle { Top = new[] { 1, 2, 3, 4, 5 }, Bottom = new[] { 5, 4, 3, 2, 1 }, Left = new[] { 1, 1, 1, 1, 1 }, Right = new[] { 5, 5, 5, 5, 5 } };
                Check(new DetectedPuzzle { Kind = PuzzleKind.Towers, Towers = solved }.Key == "T:" + solved.Key, "a complete tower key is unchanged");

                using (Bitmap image = new Bitmap(8, 6, PixelFormat.Format24bppRgb))
                {
                    BitmapData bits = image.LockBits(new Rectangle(0, 0, 8, 6), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        byte[] row = new byte[bits.Stride];
                        for (int x = 0; x < 8; x++) { row[x * 3] = 20; row[x * 3 + 1] = 30; row[x * 3 + 2] = 40; }
                        for (int y = 0; y < 6; y++) Marshal.Copy(row, 0, IntPtr.Add(bits.Scan0, y * bits.Stride), row.Length);
                        Marshal.WriteByte(bits.Scan0, 2, 200);
                        Marshal.WriteByte(bits.Scan0, 1, 10);
                        Marshal.WriteByte(bits.Scan0, 0, 20);
                    }
                    finally { image.UnlockBits(bits); }
                    Pixels pixels = new Pixels(image);
                    Color first = pixels.Get(0, 0);
                    Check(first.R == 200 && first.G == 10 && first.B == 20 && first.A == 255, "24-bit captures keep their colours without a second bitmap");
                    Color other = pixels.Get(3, 2);
                    Check(other.R == 40 && other.G == 30 && other.B == 20 && other.A == 255, "24-bit rows keep stride padding out of the pixels");
                }

                using (Bitmap image = new Bitmap(12, 9, PixelFormat.Format32bppArgb))
                {
                    image.SetPixel(4, 5, Color.FromArgb(255, 12, 80, 160));
                    image.SetPixel(1, 1, Color.FromArgb(0, 9, 9, 9));
                    Pixels pixels = new Pixels(image);
                    Color opaque = pixels.Get(4, 5);
                    Color clear = pixels.Get(1, 1);
                    Check(opaque.ToArgb() == image.GetPixel(4, 5).ToArgb(), "32-bit pixels match GetPixel");
                    Check(clear.A == image.GetPixel(1, 1).A && clear.R == image.GetPixel(1, 1).R, "32-bit alpha is preserved");
                }

                using (Bitmap image = new Bitmap(401, 401, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(image)) g.Clear(Color.Black);
                    BitmapData bits = image.LockBits(new Rectangle(0, 0, 401, 401), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        int stride = bits.Stride;
                        int offset = 17 * stride + 29 * 3;
                        Marshal.WriteByte(bits.Scan0, offset, 0);
                        Marshal.WriteByte(bits.Scan0, offset + 1, 0);
                        Marshal.WriteByte(bits.Scan0, offset + 2, 180);
                    }
                    finally { image.UnlockBits(bits); }
                    Pixels pixels = new Pixels(image);
                    List<Point> hits = pixels.AnchorCandidates(new Rectangle(0, 0, 401, 401), Point.Empty, Color.FromArgb(255, 180, 0, 0), 0).ToList();
                    Check(hits.Count == 1 && hits[0] == new Point(29, 17), "large-frame colour index finds the same pixel");
                }

                string digits = Path.Combine(Path.GetTempPath(), "kavros-digits-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(digits);
                string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                File.Copy(Path.Combine(assets, "digits.txt"), Path.Combine(digits, "digits.txt"));
                File.Copy(Path.Combine(assets, "legacy-digits.txt"), Path.Combine(digits, "legacy-digits.txt"));
                File.AppendAllText(Path.Combine(digits, "digits.txt"), Environment.NewLine + Environment.NewLine + "not a glyph" + Environment.NewLine + "1|nope" + Environment.NewLine);
                DigitReader reader = new DigitReader(digits);
                using (Bitmap blank = new Bitmap(40, 40))
                    Check(reader.Read(new Pixels(blank), new Point(20, 20), false) == 0, "a broken digit line does not discard the digit files");
                Directory.Delete(digits, true);

                Rectangle game = new Rectangle(0, 0, 1000, 800);
                Size toast = new Size(180, 34);
                Rectangle open = NoticePlacement.Place(game, toast, null);
                Check(open == new Rectangle(1000 - 180 - 8, 8, 180, 34), "an empty game keeps the notice in the top-right corner");
                Rectangle card = new Rectangle(800, 0, 200, 120);
                Rectangle puzzle = new Rectangle(100, 100, 400, 400);
                var blocked = new List<Rectangle> { card, puzzle };
                Rectangle placed = NoticePlacement.Place(game, toast, blocked);
                Check(game.Contains(placed), "the notice stays inside the game window");
                Check(!Rectangle.Inflate(placed, 4, 4).IntersectsWith(card), "the notice stays off the clue card");
                Check(!Rectangle.Inflate(placed, 4, 4).IntersectsWith(puzzle), "the notice stays off the puzzle being read");
                Rectangle forced = new Rectangle(0, 0, 1000, 800);
                Rectangle outside = NoticePlacement.Place(game, toast, new List<Rectangle> { forced });
                Check(!outside.IntersectsWith(game), "a full window moves the notice outside the game");

                log.AppendLine("Robustness checks passed: " + checks);
                File.AppendAllText(report, log.ToString());
                return 0;
            }
            catch (Exception ex)
            {
                File.AppendAllText(report, "FAIL after " + checks + ": " + ex + Environment.NewLine);
                return 1;
            }
        }
    }
}
