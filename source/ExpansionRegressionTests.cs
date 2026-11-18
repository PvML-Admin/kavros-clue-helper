using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace KavrosClueHelper
{
    public static class ExpansionRegressionTests
    {
        public static int Run(string report)
        {
            var log=new StringBuilder();int count=0;Action<bool,string> check=(ok,msg)=>{count++;if(!ok)throw new Exception(msg);};
            string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
            try
            {
                var reader=new ScanVision(assets);ScanRegion[] elite=reader.Regions.Where(r=>r.Tier=="elite").ToArray();
                check(elite.Length==20&&elite.Sum(r=>r.Spots.Count)==524,"Elite scan coverage");
                foreach(ScanRegion region in elite)
                {
                    string text=region.Name+". You are too far away and nothing scans. Reminder: The scan range of this orb is "+region.BaseRange+" paces.";
                    check(reader.MatchEliteText(text)==region,"Scan text "+region.Id);
                    check(reader.MatchEliteText(text.Replace(region.BaseRange+" paces","99 paces"))==null,"Reject wrong range "+region.Id);
                    foreach(int level in region.Levels)
                    {check(File.Exists(Path.Combine(assets,"scan",region.Id+"-"+level+".png")),"Map for level "+region.Id+":"+level);check(File.Exists(Path.Combine(assets,"scan",region.Id+"-"+level+"-structure.png")),"Tracking reference for level "+region.Id+":"+level);}
                    using(var image=new Bitmap(280,340))
                    {
                        using(Graphics g=Graphics.FromImage(image))
                        using(var paper=new SolidBrush(Color.FromArgb(173,157,99)))
                        using(var font=new Font("Segoe UI",12F,FontStyle.Regular,GraphicsUnit.Pixel))
                        using(var format=new StringFormat{Alignment=StringAlignment.Center})
                        {g.Clear(Color.FromArgb(12,30,35));g.FillRectangle(paper,45,20,190,300);g.DrawString(text,font,Brushes.White,new RectangleF(55,65,170,245),format);}
                        var timer=Stopwatch.StartNew();DetectedPuzzle detected=reader.Find(image,new Pixels(image));
                        if(detected==null)image.Save(report+".png");
                        check(detected!=null&&detected.Kind==PuzzleKind.Scan&&detected.Scan.Region.Id==region.Id,"Scan OCR "+region.Id+": "+reader.LastText);
                        log.AppendLine("Elite scan OCR "+region.Id+": "+timer.ElapsedMilliseconds+"ms");
                    }
                }
                var session=new ScanSession();session.Observe(new ScanReading{Region=elite[0]});DateTime now=DateTime.UtcNow;
                var match=new MinimapMatch{Position=new PointF(3000,3900)};session.ApplyMatch(match,now);session.ApplyMatch(match,now.AddMilliseconds(100));check(session.PositionTracked,"Confirmed position");session.ApplyMatch(null,now.AddMilliseconds(200));check(session.Position.HasValue&&!session.PositionTracked,"Short miss is labelled last confirmed");session.ApplyMatch(match,now.AddMilliseconds(300));check(!session.PositionTracked,"One recovery frame does not flash the live marker");session.ApplyMatch(match,now.AddMilliseconds(400));check(session.PositionTracked,"Two recovery frames resume the live marker");session.ApplyMatch(null,now.AddMilliseconds(1100));check(!session.Position.HasValue,"Miss grace is bounded");check(session.LastKnown.HasValue&&session.LastKnown.Value==match.Position&&!session.InRange(match.Position),"Lost tracking retains an explicitly stale marker without claiming reachable spots");
                var retention=new ClueRetention();var refs=new ReferenceClueReader(assets).Clues;
                var a=new DetectedPuzzle{Kind=PuzzleKind.EmoteClue,Master=refs[0].Card};var b=new DetectedPuzzle{Kind=PuzzleKind.EmoteClue,Master=refs[1].Card};
                retention.Accept(a);retention.Missing();check(retention.Display(null)==a,"Travel directions stay");retention.Accept(b);check(retention.Display(null)==b,"New text clue replaces old without Clear");retention.Accept(new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=new ScanReading{Region=elite[0]}});check(retention.Saved.Kind==PuzzleKind.Scan,"Scan replaces text");retention.Accept(new DetectedPuzzle{Kind=PuzzleKind.Lockbox});check(retention.Saved==null,"Puzzle replaces retained card");
                var compass=new CompassSession(assets);compass.Bearings.Add(new CompassBearing{Position=new CompassPosition{X=3000,Y=3300}});compass.Observe(new CompassReading{Solved=true},now);compass.Observe(new CompassReading{Angle=1},now.AddMilliseconds(200));check(compass.Bearings.Count==0,"New compass after solved X resets bearings");
                var detector=new PuzzleDetector(assets);
                using(Bitmap image=TestFixtures.KnotModal())
                {
                    DetectedPuzzle clean=detector.Detect(image);check(clean!=null&&clean.Kind==PuzzleKind.Knot,"Knot fixture");
                    var marks=new List<GuidanceMark>();foreach(Point[] buttons in clean.Knot.Buttons)foreach(Point button in buttons)marks.Add(new GuidanceMark{Box=new Rectangle(clean.Bounds.X+button.X-13,clean.Bounds.Y+button.Y-13,26,26),Text="4",Colour=Color.Lime});
                    using(Graphics g=Graphics.FromImage(image))GuidanceDrawing.Draw(g,marks);
                    DetectedPuzzle marked=detector.ReadAgain(image,clean);if(marked==null||marked.Key!=clean.Key)image.Save(report+"-knot.png");check(marked!=null&&marked.Key==clean.Key,"Visible knot counts do not corrupt runes: "+(marked==null?detector.DiagnoseKnot(image,clean.Bounds):clean.Key+" vs "+marked.Key));
                    // The generated fixture encodes connectivity in single pixels.
                    // Resampling destroys those markers; validate badge feedback at native scale.
                    foreach(Color colour in new[]{Color.Gold,Color.Silver})foreach(float scale in new[]{1F})
                    using(Bitmap original=TestFixtures.KnotModal())
                    {
                        using(Bitmap plain=Pixels.Crop(original,new Rectangle(Point.Empty,original.Size),new Size((int)(original.Width*scale),(int)(original.Height*scale))))
                        {DetectedPuzzle before=detector.Detect(plain);var kr=new KnotImageReader(assets);Rectangle kb=new Rectangle((int)(80*scale),(int)(75*scale),(int)(500*scale),(int)(300*scale));using(Bitmap norm=Pixels.Crop(plain,kb,new Size(500,300))){var kp=kr.Read(norm);check(before!=null&&before.Kind==PuzzleKind.Knot,"Unmarked knot at "+scale+" detected="+(before==null?"null":before.Kind.ToString())+" direct="+(kp==null?kr.LastError:"ok"));}}
                        foreach(GuidanceMark mark in marks)mark.Colour=colour;
                        using(Graphics g=Graphics.FromImage(original))GuidanceDrawing.Draw(g,marks);
                        using(Bitmap scaled=Pixels.Crop(original,new Rectangle(Point.Empty,original.Size),new Size((int)(original.Width*scale),(int)(original.Height*scale))))
                        {DetectedPuzzle read=detector.Detect(scaled);if(read==null||read.Key!=clean.Key)scaled.Save(report+"-scaled-knot.png");check(read!=null&&read.Kind==PuzzleKind.Knot&&read.Key==clean.Key,"Visible knot badges at "+scale+" colour "+colour+" "+(read==null?"null":read.Key));}
                    }
                    var engine=new PuzzleAnalysisEngine(assets);
                    using(Bitmap settled=TestFixtures.KnotModal())
                    {
                        using(AnalysisResult first=engine.Analyze(settled,6000,false,CancellationToken.None,true)){}
                        using(AnalysisResult next=engine.Analyze(settled,6000,false,CancellationToken.None,true))check(next.Plan!=null&&!next.Plan.KnotMoving,"Knot acquires settled counts");
                        using(Bitmap moving=(Bitmap)settled.Clone())
                        {
                            using(Graphics g=Graphics.FromImage(moving))using(Brush bg=new SolidBrush(Color.FromArgb(159,145,86)))g.FillRectangle(bg,clean.Bounds);
                            using(AnalysisResult pending=engine.Analyze(moving,6000,false,CancellationToken.None,true))check(pending.Plan!=null&&pending.Pending,"Framed rune animation retains the knot guide");
                        }
                        using(var closed=new Bitmap(settled.Width,settled.Height))using(AnalysisResult gone=engine.Analyze(closed,6000,false,CancellationToken.None,true))check(gone.Plan==null,"Closed knot removes guide");
                    }
                    check(!GuidanceDrawing.HideForCapture(false,PuzzleKind.Knot),"Knot stays visible during captures");
                }
                var slider=new DetectedPuzzle{Kind=PuzzleKind.Sliding,Scale=1,Grid=new Rectangle(0,0,273,273)};var steps=GuidanceDrawing.SliderMarks(slider,new[]{0,1,2,3});check(steps.Select(m=>m.Colour.ToArgb()).Distinct().Count()==4&&steps[0].Colour==Color.Lime,"Distinct slider step colours");
                log.AppendLine("PASS: "+count+" expansion assertions.");File.WriteAllText(report,log.ToString());return 0;
            }
            catch(Exception e){log.AppendLine("FAIL after "+count+": "+e);File.WriteAllText(report,log.ToString());return 1;}
        }
    }
}
