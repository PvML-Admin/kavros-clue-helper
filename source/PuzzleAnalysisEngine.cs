using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace KavrosClueHelper
{
    public static class SliderBudget
    {
        // Work budget for the immediate slider route. Deterministic work units,
        // so every PC gets the same route for the same board.
        public const int Milliseconds = 5000;
    }
    public static class SearchCancellation
    {
        [ThreadStatic] public static CancellationToken Token;
        public static void Check() { Token.ThrowIfCancellationRequested(); }
    }
    public sealed class PuzzlePlan
    {
        public DetectedPuzzle Puzzle;
        public Solution Lockbox;
        public List<int[]> Towers;
        public List<int> Sliding;
        public bool SlidingOptimal;
        public bool SlidingWholeRows;
        // Proven minimum for the board the route was solved from (0 = unknown).
        public int SlidingLowerBound;
        public KnotSolution Knot;
        public bool KnotMoving;
        public PuzzlePlan WithPuzzle(DetectedPuzzle puzzle)
        {
            var copy = (PuzzlePlan)MemberwiseClone();
            copy.Puzzle = puzzle;
            return copy;
        }
    }
    public sealed class AnalysisResult : IDisposable
    {
        public PuzzlePlan Plan;
        public Bitmap Preview;
        public Bitmap DiagnosticImage;
        public long Milliseconds;
        public bool Unchanged,Pending;
        public void Dispose() { if (Preview != null) { Preview.Dispose(); Preview = null; } if(DiagnosticImage!=null) { DiagnosticImage.Dispose(); DiagnosticImage=null; } }
    }
    public sealed class PuzzleAnalysisEngine
    {
        public readonly SliderProofSession Proofs = new SliderProofSession();
        private readonly PuzzleDetector detector;
        private DetectedPuzzle last;
        private PuzzlePlan cached;
        private string solveKey;
        private readonly KnotFrameTracker knotFrames=new KnotFrameTracker();
        private ulong lastHash;
        private Color[] sliderFrame;
        private string pendingSliderKey;
        private Size lastSize;
        private Point? previousOrigin;
        private DateTime knotConfirmed;
        private DateTime boardSeen;
        private int sourceId = Int32.MinValue;
        private DateTime nextBroadSearch = DateTime.MinValue;
        public int FullSearches { get; private set; }
        public int SlidingRouteReuses {get;private set;}
        public int BroadSearches { get { return detector.BroadSearches; } }
        public int UnchangedFrames { get; private set; }
        public Action<string> Progress;
        public PuzzleAnalysisEngine(string assets) { detector = new PuzzleDetector(assets);detector.Progress=message=>{if(Progress!=null)Progress(message);}; }
        public DetectedPuzzle ReadOnlySlider(Bitmap image,DetectedPuzzle previous){return detector.ReadAgain(image,previous);}
        private Color[] SliderFrame(Pixels pixels,DetectedPuzzle puzzle)
        {return Enumerable.Range(0,16).Select(i=>{
            int side=i/4;double along=(i%4+1)/5.0;int x=side<2?(side==0?puzzle.Grid.Left-(int)Math.Round(10*puzzle.Scale):puzzle.Grid.Right+(int)Math.Round(10*puzzle.Scale)):puzzle.Grid.Left+(int)Math.Round(puzzle.Grid.Width*along);
            int y=side>=2?(side==2?puzzle.Grid.Top-(int)Math.Round(10*puzzle.Scale):puzzle.Grid.Bottom+(int)Math.Round(10*puzzle.Scale)):puzzle.Grid.Top+(int)Math.Round(puzzle.Grid.Height*along);return pixels.Get(x,y);}).ToArray();}
        private bool VisibleSlider(Bitmap image,Pixels pixels)
        {
            if(last==null||cached==null||last.Kind!=PuzzleKind.Sliding||sliderFrame==null||!detector.SliderFrameVisible(image,last))return false;
            Color[] fresh=SliderFrame(pixels,last);return fresh.Where((c,i)=>Math.Abs(c.R-sliderFrame[i].R)+Math.Abs(c.G-sliderFrame[i].G)+Math.Abs(c.B-sliderFrame[i].B)<60).Count()>=13;
        }
        private AnalysisResult PendingSlider(long elapsed)
        {return new AnalysisResult{Plan=cached,Pending=true,Unchanged=true,Milliseconds=elapsed};}
        public AnalysisResult Analyze(Bitmap image, int source, bool reverse, CancellationToken cancellation, bool livePolling = false,bool wholeRows=false, Point? captureOrigin=null)
        {
            Stopwatch timer = Stopwatch.StartNew();
            SearchCancellation.Token = cancellation;
            Bitmap decoded=null;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                // Windows GDI+ can repeatedly decode a file-backed PNG during
                // small crop/resample operations. Decode it once for this pass.
                // Live screen captures are already memory bitmaps.
                if(image.RawFormat.Guid!=System.Drawing.Imaging.ImageFormat.MemoryBmp.Guid)
                {
                    decoded=new Bitmap(image.Width,image.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using(Graphics g=Graphics.FromImage(decoded))g.DrawImage(image,new Rectangle(Point.Empty,image.Size),new Rectangle(Point.Empty,image.Size),GraphicsUnit.Pixel);
                    image=decoded;
                }
                bool geometryChanged = lastSize != image.Size || previousOrigin != captureOrigin;
                if (source == sourceId && geometryChanged && last != null && previousOrigin.HasValue && captureOrigin.HasValue)
                {
                    Point old = previousOrigin.Value, next = captureOrigin.Value;
                    var translated = last.OffsetBy(new Point(old.X - next.X, old.Y - next.Y));
                    if (new Rectangle(Point.Empty, image.Size).Contains(translated.Bounds))
                    {
                        last = translated;
                        if (cached != null) cached = cached.WithPuzzle(translated);
                        geometryChanged = false;
                    }
                }
                previousOrigin = captureOrigin;
                if (source != sourceId || geometryChanged)
                { // Reframing the same game must retain the slider route; otherwise
                  // the worker and visible preview can follow different plans.
                  bool keepSlider=source==sourceId&&cached!=null&&(cached.Puzzle.Kind==PuzzleKind.Sliding||cached.Puzzle.Kind==PuzzleKind.Knot);
                  last=null;if(!keepSlider){cached=null;solveKey=null;}sliderFrame=null;pendingSliderKey=null; if(source!=sourceId)knotFrames.Reset();else knotFrames.Missing(DateTime.UtcNow); sourceId = source; lastSize = image.Size; nextBroadSearch=DateTime.MinValue; detector.ResetSearch(); }
                lastSize = image.Size;
                Pixels pixels = new Pixels(image);
                bool sameSolve = last != null && DetectionPolicy.CanTrack(cached) && solveKey == cached.Puzzle.Key + ":" + reverse+(cached.Puzzle.Kind==PuzzleKind.Knot?":"+cached.KnotMoving:"")+":"+wholeRows && !cached.KnotMoving && new Rectangle(Point.Empty,image.Size).Contains(last.Bounds);
                bool hashUnchanged = sameSolve && pixels.Hash(last.Bounds) == lastHash;
                bool trackingBoard = last != null && DetectionPolicy.IsBoardPuzzle(last.Kind);
                // A stable board frame can be reused. A tracked text clue must
                // still run the cheap board search, because a puzzle can open
                // somewhere else on the same screen.
                if (trackingBoard && hashUnchanged)
                { boardSeen=DateTime.UtcNow; UnchangedFrames++; return new AnalysisResult { Plan = cached, Unchanged = true, Milliseconds = timer.ElapsedMilliseconds }; }
                DetectedPuzzle puzzle = trackingBoard ? detector.ReadAgain(image,last,pixels) : null;
                if (puzzle == null)
                {
                    bool broad=!livePolling || DateTime.UtcNow>=nextBroadSearch;
                    DetectedPuzzle board=detector.DetectBoards(image,pixels,broad,livePolling);
                    if(board!=null)
                    {
                        if(last==null||trackingBoard)FullSearches++;
                        puzzle=board;
                    }
                    else
                    {
                        if(livePolling && broad) nextBroadSearch=DateTime.UtcNow.AddMilliseconds(100);
                        if(hashUnchanged)
                        { UnchangedFrames++; return new AnalysisResult { Plan = cached, Unchanged = true, Milliseconds = timer.ElapsedMilliseconds }; }
                        if(last!=null && !trackingBoard) puzzle=detector.ReadAgain(image,last,pixels);
                        if(puzzle==null)
                        {
                            FullSearches++;
                            puzzle=detector.DetectClues(image,pixels);
                        }
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if (puzzle == null)
                {
                    if(livePolling&&VisibleSlider(image,pixels))return PendingSlider(timer.ElapsedMilliseconds);
                    if(livePolling&&cached!=null&&last!=null&&last.Kind==PuzzleKind.Knot&&(DateTime.UtcNow-knotConfirmed).TotalMilliseconds<800&&detector.KnotFrameVisible(image,last))
                        return new AnalysisResult{Plan=cached,Pending=true,Unchanged=true,Milliseconds=timer.ElapsedMilliseconds};
                    if(livePolling&&cached!=null&&last!=null&&(last.Kind==PuzzleKind.Towers||last.Kind==PuzzleKind.Lockbox||last.Kind==PuzzleKind.Sliding)&&(DateTime.UtcNow-boardSeen).TotalMilliseconds<1500)
                        return new AnalysisResult{Plan=cached,Pending=true,Unchanged=true,Milliseconds=timer.ElapsedMilliseconds};
                    last = null; cached = null; solveKey = null; knotFrames.Missing(DateTime.UtcNow);
                    return new AnalysisResult { Preview = MakePreview(image,new Rectangle(Point.Empty,image.Size)), Milliseconds = timer.ElapsedMilliseconds };
                }
                if (cached != null && cached.Puzzle.Kind == PuzzleKind.Sliding && !cached.SlidingOptimal)
                {
                    List<int> proven;
                    string error;
                    if (Proofs.Read(cached.Puzzle.Board, cached.Puzzle.Theme, cached.SlidingWholeRows, out proven, out error))
                    {
                        cached = new PuzzlePlan { Puzzle = cached.Puzzle, Sliding = proven, SlidingWholeRows = cached.SlidingWholeRows, SlidingOptimal = true };
                    }
                }
                if(livePolling&&cached!=null&&cached.Puzzle.Kind==PuzzleKind.Sliding&&puzzle.Kind==PuzzleKind.Sliding&&puzzle.Theme==cached.Puzzle.Theme)
                {
                    List<int> route;
                    if(!SlidingRoute.Advance(cached.Puzzle.Board,cached.Sliding,puzzle.Board,out route)&&VisibleSlider(image,pixels))
                    {if(pendingSliderKey!=puzzle.Key){pendingSliderKey=puzzle.Key;return PendingSlider(timer.ElapsedMilliseconds);}}
                    else pendingSliderKey=null;
                }
                bool knotMoving=false;
                if(puzzle.Kind==PuzzleKind.Knot)knotMoving=knotFrames.Observe(puzzle,DateTime.UtcNow,livePolling);
                else knotFrames.Reset();
                string key=puzzle.Key+":"+reverse+(puzzle.Kind==PuzzleKind.Knot?":"+knotMoving:"")+":"+wholeRows;
                PuzzlePlan plan;
                if (cached != null && key == solveKey)
                    plan = new PuzzlePlan { Puzzle = puzzle, Lockbox = cached.Lockbox, Towers = cached.Towers, Sliding = cached.Sliding, SlidingOptimal=cached.SlidingOptimal,SlidingWholeRows=cached.SlidingWholeRows,SlidingLowerBound=cached.SlidingLowerBound,Knot = cached.Knot,KnotMoving=knotMoving };
                else
                {
                    if(Progress!=null)Progress(puzzle.Kind==PuzzleKind.EmoteClue||puzzle.Kind==PuzzleKind.Compass?"Reading clue...":"Solving puzzle...");
                    plan = new PuzzlePlan { Puzzle = puzzle,KnotMoving=knotMoving };
                    if (puzzle.Kind == PuzzleKind.Lockbox) plan.Lockbox = LockboxSolver.Solve(puzzle.Board,reverse?-1:1);
                    if (puzzle.Kind == PuzzleKind.Towers) plan.Towers = puzzle.TowerSolutions;
                    if(puzzle.Kind==PuzzleKind.Sliding&&SlidingSolver.IsSolvable(puzzle.Board))
                    {
                        List<int> remaining;
                        plan.SlidingWholeRows=wholeRows;
                        if(cached!=null&&cached.Puzzle.Kind==PuzzleKind.Sliding&&cached.SlidingWholeRows==wholeRows&&cached.Puzzle.Theme==puzzle.Theme&&SlidingRoute.Advance(cached.Puzzle.Board,cached.Sliding,puzzle.Board,out remaining))
                        {plan.Sliding=remaining;int consumed=cached.Sliding.Count-remaining.Count;plan.SlidingOptimal=cached.SlidingOptimal&&(!wholeRows||SlidingRoute.CompletedClicks(cached.Puzzle.Board,cached.Sliding,consumed)==SlidingRoute.ClickRoute(cached.Puzzle.Board,cached.Sliding.Take(consumed).ToList()).Count);SlidingRouteReuses++;}
                        else {if(Progress!=null)Progress("Optimising slider route...");SlidingOptimization route=wholeRows?SlidingClickOptimizer.Solve(puzzle.Board):SlidingOptimizer.Solve(puzzle.Board,SliderBudget.Milliseconds);plan.Sliding=route.Moves;plan.SlidingOptimal=route.Optimal;plan.SlidingLowerBound=route.LowerBound;}
                    }
                    if(puzzle.Kind==PuzzleKind.Knot&&!knotMoving)plan.Knot=KnotSolver.Solve(puzzle.Knot);
                }
                cancellation.ThrowIfCancellationRequested();
                if (!DetectionPolicy.CanTrack(plan))
                {
                    if(livePolling&&VisibleSlider(image,pixels))return PendingSlider(timer.ElapsedMilliseconds);
                    last = null; cached = null; solveKey = null; knotFrames.Missing(DateTime.UtcNow);
                    return new AnalysisResult { Preview = MakePreview(image,new Rectangle(Point.Empty,image.Size)), Milliseconds = timer.ElapsedMilliseconds };
                }
                last = puzzle; cached = plan; solveKey = key;if(puzzle.Kind==PuzzleKind.Sliding)sliderFrame=SliderFrame(pixels,puzzle);else{sliderFrame=null;pendingSliderKey=null;} lastHash = pixels.Hash(puzzle.Bounds);
                if(puzzle.Kind==PuzzleKind.Knot&&!knotMoving)knotConfirmed=DateTime.UtcNow;
                if(DetectionPolicy.IsBoardPuzzle(puzzle.Kind))boardSeen=DateTime.UtcNow;
                return new AnalysisResult { Plan = plan, Preview = MakePreview(image,puzzle.Bounds), Milliseconds = timer.ElapsedMilliseconds };
            }
            finally { if(decoded!=null)decoded.Dispose();SearchCancellation.Token = CancellationToken.None; }
        }
        private static Bitmap MakePreview(Bitmap image, Rectangle area)
        {
            double ratio = Math.Min(1,Math.Min(920.0/area.Width,444.0/area.Height));
            return Pixels.Crop(image,area,new Size(Math.Max(1,(int)(area.Width*ratio)),Math.Max(1,(int)(area.Height*ratio))));
        }
    }
}
