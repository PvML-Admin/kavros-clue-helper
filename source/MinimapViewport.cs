using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
namespace KavrosClueHelper
{
    public sealed class MinimapViewport
    {
        public Rectangle Bounds;
        public int FrameSides;
        private static bool Frame(int c)
        {
            int r=(c>>16)&255,g=(c>>8)&255,b=c&255;
            return r<12&&g<12&&b<12||r<65&&g<100&&b<115&&g>=r+3&&b>=r+4;
        }
        private static double Stripe(Point centre,int distance,int dx,int dy,int length,Func<int,int,int> pixel)
        {
            int hits=0,count=0;
            for(int offset=-length;offset<=length;offset+=2)
            {if(Frame(pixel(centre.X+distance*dx-offset*dy,centre.Y+distance*dy+offset*dx)))hits++;count++;}
            return hits/(double)count;
        }
        public static MinimapViewport Find(int width,int height,Point centre,Func<int,int,int> pixel)
        {
            int[] limits={centre.X,width-1-centre.X,centre.Y,height-1-centre.Y};
            var candidates=new List<int>[4];
            for(int side=0;side<4;side++)
            {
                candidates[side]=new List<int>();int dx=side==0?-1:side==1?1:0,dy=side==2?-1:side==3?1:0;
                for(int distance=24;distance<Math.Min(640,limits[side]-2);distance++)
                {
                    int length=Math.Min(48,Math.Max(18,distance/2));
                    int crossLimit=side<2?Math.Min(centre.Y,height-1-centre.Y):Math.Min(centre.X,width-1-centre.X);
                    length=Math.Min(length,crossLimit-2);if(length<12)break;
                    if(Stripe(centre,distance,dx,dy,length,pixel)>=0.87&&Stripe(centre,distance-5,dx,dy,length,pixel)<0.72)
                    {if(candidates[side].Count==0||distance-candidates[side].Last()>5)candidates[side].Add(distance);}
                }
                if(limits[side]<=640)candidates[side].Add(limits[side]);
            }
            int[] found=new int[4];int sides=0;
            for(int axis=0;axis<2;axis++)
            {
                int first=axis*2,second=first+1;
                var pair=(from a in candidates[first] from b in candidates[second]
                    where a+b>=64&&Math.Min(a,b)/(double)Math.Max(a,b)>=0.70
                    orderby a+b select new[]{a,b}).FirstOrDefault();
                if(pair==null)return null;found[first]=pair[0];found[second]=pair[1];
                if(pair[0]!=limits[first])sides++;if(pair[1]!=limits[second])sides++;
            }
            Rectangle bounds=Rectangle.FromLTRB(centre.X-found[0],centre.Y-found[2],centre.X+found[1]+1,centre.Y+found[3]+1);
            double rx=Math.Min(found[0],found[1])/(double)Math.Max(1,Math.Max(found[0],found[1]));
            double ry=Math.Min(found[2],found[3])/(double)Math.Max(1,Math.Max(found[2],found[3]));
            if(bounds.Width<60||bounds.Height<60||bounds.Width>1024||bounds.Height>1024||rx<0.45||ry<0.45)return null;
            if(sides<2&&(width>800||height>800))return null;
            return new MinimapViewport{Bounds=bounds,FrameSides=sides};
        }
    }
}
