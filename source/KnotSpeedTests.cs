using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace KavrosClueHelper
{
    public static class KnotSpeedTests
    {
        public static void Verify(Action<bool, string> check, string assets, Bitmap settled, Rectangle body)
        {
            var engine = new PuzzleAnalysisEngine(assets);
            using (AnalysisResult first = engine.Analyze(settled, 6000, false, CancellationToken.None, true))
            {
                string scores = first.Plan == null || first.Plan.Puzzle.Knot == null ? "none" : first.Plan.Puzzle.Knot.MinRune.ToString("0.00") + "/" + first.Plan.Puzzle.Knot.MinTrack.ToString("0.00") + "/" + first.Plan.Puzzle.Knot.MinMargin.ToString("0.00");
                check(first.Plan != null && !first.Plan.KnotMoving && first.Plan.Knot != null, "Knot shows counts on the first still frame " + scores);
                check(first.Plan != null && !DetectionPolicy.NeedsConfirmation(first.Plan), "a solved still knot does not wait for another frame");
            }
            var knotReader = new KnotImageReader(assets);
            using (Bitmap crop = Pixels.Crop(settled, body, body.Size))
            {
                KnotPuzzle read = knotReader.Read(crop);
                int misses = knotReader.CacheMisses;
                string key = KnotFrameTracker.RuneKey(read);
                KnotPuzzle again = knotReader.Read(crop);
                check(read != null && read.Reliable && KnotFrameTracker.RuneKey(again) == key, "cached knot runes match the first read " + (read == null ? knotReader.LastError : read.MinRune.ToString("0.00")));
                check(knotReader.CacheHits >= 20 && knotReader.CacheMisses == misses, "unchanged knot tiles are not read again");
                using (Graphics paint = Graphics.FromImage(crop)) paint.FillRectangle(Brushes.Black, 90, 40, 12, 12);
                int hits = knotReader.CacheHits;
                knotReader.Read(crop);
                check(knotReader.CacheHits > hits && knotReader.CacheMisses > misses && knotReader.CacheMisses - misses < knotReader.CacheHits - hits, "a changed tile is re-read and the other tiles stay cached");
            }
        }
        public static int Run(string report)
        {
            int count = 0;
            try
            {
                Action<bool, string> check = (ok, msg) => { count++; if (!ok) throw new Exception(msg); };
                string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                var tracker = new KnotFrameTracker();
                DateTime now = DateTime.UtcNow;
                KnotPuzzle source = TestFixtures.KnotExample();
                Func<int, DetectedPuzzle> rotated = offset => new DetectedPuzzle { Kind = PuzzleKind.Knot, Knot = new KnotPuzzle { Colours = source.Colours, Locks = source.Locks, Buttons = source.Buttons, Lanes = source.Lanes.Select((lane, i) => lane.Select((rune, j) => lane[(j + offset + i) % lane.Length]).ToArray()).ToArray() } };
                check(tracker.Observe(rotated(0), now, true), "an uncertain first knot frame waits");
                check(!tracker.Observe(rotated(0), now.AddMilliseconds(125), true), "the repeated frame shows counts");
                var sureTracker = new KnotFrameTracker();
                DetectedPuzzle sure = rotated(0);
                sure.Knot.Reliable = true;
                check(!sureTracker.Observe(sure, now, true), "a reliable still knot shows counts on the first frame");
                DetectedPuzzle flicker = rotated(0);
                flicker.Knot.Lanes[0][0] = new RuneValue(flicker.Knot.Lanes[0][0].Id, !flicker.Knot.Lanes[0][0].IsNot);
                flicker.Knot.Reliable = true;
                check(!sureTracker.Observe(flicker, now.AddMilliseconds(40), true), "a match-marker flicker does not hide the counts");
                DetectedPuzzle turned = rotated(1);
                turned.Knot.Reliable = true;
                check(sureTracker.Observe(turned, now.AddMilliseconds(80), true), "a confident turn still waits until the ring stops");
                check(!sureTracker.Observe(turned, now.AddMilliseconds(120), true), "the same stopped ring then shows its counts");
                var detector = new PuzzleDetector(assets);
                using (Bitmap image = TestFixtures.KnotModal())
                {
                    DetectedPuzzle clean = detector.Detect(image);
                    check(clean != null && clean.Kind == PuzzleKind.Knot, "Knot fixture");
                    using (Bitmap settled = TestFixtures.KnotModal()) Verify(check, assets, settled, clean.Bounds);
                }
                File.WriteAllText(report, "KNOT SPEED PASS: " + count + " assertions.\r\n");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(report, "FAIL after " + count + ": " + ex + "\r\n");
                return 1;
            }
        }
    }
}
