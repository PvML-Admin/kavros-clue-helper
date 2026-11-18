using System;using System.Collections.Generic;using System.Drawing;using System.IO;using System.Linq;
namespace KavrosClueHelper
{
 public sealed class MinimapOrientation
 {
  private readonly string file;
  private readonly Dictionary<int,ImagePattern> patterns=new Dictionary<int,ImagePattern>();
  public MinimapOrientation(string assets){file=Path.Combine(assets,"minimap","north-ring.png");}
  public double? Read(Bitmap image,Rectangle viewport,Rectangle home)
  {
   if(!File.Exists(file))return null;double scale=home.Width/22.0;ImagePattern pattern;
   if(!patterns.TryGetValue(home.Width,out pattern))using(var original=new Bitmap(file))using(Bitmap resized=Pixels.Crop(original,new Rectangle(Point.Empty,original.Size),new Size((int)Math.Round(original.Width*scale),(int)Math.Round(original.Height*scale))))patterns[home.Width]=pattern=new ImagePattern(resized);
   var pixels=new Pixels(image);Rectangle area=Rectangle.Intersect(new Rectangle(Point.Empty,image.Size),new Rectangle((int)(home.Left-14*scale),Math.Max(0,(int)(viewport.Top-20*scale)),(int)(60*scale),(int)(80*scale)));
   foreach(Point hit in pattern.Find(pixels,area,3,100))
   {
    double cx=hit.X+13*scale,cy=hit.Y+12*scale,sx=0,sy=0,weight=0;int count=0,radius=(int)Math.Ceiling(10*scale);
    for(int y=(int)cy-radius;y<=cy+radius;y++)for(int x=(int)cx-radius;x<=cx+radius;x++)
    {
     if((x-cx)*(x-cx)+(y-cy)*(y-cy)>radius*radius)continue;Color c=pixels.Get(x,y);
     if(c.R<110||c.R<c.G*1.7||c.R<c.B*1.7)continue;double w=c.R-Math.Max(c.G,c.B);sx+=(x-cx)*w;sy+=(y-cy)*w;weight+=w;count++;
    }
    if(count<2||weight==0)continue;double dx=sx/weight,dy=sy/weight;if(dx*dx+dy*dy<4*scale*scale)continue;
    return Math.Atan2(-dx,-dy);
   }
   return null;
  }
 }
}
