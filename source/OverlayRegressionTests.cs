using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace KavrosClueHelper
{
    public static class OverlayRegressionTests
    {
        private static int assertions;
        private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception("FAILED: " + message); }
        public static int Run(string report)
        {
            try
            {
                string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                PuzzleDetector detector = new PuzzleDetector(assets);
                foreach (float scale in new[] { 0.8F, 1F, 1.25F, 1.5F, 2F })
                foreach (bool filled in new[] { false, true })
                using (Bitmap original = TestFixtures.TowerModal("rev944", false, filled))
                using (Bitmap image = Pixels.Crop(original, new Rectangle(Point.Empty, original.Size), new Size((int)Math.Round(original.Width * scale), (int)Math.Round(original.Height * scale))))
                {
                    Rectangle grid = new Rectangle((int)Math.Round(121 * scale), (int)Math.Round(113 * scale), (int)Math.Round(218 * scale), (int)Math.Round(218 * scale));
                    DetectedPuzzle puzzle = detector.ReadTowers(image, grid, scale, true);
                    Check(puzzle != null, "baseline tower read at " + scale);
                    int[] targets = TowerSolver.Solve(TestFixtures.TowersExample(), false)[0];
                    List<GuidanceMark> marks = GuidanceDrawing.TowerMarks(puzzle, targets);
                    Check(marks.Count == 25, "all 25 labels remain, including entered cells");
                    Check(marks.Select(m => m.Text).SequenceEqual(targets.Select(v => v.ToString())), "labels contain solved heights");
                    Check(marks.All(m => m.Compact && m.Corner && !m.Outline && m.Colour == Color.White), "small white corner labels without outlines");
                    using (Bitmap before = (Bitmap)image.Clone())
                    {
                        using (Graphics graphics = Graphics.FromImage(image)) GuidanceDrawing.Draw(graphics, marks);
                        foreach (GuidanceMark mark in marks)
                        {
                            Rectangle label = GuidanceDrawing.LabelBounds(mark);
                            Check(mark.Box.Contains(label), "badge fits inside cell");
                            Rectangle centre = new Rectangle(mark.Box.X + (int)(12 * scale), mark.Box.Y + (int)(12 * scale), (int)(17 * scale), (int)(20 * scale));
                            Check(!centre.IntersectsWith(label), "badge clears the game's centre digit");
                            for (int y = centre.Top; y < centre.Bottom; y++)
                                for (int x = centre.Left; x < centre.Right; x++)
                                    if (before.GetPixel(x, y) != image.GetPixel(x, y)) throw new Exception("Corner painting changed a centred game digit.");
                            int light = 0;
                            for (int y = label.Top; y < label.Bottom; y++)
                                for (int x = label.Left; x < label.Right; x++)
                                    if (image.GetPixel(x, y).R > 100) light++;
                            Check(light > 0, "corner digit is painted and not clipped");
                        }
                    }
                    DetectedPuzzle reread = detector.ReadTowers(image, grid, scale, true);
                    Check(reread != null && reread.Towers.Key == puzzle.Towers.Key, "badges do not alter captured clues at " + scale);
                    Check(reread.Towers.Cells.SequenceEqual(puzzle.Towers.Cells), "badges do not become entered game heights at " + scale);
                    if (scale == 1 && filled) image.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)), "tower-corner-preview.png"));
                    Check(GuidanceDrawing.SameMarks(marks, marks.Select(m => m.Offset(0, 0)).ToList()), "unchanged marks do not request a redraw");
                    List<GuidanceMark> changed = marks.Select(m => m.Offset(0, 0)).ToList();
                    changed[0].Text = "?";
                    Check(!GuidanceDrawing.SameMarks(marks, changed), "changed digits request a redraw");
                    changed = marks.Select(m => m.Offset(1, 0)).ToList();
                    Check(!GuidanceDrawing.SameMarks(marks, changed), "changed cell positions request a redraw");
                }
                Check(!GuidanceDrawing.HideForCapture(false, PuzzleKind.Towers), "tower labels stay visible even without capture exclusion");
                foreach (PuzzleKind kind in new[] { PuzzleKind.Towers, PuzzleKind.Knot, PuzzleKind.Lockbox, PuzzleKind.Sliding })
                    Check(!GuidanceDrawing.HideForCapture(true, kind), "excluded overlays remain visible during scans");
                Check(GuidanceDrawing.HideForCapture(false, PuzzleKind.Lockbox), "unsupported capture exclusion retains safe lockbox reading");
                File.AppendAllText(report, "V0.2.3 OVERLAY REGRESSIONS PASS: " + assertions + " assertions.\r\nCorner labels checked on bitmaps at 80%, 100%, 125%, 150% and 200%.\r\nNo windows were shown and no application input was sent.\r\n");
                return 0;
            }
            catch (Exception ex) { File.AppendAllText(report, ex + "\r\n"); return 1; }
        }
    }
}
