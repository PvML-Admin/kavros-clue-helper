using System;
using System.Drawing;

namespace KavrosClueHelper
{
    // Shared rule for every live reading: keep the last good result through
    // brief misreads, and only treat the thing as gone after several misses
    // in a row that also span a minimum time (about 0.5 to 1 second).
    public sealed class DetectionHold
    {
        public const int MissFrames = 4;
        public static readonly TimeSpan MissTime = TimeSpan.FromMilliseconds(700);
        private int misses;
        private DateTime firstMiss;
        public int Misses { get { return misses; } }
        public void Seen() { misses = 0; }
        public void Reset() { misses = 0; }
        // Records one missed frame. Returns true while the last good result
        // should stay on screen, false once it counts as closed.
        public bool Miss(DateTime now)
        {
            if (misses == 0) firstMiss = now;
            misses++;
            return !Expired(now);
        }
        public bool Expired(DateTime now)
        {
            return misses >= MissFrames && now - firstMiss >= MissTime;
        }
    }

    // A different reading must be seen twice in a row before it replaces the
    // one on screen. One misread frame can then never swap the guidance.
    public sealed class ChangeConfirmation
    {
        private string candidate;
        public string Candidate { get { return candidate; } }
        public void Reset() { candidate = null; }
        public bool Accept(string shown, string next)
        {
            if (shown == null || next == shown) { candidate = null; return true; }
            if (next == candidate) { candidate = null; return true; }
            candidate = next;
            return false;
        }
    }

    public static class SteadyPolicy
    {
        // Board puzzles whose guidance is held through brief misses: the
        // lockbox and the towers. Slider and knot keep their own rules.
        public static bool HoldsThroughMisses(PuzzleKind kind)
        {
            return kind == PuzzleKind.Lockbox || kind == PuzzleKind.Towers;
        }
        // Same board and nearly the same place: nothing to redraw.
        public static bool SameState(DetectedPuzzle shown, DetectedPuzzle next, int tolerance)
        {
            if (shown == null || next == null || shown.Kind != next.Kind || shown.Key != next.Key) return false;
            return Near(shown.Bounds, next.Bounds, tolerance) && Near(shown.Grid, next.Grid, tolerance);
        }
        public static bool Near(Rectangle a, Rectangle b, int tolerance)
        {
            return Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance
                && Math.Abs(a.Width - b.Width) <= tolerance && Math.Abs(a.Height - b.Height) <= tolerance;
        }
    }

    // Estimates the player's map position from recent movement while the
    // minimap cannot be matched. Positions are map tiles; time in UTC.
    public sealed class MotionEstimate
    {
        // RS3 running is about 3.3 tiles per second; anything faster is a
        // teleport or a misread, not movement to extrapolate.
        public const double MaxSpeed = 4.0;
        public static readonly TimeSpan MaxExtrapolation = TimeSpan.FromSeconds(1.2);
        private PointF? last;
        private DateTime lastTime;
        private double vx, vy;
        public PointF Velocity { get { return new PointF((float)vx, (float)vy); } }
        public void Reset() { last = null; vx = vy = 0; }
        public void Observe(PointF position, DateTime now)
        {
            if (last.HasValue)
            {
                double dt = (now - lastTime).TotalSeconds;
                if (dt > 0.02 && dt < 3)
                {
                    double nx = (position.X - last.Value.X) / dt, ny = (position.Y - last.Value.Y) / dt;
                    double speed = Math.Sqrt(nx * nx + ny * ny);
                    if (speed > MaxSpeed) { nx *= MaxSpeed / speed; ny *= MaxSpeed / speed; }
                    // Smooth so one jittery match cannot fling the estimate.
                    vx = vx * 0.5 + nx * 0.5; vy = vy * 0.5 + ny * 0.5;
                }
                else if (dt >= 3) { vx = vy = 0; }
            }
            last = position; lastTime = now;
        }
        public PointF? Estimate(DateTime now)
        {
            if (!last.HasValue) return null;
            double dt = (now - lastTime).TotalSeconds;
            if (dt <= 0) return last;
            dt = Math.Min(dt, MaxExtrapolation.TotalSeconds);
            return new PointF((float)(last.Value.X + vx * dt), (float)(last.Value.Y + vy * dt));
        }
    }
}
