using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace KavrosClueHelper
{
    // Scale-independent line signatures. A match requires the whole paragraph,
    // including equipment lines, rather than accepting parchment or a title.
    public static class ParagraphVision
    {
        public sealed class Line
        {
            public Rectangle Bounds;
            public double[] Signature;
        }
        public static bool Ink(Color p)
        { return p.R>40&&p.R<164&&p.G>25&&p.G<148&&p.B>15&&p.B<125&&p.R>p.G&&p.R-p.G<55&&p.G>p.B&&p.G-p.B<55; }
        public static List<Rectangle> FindPapers(Pixels p)
        {
            var regions=new List<Rectangle>();
            for(int y=8;y<p.Height-8;y+=12)
            {
                SearchCancellation.Check();
                int start=-1,last=-1,hits=0;
                for(int x=0;x<p.Width+40;x+=4)
                {
                    Color c=p.Get(x,y);
                    if(x<p.Width&&c.R>155&&c.G>115&&c.B>65&&c.R-c.G>5&&c.R-c.G<90&&c.G-c.B>12&&c.G-c.B<100)
                    {if(start<0)start=x;last=x;hits++;}
                    if(start>=0&&x-last>=36)
                    {
                        int width=last-start+4;
                        if(width>=180&&width<=1400&&hits*4>=width*0.6)
                        {
                            Rectangle run=Rectangle.FromLTRB(Math.Max(0,start-4),Math.Max(0,y-12),Math.Min(p.Width,last+8),Math.Min(p.Height,y+12));
                            int existing=regions.FindIndex(r=>y<=r.Bottom+12&&Rectangle.Intersect(new Rectangle(r.X,0,r.Width,1),new Rectangle(run.X,0,run.Width,1)).Width>=Math.Min(r.Width,run.Width)*0.85);
                            if(existing<0)regions.Add(run);else regions[existing]=Rectangle.Union(regions[existing],run);
                        }
                        start=-1;last=-1;hits=0;
                    }
                }
            }
            return regions.Where(r=>r.Height>=90&&r.Width>=180).OrderByDescending(r=>r.Width*r.Height).Take(8).ToList();
        }
        public static List<Line> Template(Bitmap image)
        {
            Pixels p=new Pixels(image);bool[] mask=new bool[p.Width*p.Height];
            for(int y=0;y<p.Height;y++)for(int x=0;x<p.Width;x++)mask[y*p.Width+x]=p.Get(x,y).A>=100;
            return Lines(mask,p.Width,p.Height,false);
        }
        public static List<Line> Read(Pixels pixels,Rectangle paper)
        {
            int w=paper.Width,h=paper.Height,margin=Math.Max(5,w/45);bool[] raw=new bool[w*h],mask=new bool[w*h];
            for(int y=2;y<h-2;y++)for(int x=margin;x<w-margin;x++)raw[y*w+x]=Ink(pixels.Get(paper.X+x,paper.Y+y));
            bool[] visited=new bool[raw.Length];var queue=new Queue<int>();var component=new List<int>();
            for(int index=0;index<raw.Length;index++)
            {
                if(!raw[index]||visited[index])continue;
                queue.Enqueue(index);visited[index]=true;component.Clear();int left=w,top=h,right=0,bottom=0;
                while(queue.Count>0)
                {
                    int v=queue.Dequeue(),x=v%w,y=v/w;component.Add(v);
                    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int n=ny*w+nx;
                        if(raw[n]&&!visited[n]){visited[n]=true;queue.Enqueue(n);}
                    }
                }
                if(component.Count>=2&&right-left<=Math.Max(45,w/6)&&bottom-top<=Math.Max(40,h/8)&&component.Count<=Math.Max(650,w*h/120))
                    foreach(int v in component)mask[v]=true;
            }
            return Lines(mask,w,h,true);
        }
        private static List<Line> Lines(bool[] mask,int w,int h,bool filter)
        {
            var spans=new List<Rectangle>();int first=-1,last=-1;
            for(int y=0;y<=h+3;y++)
            {
                int count=0;if(y<h)for(int x=0;x<w;x++)if(mask[y*w+x])count++;
                if(count>=Math.Max(3,filter?w/110:3)){if(first<0)first=y;last=y;}
                if(first>=0&&y-last>3)
                {
                    int top=Math.Max(0,first-2),bottom=Math.Min(h,last+3),left=w,right=-1,inkTop=h,inkBottom=-1;
                    for(int yy=top;yy<bottom;yy++)for(int x=0;x<w;x++)if(mask[yy*w+x]){left=Math.Min(left,x);right=Math.Max(right,x);inkTop=Math.Min(inkTop,yy);inkBottom=Math.Max(inkBottom,yy);}
                    Rectangle r=Rectangle.FromLTRB(left,inkTop,right+1,inkBottom+1);
                    if(right>=left&&r.Width>=25&&r.Height>=4&&r.Height<=48&&(!filter||Math.Abs((left+right)/2.0-w/2.0)<w*0.18))spans.Add(r);
                    first=last=-1;
                }
            }
            // Enlarged descenders can fall below the row-count threshold and
            // appear as a second short band. Rejoin touching ink bands before
            // checking paragraph line spacing.
            for(int i=1;i<spans.Count;i++)
                if(spans[i].Top<=spans[i-1].Bottom+Math.Max(2,spans[i-1].Height/8))
                {spans[i-1]=Rectangle.Union(spans[i-1],spans[i]);spans.RemoveAt(i);i--;}
            return spans.Select(r=>new Line{Bounds=r,Signature=Signature(mask,w,h,r)}).ToList();
        }
        private static double[] Signature(bool[] mask,int w,int h,Rectangle box)
        {
            const int sw=160,sh=20;double[] result=new double[sw*sh];
            for(int y=0;y<sh;y++)for(int x=0;x<sw;x++)
            {
                double px=box.Left+(x+0.5)*box.Width/sw-0.5,py=box.Top+(y+0.5)*box.Height/sh-0.5,total=0;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    double xx=px+dx*0.32,yy=py+dy*0.25;int ix=(int)Math.Floor(xx),iy=(int)Math.Floor(yy);double fx=xx-ix,fy=yy-iy;
                    for(int j=0;j<2;j++)for(int i=0;i<2;i++)
                        if(ix+i>=0&&iy+j>=0&&ix+i<w&&iy+j<h&&mask[(iy+j)*w+ix+i])total+=(i==0?1-fx:fx)*(j==0?1-fy:fy);
                }
                result[y*sw+x]=total/9;
            }
            return result;
        }
        public static double Similarity(Line a,Line b)
        {
            const int w=160,h=20;double best=0;
            for(int dy=-2;dy<=2;dy++)for(int dx=-3;dx<=3;dx++)
            {
                double dot=0,aa=0,bb=0;
                for(int y=2;y<h-2;y++)for(int x=3;x<w-3;x++)
                {double v=a.Signature[y*w+x],z=b.Signature[(y+dy)*w+x+dx];dot+=v*z;aa+=v*v;bb+=z*z;}
                best=Math.Max(best,dot/Math.Sqrt(Math.Max(1e-12,aa*bb)));
            }
            return best;
        }
        public static double Match(List<Line> template,List<Line> lines,int start)
        {
            if(!LayoutMatches(template,lines,start))return 0;
            double sum=0,min=1,tw=template.Max(l=>l.Bounds.Width),rw=lines.Skip(start).Take(template.Count).Max(l=>l.Bounds.Width);
            for(int i=0;i<template.Count;i++)
            {
                if(Math.Abs(template[i].Bounds.Width/tw-lines[start+i].Bounds.Width/rw)>0.085)return 0;
                double score=Similarity(template[i],lines[start+i]);sum+=score;min=Math.Min(min,score);
            }
            return min<0.65?0:sum/template.Count;
        }
        public static bool LayoutMatches(List<Line> template,List<Line> lines,int start)
        {
            if(template.Count<3||start+template.Count>lines.Count)return false;
            double tw=template.Max(l=>l.Bounds.Width),rw=lines.Skip(start).Take(template.Count).Max(l=>l.Bounds.Width),scale=rw/tw;
            double centre=lines.Skip(start).Take(template.Count).Average(l=>l.Bounds.Left+l.Bounds.Width/2.0);
            for(int i=0;i<template.Count;i++)
            {
                Rectangle t=template[i].Bounds,r=lines[start+i].Bounds;
                if(Math.Abs(t.Width/tw-r.Width/rw)>0.10||Math.Abs(r.Left+r.Width/2.0-centre)>Math.Max(5,rw*0.06))return false;
                if(Math.Abs(r.Top-lines[start].Bounds.Top-(t.Top-template[0].Bounds.Top)*scale)>Math.Max(4,scale*5))return false;
            }
            return true;
        }
        public static bool CompleteRows(List<Line> template,List<Line> lines,int start)
        {
            if(template.Count<3||start+template.Count>lines.Count)return false;
            int last=template.Count-1;double scale=(lines[start+last].Bounds.Top-lines[start].Bounds.Top)/(double)Math.Max(1,template[last].Bounds.Top-template[0].Bounds.Top);
            if(scale<0.45||scale>2.5)return false;
            for(int i=0;i<template.Count;i++)
                if(Math.Abs(lines[start+i].Bounds.Top-lines[start].Bounds.Top-(template[i].Bounds.Top-template[0].Bounds.Top)*scale)>Math.Max(3,scale*4))return false;
            return true;
        }
    }
}

