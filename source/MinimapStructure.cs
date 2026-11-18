using System;using System.Collections.Generic;using System.Drawing;using System.IO;using System.Linq;using System.Threading;using System.Globalization;
namespace KavrosClueHelper
{
 public sealed class MinimapStructure
 {
  private sealed class Trial {public double X,Y,Scale,Angle,Error;}
  private int chosenMinimum=185;private byte[] field;private List<Point> edges;private int width,height,id;private RectangleF world;
  private static bool Pale(Color c,int minimum=185){return Math.Min(c.R,Math.Min(c.G,c.B))>=minimum&&Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<40;}
  private static byte[] Distance(bool[] mask,int w,int h)
  {
   byte[] f=mask.Select(b=>(byte)(b?0:64)).ToArray();
   for(int y=1;y<h;y++)for(int x=1;x<w-1;x++){int i=y*w+x;f[i]=(byte)Math.Min(f[i],Math.Min(Math.Min(f[i-1]+4,f[i-w]+4),Math.Min(f[i-w-1]+6,f[i-w+1]+6)));}
   for(int y=h-2;y>=0;y--)for(int x=w-2;x>0;x--){int i=y*w+x;f[i]=(byte)Math.Min(f[i],Math.Min(Math.Min(f[i+1]+4,f[i+w]+4),Math.Min(f[i+w-1]+6,f[i+w+1]+6)));}
   return f;
  }
  private void Load(string assets,ScanRegion region)
  {
   if(field!=null&&id==region.Id*4+region.Level)return;
   string transform=Path.Combine(assets,"scan",region.Id+"-track-map.tsv");if(!File.Exists(transform))transform=Path.Combine(assets,"scan",region.Id+"-map.tsv");
   float[] b=File.ReadAllText(transform).Split(new[]{'\t'}).Select(v=>Single.Parse(v,CultureInfo.InvariantCulture)).ToArray();world=RectangleF.FromLTRB(b[0],b[1],b[2],b[3]);
   using(var image=new Bitmap(Path.Combine(assets,"scan",region.AssetKey+"-structure.png")))
   {
    width=image.Width;height=image.Height;var p=new Pixels(image);var pale=new bool[width*height];var outline=new bool[pale.Length];edges=new List<Point>();
    for(int y=0;y<height;y++)for(int x=0;x<width;x++)pale[y*width+x]=Pale(p.Get(x,y));
    for(int y=1;y<height-1;y++)for(int x=1;x<width-1;x++){int i=y*width+x;if(pale[i]&&(!pale[i-1]||!pale[i+1]||!pale[i-width]||!pale[i+width])){outline[i]=true;if((x+y)%2==0)edges.Add(new Point(x,y));}}
    field=Distance(outline,width,height);
   }
   id=region.Id*4+region.Level;
  }
  private double Score(IList<Point> samples,double x,double y,double scale,double angle,double cutoff)
  {
   double cos=Math.Cos(angle)/scale,sin=Math.Sin(angle)/scale,sx=width/world.Width,sy=height/world.Height,total=0;int valid=0;
   for(int i=0;i<samples.Count;i++)
   {
    Point p=samples[i];double wx=x+p.X*cos-p.Y*sin,wy=y-p.X*sin-p.Y*cos;int rx=(int)Math.Round((wx-world.Left)*sx),ry=(int)Math.Round((world.Bottom-wy)*sy);
    if(rx<0||ry<0||rx>=width||ry>=height){total+=8;continue;}total+=Math.Min(8,field[ry*width+rx]*scale/(4*sx));valid++;if(total>cutoff*samples.Count)return 100;
   }
   return valid<samples.Count*0.85?100:total/samples.Count;
  }
  private double Reverse(Trial t,byte[] source,int sw,int sh,Point dot,int rx,int ry)
  {
   double c=Math.Cos(t.Angle)*t.Scale,s=Math.Sin(t.Angle)*t.Scale,total=0;int count=0;
   foreach(Point p in edges)
   {
    double wx=world.Left+p.X*world.Width/width-t.X,wy=world.Bottom-p.Y*world.Height/height-t.Y;double dx=wx*c-wy*s,dy=-wx*s-wy*c;
    if(dx*dx/(rx*rx)+dy*dy/(ry*ry)>0.88||dx*dx+dy*dy<100)continue;int x=(int)Math.Round(dot.X+dx),y=(int)Math.Round(dot.Y+dy);if(x<0||y<0||x>=sw||y>=sh)continue;total+=Math.Min(8,source[y*sw+x]/4.0);count++;
   }
   return count<40?100:total/count;
  }
  private static void Keep(List<Trial> list,Trial trial)
  {
   Trial same=list.FirstOrDefault(t=>Math.Abs(t.X-trial.X)<6&&Math.Abs(t.Y-trial.Y)<6&&Math.Abs(t.Scale-trial.Scale)<0.3&&Math.Abs(CompassMath.Difference(t.Angle,trial.Angle))<Math.PI/12);
   if(same!=null){if(same.Error<=trial.Error)return;list.Remove(same);}list.Add(trial);list.Sort((a,b)=>a.Error.CompareTo(b.Error));if(list.Count>32)list.RemoveAt(32);
  }
  public MinimapMatch Find(Bitmap image,string assets,ScanRegion region,Point dot,Rectangle viewport,MinimapMatch previous,CancellationToken token,out string diagnostics,double? rotationHint=null)
  {
   Load(assets,region);int first=chosenMinimum;MinimapMatch match=FindPass(image,assets,region,dot,viewport,previous,token,out diagnostics,first,rotationHint);
   if(match!=null)return match;
   int second=first==185?155:185;match=FindPass(image,assets,region,dot,viewport,previous,token,out diagnostics,second,rotationHint);
   if(match!=null)chosenMinimum=second;return match;
  }
  private MinimapMatch FindPass(Bitmap image,string assets,ScanRegion region,Point dot,Rectangle viewport,MinimapMatch previous,CancellationToken token,out string diagnostics,int minimum,double? rotationHint)
  {
   Load(assets,region);var pixels=new Pixels(image);var samples=new List<Point>();var mask=new bool[image.Width*image.Height];
   int rx=Math.Min(320,Math.Min(dot.X-viewport.Left,viewport.Right-dot.X)-18),ry=Math.Min(320,Math.Min(dot.Y-viewport.Top,viewport.Bottom-dot.Y)-18);
   foreach(int sourceMinimum in new[]{minimum})
   {
   samples.Clear();Array.Clear(mask,0,mask.Length);
   for(int y=-ry;y<=ry;y++)for(int x=-rx;x<=rx;x++)
   {
    if(x*x+y*y<100||x*x/(double)(rx*rx)+y*y/(double)(ry*ry)>1||!Pale(pixels.Get(dot.X+x,dot.Y+y),sourceMinimum))continue;int same=0;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(Pale(pixels.Get(dot.X+x+dx,dot.Y+y+dy),sourceMinimum))same++;
    if(same>=3){mask[(dot.Y+y)*image.Width+dot.X+x]=true;if((x+y)%2==0)samples.Add(new Point(x,y));}
   }
   if(samples.Count>=100)break;
   }
   samples=samples.OrderBy(p=>unchecked((uint)((p.X+2048)*73856093)^(uint)((p.Y+2048)*19349663))).Take(400).ToList();diagnostics="outline minimum "+minimum+" samples "+samples.Count;if(samples.Count<100)return null;
   var coarse=samples.Take(32).ToList();var best=new List<Trial>();double left=region.MapBounds.Left,right=region.MapBounds.Right,bottom=region.MapBounds.Top,top=region.MapBounds.Bottom;
   double[] scales={0.75,1,1.5,2,2.5,3,3.5,4,4.5,5,6,8,12,16},angles=Enumerable.Range(0,48).Select(i=>i*Math.PI/24).ToArray();
   if(rotationHint.HasValue)angles=Enumerable.Range(-6,13).Select(i=>rotationHint.Value+i*Math.PI/36).ToArray();
   if(previous!=null){left=previous.Position.X-28;right=previous.Position.X+28;bottom=previous.Position.Y-28;top=previous.Position.Y+28;scales=new[]{previous.PixelsPerTile*0.8,previous.PixelsPerTile-0.2,previous.PixelsPerTile,previous.PixelsPerTile+0.2,previous.PixelsPerTile*1.25};double a=rotationHint??previous.Rotation;angles=rotationHint.HasValue?new[]{a-Math.PI/18,a,a+Math.PI/18}:Enumerable.Range(-12,25).Select(i=>a+i*Math.PI/24).ToArray();}
   foreach(double scale in scales)foreach(double angle in angles)
   {
    token.ThrowIfCancellationRequested();for(double y=bottom;y<=top;y+=2)for(double x=left;x<=right;x+=2){double limit=best.Count<32?4:Math.Min(4,best.Last().Error);double e=Score(coarse,x,y,scale,angle,limit);if(e<limit)Keep(best,new Trial{X=x,Y=y,Scale=scale,Angle=angle,Error=e});}
   }
   var refined=new List<Trial>();byte[] source=Distance(mask,image.Width,image.Height);
   foreach(Trial seed in best)
   {
    Trial win=new Trial{X=seed.X,Y=seed.Y,Scale=seed.Scale,Angle=seed.Angle,Error=Score(samples,seed.X,seed.Y,seed.Scale,seed.Angle,100)};
    foreach(double da in new[]{-Math.PI/24,0,Math.PI/24})foreach(double ds in new[]{-0.25,0.0,0.25})for(double y=seed.Y-3;y<=seed.Y+3;y++)for(double x=seed.X-3;x<=seed.X+3;x++)
    {token.ThrowIfCancellationRequested();if(seed.Scale+ds<0.5)continue;double e=Score(samples,x,y,seed.Scale+ds,seed.Angle+da,win.Error);if(e<win.Error)win=new Trial{X=x,Y=y,Scale=seed.Scale+ds,Angle=seed.Angle+da,Error=e};}
    Trial centre=win;foreach(double da in new[]{-Math.PI/90,0,Math.PI/90})foreach(double ds in new[]{-0.125,-0.0625,0.0,0.0625,0.125})for(double y=centre.Y-1;y<=centre.Y+1;y+=0.5)for(double x=centre.X-1;x<=centre.X+1;x+=0.5)
    {double e=Score(samples,x,y,centre.Scale+ds,centre.Angle+da,win.Error);if(e<win.Error)win=new Trial{X=x,Y=y,Scale=centre.Scale+ds,Angle=centre.Angle+da,Error=e};}
    double reverse=Reverse(win,source,image.Width,image.Height,dot,rx,ry);win.Error=(win.Error+reverse)/2;Keep(refined,win);
   }
   Trial winner=refined.FirstOrDefault(),runner=winner==null?null:refined.FirstOrDefault(t=>Math.Sqrt(Math.Pow(t.X-winner.X,2)+Math.Pow(t.Y-winner.Y,2))>12);if(winner==null)return null;
   diagnostics+=" best "+winner.X+","+winner.Y+" scale "+winner.Scale+" angle "+winner.Angle+" error "+winner.Error+" runner "+(runner==null?"none":runner.Error.ToString());
   if(rotationHint.HasValue&&Math.Abs(CompassMath.Difference(winner.Angle,rotationHint.Value))>Math.PI*35/180)return null;
   if(winner.Error>3||runner!=null&&runner.Error<winner.Error+0.6)return null;
   return new MinimapMatch{Position=new PointF((float)winner.X,(float)winner.Y),PixelsPerTile=winner.Scale,Rotation=winner.Angle,Error=winner.Error,PlayerPixel=dot,Viewport=viewport};
  }
 }
}
