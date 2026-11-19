using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
namespace KavrosClueHelper
{
    // Pure logic checks for the shared "keep the last good result" tracking.
    // They need no game window, so they run anywhere the program runs.
    public static class SteadyTrackingTests
    {
        public static int Run(string report)
        {
            var log=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,msg)=>{count++;if(!ok)throw new Exception(msg);log.AppendLine("ok "+msg);};
            try
            {
                DateTime t=new DateTime(2026,1,1,12,0,0,DateTimeKind.Utc);
                var hold=new DetectionHold();
                check(hold.Miss(t),"one missed frame keeps the result");
                check(hold.Miss(t.AddMilliseconds(50))&&hold.Miss(t.AddMilliseconds(100))&&hold.Miss(t.AddMilliseconds(150)),"four quick misses are not enough time");
                check(!hold.Miss(t.AddMilliseconds(750)),"several misses over 0.7 s close it");
                hold.Reset();
                for(int i=0;i<3;i++)hold.Miss(t.AddMilliseconds(i*400));
                check(hold.Miss(t.AddMilliseconds(1150))==false,"slow misses close after four frames");
                hold.Reset();hold.Miss(t);hold.Miss(t.AddMilliseconds(400));hold.Seen();
                check(hold.Miss(t.AddMilliseconds(900))&&hold.Misses==1,"a good frame restarts the miss count");

                var change=new ChangeConfirmation();
                check(change.Accept(null,"A"),"first reading is shown at once");
                check(change.Accept("A","A"),"same reading is kept");
                check(!change.Accept("A","B"),"one different frame does not replace the board");
                check(change.Accept("A","B"),"the same new board twice in a row replaces it");
                check(!change.Accept("A","B")&&!change.Accept("A","C")&&change.Accept("A","C"),"alternating misreads never replace it");
                check(!change.Accept("A","B")&&change.Accept("A","A")&&!change.Accept("A","B"),"returning to the shown board clears the candidate");

                var a=new DetectedPuzzle{Kind=PuzzleKind.Lockbox,Board=new int[25],Bounds=new Rectangle(100,100,300,300),Grid=new Rectangle(120,120,250,250)};
                var b=new DetectedPuzzle{Kind=PuzzleKind.Lockbox,Board=new int[25],Bounds=new Rectangle(101,99,300,301),Grid=new Rectangle(121,120,250,250)};
                check(SteadyPolicy.SameState(a,b,2),"a 1 px jitter on the same board needs no redraw");
                b.Board[3]=1;check(!SteadyPolicy.SameState(a,b,2),"a clicked board is redrawn");
                b.Board[3]=0;b.Bounds=new Rectangle(140,100,300,300);check(!SteadyPolicy.SameState(a,b,2),"a moved board is redrawn");
                check(SteadyPolicy.HoldsThroughMisses(PuzzleKind.Lockbox)&&SteadyPolicy.HoldsThroughMisses(PuzzleKind.Towers)&&!SteadyPolicy.HoldsThroughMisses(PuzzleKind.Sliding)&&!SteadyPolicy.HoldsThroughMisses(PuzzleKind.Knot),"only lockbox and towers use the hold");

                var motion=new MotionEstimate();
                check(!motion.Estimate(t).HasValue,"no estimate before any position");
                motion.Observe(new PointF(100,100),t);motion.Observe(new PointF(101,100),t.AddMilliseconds(250));motion.Observe(new PointF(102,100),t.AddMilliseconds(500));
                PointF e=motion.Estimate(t.AddMilliseconds(750)).Value;
                check(e.X>102.4&&e.X<103.2&&Math.Abs(e.Y-100)<0.01,"estimate follows recent movement ("+e+")");
                PointF far=motion.Estimate(t.AddSeconds(30)).Value;
                check(far.X<102+MotionEstimate.MaxSpeed*1.3,"estimate stops after a short time ("+far+")");
                motion.Reset();motion.Observe(new PointF(0,0),t);motion.Observe(new PointF(50,0),t.AddMilliseconds(250));
                check(motion.Velocity.X<=MotionEstimate.MaxSpeed+0.01,"a teleport is not treated as running speed");

                var region=new ScanRegion{Id=1,Name="Test",BaseRange=20,Level=0,Spots=new List<PointF>{new PointF(140,100)},SpotLevels=new List<int>{0}};
                var scan=new ScanSession();scan.Observe(new ScanReading{Region=region});
                check(scan.TrackingStatus==ScanSession.FindingHint,"automatic tracking asks for no manual step");
                scan.ApplyMatch(new MinimapMatch{Position=new PointF(100,100)},t);
                check(!scan.Position.HasValue,"first acquisition still waits for a second frame");
                for(int i=1;i<=4;i++)scan.ApplyMatch(new MinimapMatch{Position=new PointF(100+i,100)},t.AddMilliseconds(250*i));
                check(scan.PositionTracked,"consistent frames follow the player");
                PointF last=scan.LastKnown.Value;
                scan.ApplyMatch(null,t.AddMilliseconds(1250));
                check(scan.PositionTracked&&scan.Position.Value.X>last.X,"a missed frame moves the marker along the heading");
                for(int i=2;i<=7;i++)scan.ApplyMatch(null,t.AddMilliseconds(1000+250*i));
                check(scan.PositionTracked&&scan.Position.HasValue,"seven misses are not yet lost");
                scan.ApplyMatch(null,t.AddMilliseconds(3200));
                check(!scan.PositionTracked&&!scan.Position.HasValue&&scan.LastKnown==last,"eight misses over two seconds count as lost; the last position stays");
                check(scan.TrackingStatus==ScanSession.LostHint,"lost status reacquires automatically");
                scan.ApplyMatch(new MinimapMatch{Position=new PointF(last.X+3,last.Y)},t.AddMilliseconds(3500));
                check(scan.PositionTracked&&scan.Position.HasValue,"a match near the last position picks up again at once");
                scan.ApplyMatch(new MinimapMatch{Position=new PointF(last.X+300,last.Y)},t.AddMilliseconds(3750));
                check(scan.PositionTracked&&CompassMath.Distance(scan.Position.Value,new PointF(last.X+3,last.Y))<10,"a far misread does not move the marker");
                scan.Observe(new ScanReading{Region=region});
                check(scan.PositionTracked,"re-reading the same scan clue keeps tracking");
                File.WriteAllText(report,log.ToString()+"Steady tracking checks passed: "+count+Environment.NewLine);
                return 0;
            }
            catch(Exception ex)
            {
                File.WriteAllText(report,log.ToString()+"FAILED after "+count+" checks: "+ex.Message+Environment.NewLine);
                return 1;
            }
        }
    }
}
