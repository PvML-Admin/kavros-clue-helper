using System;using System.Drawing;using System.IO;using System.Linq;using System.Threading;
namespace KavrosClueHelper
{
 public static class SliderGuidanceRegressionTests
 {
  private static int checks;private static void Check(bool pass,string message){checks++;if(!pass)throw new Exception("FAILED: "+message);}
  public static int Run(string report)
  {
   try
   {
    string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");var engine=new PuzzleAnalysisEngine(assets);var detector=new PuzzleDetector(assets);
    using(var native=new Bitmap(Path.Combine(assets,"test_assets","reported","slider-native.png")))
    using(AnalysisResult baseline=engine.Analyze(native,150,false,CancellationToken.None,true))
    {
     Check(baseline.Plan!=null&&baseline.Plan.Puzzle.Kind==PuzzleKind.Sliding,"native slider remains recognizable with badge-safe features");
     var puzzle=baseline.Plan.Puzzle;var route=baseline.Plan.Sliding;Check(route!=null&&route.Count>4,"slider has a route for preview");
     var fresh=engine.ReadOnlySlider(native,puzzle);Check(fresh!=null&&fresh.Board.SequenceEqual(puzzle.Board),"fresh-frame validation reads the current board without replanning");
     Check(baseline.Plan.Sliding.SequenceEqual(route),"fresh-frame read leaves the active route intact");
     using(var painted=(Bitmap)native.Clone())
     {
      using(Graphics g=Graphics.FromImage(painted))GuidanceDrawing.Draw(g,GuidanceDrawing.SliderMarks(puzzle,route));
      var read=detector.ReadAgain(painted,puzzle);Check(read!=null&&read.Board.SequenceEqual(puzzle.Board),"visible four-step badges do not change tile reading");
      using(AnalysisResult result=engine.Analyze(painted,150,false,CancellationToken.None,true))Check(result.Plan!=null&&result.Plan.Sliding.SequenceEqual(route),"overlay pixels do not restart or change the route");
     }
     using(var animation=(Bitmap)native.Clone())
     {
      using(Graphics g=Graphics.FromImage(animation))for(int i=0;i<25;i++)g.FillRectangle(Brushes.Black,GuidanceDrawing.Cell(puzzle,i));
      using(AnalysisResult pending=engine.Analyze(animation,150,false,CancellationToken.None,true))Check(pending.Pending&&pending.Plan!=null,"a framed animation retains the existing guide");
      using(AnalysisResult recovered=engine.Analyze(native,150,false,CancellationToken.None,true))Check(!recovered.Pending&&recovered.Plan!=null,"settled tiles resume immediately");
     }
     int[] board=(int[])puzzle.Board.Clone();
     using(var reference=new Bitmap(Path.Combine(assets,"data","sliders","sliders",puzzle.Theme+".png")))
     using(Bitmap blank=native.Clone(GuidanceDrawing.Cell(puzzle,Array.IndexOf(board,24)),System.Drawing.Imaging.PixelFormat.Format24bppRgb))
     for(int step=0;step<12;step++)
     {
      int move=route[step],empty=Array.IndexOf(board,24);board[empty]=board[move];board[move]=24;
      using(var frame=(Bitmap)native.Clone())
      {
       using(Graphics g=Graphics.FromImage(frame))for(int i=0;i<25;i++){if(board[i]==24)g.DrawImage(blank,GuidanceDrawing.Cell(puzzle,i));else g.DrawImage(reference,GuidanceDrawing.Cell(puzzle,i),new Rectangle(board[i]%5*49,board[i]/5*49,49,49),GraphicsUnit.Pixel);}
       using(Graphics g=Graphics.FromImage(frame))GuidanceDrawing.Draw(g,GuidanceDrawing.SliderMarks(puzzle,route.Skip(step).ToList(),step+1));
       using(Bitmap captured=step%2==0?frame.Clone(Rectangle.Intersect(new Rectangle(Point.Empty,frame.Size),Rectangle.Inflate(puzzle.Grid,60,60)),System.Drawing.Imaging.PixelFormat.Format24bppRgb):(Bitmap)frame.Clone())
       using(AnalysisResult result=engine.Analyze(captured,150,false,CancellationToken.None,true))
       {
        Check(result.Plan!=null&&!result.Pending&&result.Plan.Puzzle.Board.SequenceEqual(board),"each move is read beneath the still-visible overlay");
        Check(result.Plan.Sliding.SequenceEqual(route.Skip(step+1)),"each move advances the cached route");
        var next=GuidanceDrawing.SliderMarks(result.Plan.Puzzle,result.Plan.Sliding,step+2);Check(next.Count==4&&next[0].Text==(step+2).ToString(),"rolling preview replenishes four numbered steps");
       }
      }
     }
     Check(engine.SlidingRouteReuses>=12,"route survives alternating full/cropped captures without replanning");
     using(var closed=new Bitmap(native.Width,native.Height))using(AnalysisResult gone=engine.Analyze(closed,150,false,CancellationToken.None,true))Check(gone.Plan==null&&!gone.Pending,"closing the actual puzzle clears guidance");
    }
    foreach(float scale in new[]{0.8F,1F,1.25F,2F})
    using(Bitmap original=TestFixtures.SliderImage("castle",Enumerable.Range(0,25).Select(i=>i==23?24:i==24?23:i).ToArray(),"rev944"))
    using(Bitmap frame=Pixels.Crop(original,new Rectangle(Point.Empty,original.Size),new Size((int)Math.Round(original.Width*scale),(int)Math.Round(original.Height*scale))))
    {
     var puzzle=detector.Detect(frame);Check(puzzle!=null&&puzzle.Kind==PuzzleKind.Sliding,"slider fixture at scale "+scale);
     var marks=GuidanceDrawing.SliderMarks(puzzle,new[]{23,18,17,22});using(Graphics g=Graphics.FromImage(frame))GuidanceDrawing.Draw(g,marks);
     var read=detector.ReadAgain(frame,puzzle);Check(read!=null&&read.Board.SequenceEqual(puzzle.Board),"badges stay readable to the detector at scale "+scale);
    }
    Check(!GuidanceDrawing.HideForCapture(false,PuzzleKind.Sliding),"slider stays visible in screenshots");
    File.AppendAllText(report,"V0.5.5 SLIDER GUIDANCE PASS: "+checks+" assertions. Visible badges, twelve consecutive moves, rolling four-step previews, animation retention, closure and scaled tile reading. No windows shown or game input/capture.\r\n");return 0;
   }
   catch(Exception e){File.AppendAllText(report,e+"\r\n");return 1;}
  }
 }
}
