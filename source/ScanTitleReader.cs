using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace KavrosClueHelper
{
    // Match the white letters separately from the tan parchment and its shadows.
    internal sealed class ScanTitleReader
    {
        private sealed class Glyphs
        {
            public int Width, Height;
            public bool[] Ink;
        }
        private readonly Dictionary<int,Glyphs> titles = new Dictionary<int,Glyphs>();
        private Glyphs different;
        public ScanTitleReader(string assets)
        {
            string directory = Path.Combine(assets, "scan-titles");
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory, "*.png"))
            using (Bitmap image = new Bitmap(file))
            {
                var mask = Extract(new Pixels(image), new Rectangle(Point.Empty,image.Size));
                int id;
                if (Int32.TryParse(Path.GetFileNameWithoutExtension(file),out id)) titles[id]=mask;
                else if (Path.GetFileNameWithoutExtension(file)=="different") different=mask;
            }
        }
        private static bool White(Color c)
        { return Math.Min(c.R,Math.Min(c.G,c.B))>=192 && Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<44; }
        private static Glyphs Extract(Pixels p,Rectangle box)
        {
            int x0=box.Right,y0=box.Bottom,x1=-1,y1=-1;
            for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++)if(White(p.Get(x,y)))
            { x0=Math.Min(x0,x); x1=Math.Max(x1,x); y0=Math.Min(y0,y); y1=Math.Max(y1,y); }
            if(x1<x0||y1<y0)return null;
            int w=x1-x0+1,h=y1-y0+1;
            var g=new Glyphs{Width=w,Height=h,Ink=new bool[w*h]};
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)g.Ink[y*w+x]=White(p.Get(x0+x,y0+y));
            return g;
        }
        private static double Score(Glyphs a,Glyphs b)
        {
            if(a==null||b==null||Math.Abs(Math.Log(a.Width*(double)b.Height/(a.Height*b.Width)))>.30)return 0;
            int both=0,total=0;
            for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)
            {
                bool k=a.Ink[y*a.Width+x], v=b.Ink[(int)((y+.5)*b.Height/a.Height)*b.Width+(int)((x+.5)*b.Width/a.Width)];
                if(k)total++;if(v)total++;if(k&&v)both++;
            }
            return 2.0*both/Math.Max(1,total);
        }
        public ScanReading Read(Pixels pixels, Rectangle paper,List<ScanRegion> regions)
        {
            int left=paper.Left+paper.Width/10,right=paper.Right-paper.Width/10;
            int gap=Math.Max(5,paper.Height/30),first=-1,last=-1,body=-1,lines=0;
            for(int y=paper.Top+paper.Height/8;y<paper.Top+paper.Height*3/5;y++)
            {
                int count=0;for(int x=left;x<right;x++)if(White(pixels.Get(x,y)))count++;
                if(count<2)continue;
                if(first<0)first=y;
                if(body<0&&last>=0&&y-last>gap){body=y;lines=last;}
                last=y;
            }
            if(body<0||last-body<paper.Height/14)return null;
            Glyphs title=Extract(pixels,Rectangle.FromLTRB(left,first,right,lines+1));
            int winner=0;double best=0,runner=0;
            foreach(var entry in titles)
            {
                double score=Score(title,entry.Value);
                if(score>best){runner=best;best=score;winner=entry.Key;}else runner=Math.Max(runner,score);
            }
            if(best<.73||best-runner<.10)return null;
            ScanRegion region=regions.Find(r=>r.Id==winner);
            if(region==null)return null;
            Glyphs message=Extract(pixels,Rectangle.FromLTRB(left,body,right,body+Math.Max(5,(int)Math.Round(paper.Height*8.0/276))));
            return new ScanReading{Region=region,DifferentLevel=Score(message,different)>.73};
        }
    }
}
