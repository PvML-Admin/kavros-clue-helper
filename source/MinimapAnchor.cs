using System;using System.Collections.Generic;using System.Drawing;using System.IO;using System.Linq;
namespace KavrosClueHelper
{
 public sealed class MinimapAnchor
 {
  private sealed class Model {public ImagePattern Pattern;public Size Size;public Point[] Points;public double[] Values;public double Energy;public Point[] Blue;}
  public bool Strong {get;private set;}
  private readonly List<Model> models=new List<Model>();
  public MinimapAnchor(string assets)
  {
   string file=Path.Combine(assets,"minimap","home.png");if(!File.Exists(file))return;
   using(var original=new Bitmap(file))foreach(float scale in new[]{1F,0.8F,1.25F,1.5F,2F,0.6F})
   using(Bitmap image=Pixels.Crop(original,new Rectangle(Point.Empty,original.Size),new Size((int)Math.Round(original.Width*scale),(int)Math.Round(original.Height*scale))))
   {
    var points=new List<Point>();var values=new List<double>();var p=new Pixels(image);
    for(int y=(int)(image.Height*0.23);y<image.Height*0.77;y++)for(int x=(int)(image.Width*0.23);x<image.Width*0.77;x++){Color c=p.Get(x,y);points.Add(new Point(x,y));values.Add(Math.Min(c.R,Math.Min(c.G,c.B)));}
    double mean=values.Average();double[] centred=values.Select(v=>v-mean).ToArray();models.Add(new Model{Pattern=new ImagePattern(image),Size=image.Size,Points=points.ToArray(),Values=centred,Energy=centred.Sum(v=>v*v),Blue=points.Where(q=>{Color c=p.Get(q.X,q.Y);return c.B>c.R+16&&c.B>c.G+10;}).ToArray()});
   }
  }
  private static double Glyph(Pixels image,Point hit,Model model)
  {
   double sum=0,squares=0,product=0;
   for(int i=0;i<model.Points.Length;i++){Point p=model.Points[i];Color c=image.Get(hit.X+p.X,hit.Y+p.Y);double v=Math.Min(c.R,Math.Min(c.G,c.B));sum+=v;squares+=v*v;product+=v*model.Values[i];}
   double energy=squares-sum*sum/model.Points.Length;int blue=model.Blue.Count(p=>{Color c=image.Get(hit.X+p.X,hit.Y+p.Y);return c.B>c.R+8&&c.B>c.G+5;});return energy<=0||model.Blue.Length<4||blue<model.Blue.Length*0.60?0:product/Math.Sqrt(energy*model.Energy);
  }
  public List<Rectangle> Find(Bitmap image)
  {
   var candidates=new List<Tuple<Rectangle,double>>();var pixels=new Pixels(image);
   foreach(Model model in models)foreach(Point hit in model.Pattern.Find(pixels,new Rectangle(Point.Empty,image.Size),1000,100))
   {double score=Glyph(pixels,hit,model);if(score>=0.60)candidates.Add(Tuple.Create(new Rectangle(hit,model.Size),score));}
   Strong=candidates.Count>0&&candidates.Max(c=>c.Item2)>=0.90;
   var result=new List<Rectangle>();foreach(var candidate in candidates.Where(c=>c.Item2>=candidates.Max(v=>v.Item2)-0.08).OrderByDescending(c=>c.Item2))
   {Rectangle box=candidate.Item1;if(!result.Any(r=>Rectangle.Inflate(r,r.Width/2,r.Height/2).Contains(new Point(box.Left+box.Width/2,box.Top+box.Height/2))))result.Add(box);}
   return result;
  }
  public static bool ContainsHome(Rectangle view,Rectangle home)
  {
   double scale=home.Width/22.0;return home.Left>=view.Left-4*scale&&home.Left-view.Left<30*scale&&home.Bottom<=view.Bottom+4*scale&&view.Bottom-home.Bottom<22*scale;
  }
  public static MinimapViewport FromCentre(int width,int height,Point centre,Rectangle home)
  {
   double scale=home.Width/22.0;int left=(int)Math.Round(home.Left-7*scale),bottom=(int)Math.Round(home.Bottom+8*scale);
   if(centre.X-left<30||bottom-centre.Y<30)return null;
   Rectangle bounds=Rectangle.Intersect(new Rectangle(0,0,width,height),Rectangle.FromLTRB(left,2*centre.Y-bottom,2*centre.X-left,bottom));
   if(bounds.Width<60||bounds.Height<60||bounds.Width>1024||bounds.Height>1024)return null;
   return new MinimapViewport{Bounds=bounds,FrameSides=2};
  }
  private static bool Frame(int colour)
  {int r=(colour>>16)&255,g=(colour>>8)&255,b=colour&255;return r<12&&g<12&&b<12||r<65&&g<100&&b<115&&g>=r+3&&b>=r+4;}
  public static MinimapViewport Viewport(int width,int height,Rectangle home,Func<int,int,int> pixel)
  {
   double scale=home.Width/22.0;int expectedLeft=(int)Math.Round(home.Left-7*scale),expectedBottom=(int)Math.Round(home.Bottom+8*scale);
   int left=-1,top=-1,length=0,start=Math.Max(0,home.Top-home.Height/2),margin=Math.Max(3,(int)Math.Ceiling(4*scale));
   for(int x=Math.Max(0,expectedLeft-margin);x<=Math.Min(width-1,expectedLeft+margin);x++)
   {
    int bad=0,last=start,hits=0;
    for(int y=start;y>=Math.Max(0,start-1024);y--){if(Frame(pixel(x,y))){last=y;hits++;bad=0;}else if(++bad>3)break;}
    int run=start-last+1;if(run>=60&&hits>run*0.85&&run>length){length=run;left=x;top=last;}
   }
   if(left<0)return null;int right=-1,bottom=-1,best=0;
   for(int y=Math.Max(0,expectedBottom-margin);y<=Math.Min(height-1,expectedBottom+margin);y++)
   {
    int begin=Math.Min(width-1,left+home.Width*2),last=begin,bad=0,hits=0;
    for(int x=begin;x<Math.Min(width,left+1024);x++){if(Frame(pixel(x,y))){last=x;hits++;bad=0;}else if(++bad>3)break;}
    int run=last-begin+1;if(run<30||hits<run*0.85)continue;
    int score=run*10-Math.Abs(y-expectedBottom);if(score>best){best=score;right=last+1;bottom=y+1;}
   }
   if(right<0||bottom-top<60||right-left<60||bottom-top>1024||right-left>1024)return null;
   return new MinimapViewport{Bounds=Rectangle.FromLTRB(left,top,right,bottom),FrameSides=3};
  }
 }
}
