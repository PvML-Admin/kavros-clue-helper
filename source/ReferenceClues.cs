// Reference clue data: Clue Trainer, Copyright (c) 2024 Lukas Gail, MIT.
// See THIRD_PARTY.txt and assets/data/LICENSE-ClueTrainer.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KavrosClueHelper
{
    public sealed class ReferenceClue
    {
        public MasterClue Card;
        public string Type;
        public string[] Text,Anagrams;
    }
    public sealed class ReferenceClueReader
    {
        public readonly List<ReferenceClue> Clues=new List<ReferenceClue>();
        private readonly NativeClueOcr ocr=new NativeClueOcr();
        private readonly Dictionary<int,MapSignature> maps=new Dictionary<int,MapSignature>();
        private readonly Dictionary<int,MapSignature> variants=new Dictionary<int,MapSignature>();
        private readonly List<KeyValuePair<string,Point>> lodestones=new List<KeyValuePair<string,Point>>();
        private static readonly Regex Coordinates=new Regex(@"^(\d{1,2}) degrees? (\d{1,2}) minutes? (north|south) (\d{1,2}) degrees? (\d{1,2}) minutes? (east|west)$",RegexOptions.CultureInvariant);
        private ulong cachedHash;private Size cachedSize;private Rectangle cachedPaper;private ReferenceClue cached;
        private bool hasCache;
        private readonly Dictionary<ulong,ReferenceClue> recent=new Dictionary<ulong,ReferenceClue>();
        private readonly Queue<ulong> recentOrder=new Queue<ulong>();
        public Action<string> Progress;
        public string LastText,LastError,MapScores;
        private static int Number(XElement e,string key,int fallback=0){XAttribute a=e.Attribute(key);return a==null?fallback:Int32.Parse(a.Value,CultureInfo.InvariantCulture);}
        public ReferenceClueReader(string assets)
        {
            foreach(string row in File.ReadAllLines(Path.Combine(assets,"compass","positions.tsv")))
            {
                string[] cols=row.Split('\t');int x,y;
                if(cols.Length>=3&&cols[0].IndexOf("lodestone",StringComparison.OrdinalIgnoreCase)>=0&&Int32.TryParse(cols[1],out x)&&Int32.TryParse(cols[2],out y))lodestones.Add(new KeyValuePair<string,Point>(cols[0],new Point(x,y)));
            }
            foreach(XElement e in XDocument.Load(Path.Combine(assets,"data","reference-clues.xml")).Root.Elements("clue"))
            {
                var card=new MasterClue{Id=Number(e,"id"),Tier=(string)e.Attribute("tier"),Reference=true,Location=(string)e.Attribute("heading"),Emote=(string)e.Attribute("action"),Travel=(string)e.Element("details"),Equipment=new string[0],Floor=Number(e,"level"),Area=new Rectangle(Number(e,"x"),Number(e,"y"),Number(e,"width"),Number(e,"height"))};
                var c=new ReferenceClue{Card=card,Type=(string)e.Attribute("type"),Text=e.Elements("text").Select(t=>t.Value).ToArray(),Anagrams=e.Elements("anagram").Select(t=>t.Value).ToArray()};Clues.Add(c);
                string file=Path.Combine(assets,"reference-maps",card.Id+".png");
                if(c.Type=="map"&&File.Exists(file))using(Bitmap image=new Bitmap(file))
                {var p=new Pixels(image);Rectangle paper=Papers(p,false).FirstOrDefault();if(!paper.IsEmpty){MapSignature s=MapSignature.Read(p,paper);if(s!=null)maps[card.Id]=s;}}
                string variant=Path.Combine(assets,"reference-map-variants",card.Id+".png");
                if(c.Type=="map"&&File.Exists(variant))using(Bitmap image=new Bitmap(variant))
                {MapSignature s=MapSignature.Read(new Pixels(image),new Rectangle(Point.Empty,image.Size));if(s!=null)variants[card.Id]=s;}
            }
        }
        private static List<Rectangle> Papers(Pixels p,bool trim=true)
        {
            var papers=ParagraphVision.FindPapers(p);
            for(int i=0;i<papers.Count;i++)
            {
                Rectangle r=papers[i];int top=r.Top,bottom=r.Bottom;
                while(top>0&&r.Bottom-top<r.Width&&PaperSides(p,r,top-1))top--;
                while(bottom<p.Height&&bottom-top<r.Width&&PaperSides(p,r,bottom))bottom++;
                papers[i]=Rectangle.FromLTRB(r.Left,top,r.Right,bottom);
            }
            for(int i=0;i<papers.Count;i++)for(int j=i+1;j<papers.Count;j++)
            {Rectangle a=papers[i],b=papers[j];if(Math.Abs(a.Left-b.Left)<20&&Math.Abs(a.Right-b.Right)<20&&Math.Max(a.Top,b.Top)-Math.Min(a.Bottom,b.Bottom)<Math.Min(a.Width,b.Width)/2){papers[i]=Rectangle.Union(a,b);papers.RemoveAt(j--);}}
            var candidates=new List<Rectangle>();
            foreach(Rectangle r in papers.OrderByDescending(box=>box.Width*box.Height))
            {
                candidates.Add(r);
                if(!trim)continue;
                int left=r.Left,right=r.Right;
                while(left<right-70&&!PaperColumn(p,r,left))left++;
                while(right>left+70&&!PaperColumn(p,r,right-1))right--;
                if(right-left>r.Width*.68&&(left!=r.Left||right!=r.Right))candidates.Add(Rectangle.FromLTRB(left,r.Top,right,r.Bottom));
            }
            return candidates;
        }
        private static bool PaperColumn(Pixels p,Rectangle r,int x)
        {
            int good=0;
            for(int i=1;i<12;i++){Color c=p.Get(x,r.Top+r.Height*i/12);if(c.R>150&&c.G>105&&c.B>55&&c.R-c.G>3&&c.R-c.G<95&&c.G-c.B>10&&c.G-c.B<105)good++;}
            return good>=8;
        }
        private static bool PaperSides(Pixels p,Rectangle r,int y)
        {
            int good=0;foreach(double f in new[]{0.06,0.10,0.90,0.94})
            {Color c=p.Get(r.Left+(int)(r.Width*f),y);if(c.R>155&&c.G>115&&c.B>65&&c.R-c.G>5&&c.R-c.G<90&&c.G-c.B>12&&c.G-c.B<100)good++;}
            return good>=3;
        }
        public static string Normalize(string text)
        {return Regex.Replace((text??"").ToLowerInvariant().Replace('\u2019','\''),"[^a-z0-9]+"," ").Trim();}
        internal static double Similarity(string a,string b)
        {
            if(a==b)return 1;if(a.Length==0||b.Length==0)return 0;
            if(Math.Abs(a.Length-b.Length)>Math.Max(a.Length,b.Length)*0.25)return 0;
            int[] row=Enumerable.Range(0,b.Length+1).ToArray();
            for(int i=1;i<=a.Length;i++){int before=row[0];row[0]=i;for(int j=1;j<=b.Length;j++){int old=row[j];row[j]=Math.Min(Math.Min(row[j]+1,row[j-1]+1),before+(a[i-1]==b[j-1]?0:1));before=old;}}
            return 1-row[b.Length]/(double)Math.Max(a.Length,b.Length);
        }
        public ReferenceClue MatchText(string raw)
        {
            string text=Normalize(raw);if(text.Length<8)return null;
            double best=0,runner=0;ReferenceClue winner=null;
            foreach(ReferenceClue c in Clues)
            {
                if(c.Type=="map")continue;
                double score=c.Text.Max(t=>Similarity(text,Normalize(t)));
                if(c.Type=="coordinates")
                {
                    // All four numbers and all directions must agree exactly.
                    string nums=String.Join(",",Regex.Matches(text,@"\d+|north|south|east|west").Cast<Match>().Select(m=>m.Value.TrimStart('0').PadLeft(1,'0')));
                    bool exact=c.Text.Any(t=>nums==String.Join(",",Regex.Matches(Normalize(t),@"\d+|north|south|east|west").Cast<Match>().Select(m=>m.Value.TrimStart('0').PadLeft(1,'0'))));
                    if(!exact)score=0;
                }
                if(c.Type=="anagram")
                {
                    if(!text.Contains("anagram")||!c.Anagrams.Any(a=>text.EndsWith(Normalize(a))))score=0;
                }
                if(score>best){runner=best;best=score;winner=c;}else runner=Math.Max(runner,score);
            }
            if(winner!=null&&best>=(text.Length<18?1:text.Length<35?0.96:0.90)&&best-runner>=0.035)return winner;
            Match coordinate=Coordinates.Match(text);
            if(!coordinate.Success||lodestones.Count==0)return null;
            int lat=Int32.Parse(coordinate.Groups[1].Value),lm=Int32.Parse(coordinate.Groups[2].Value);
            int lon=Int32.Parse(coordinate.Groups[4].Value),om=Int32.Parse(coordinate.Groups[5].Value);
            if(lm>=60||om>=60||lat>60||lon>60)return null;
            int x=2440+(coordinate.Groups[6].Value=="east"?1:-1)*(32*lon+(int)Math.Ceiling(om*8/15.0));
            int y=3161+(coordinate.Groups[3].Value=="north"?1:-1)*(32*lat+(int)Math.Ceiling(lm*8/15.0));
            if(x<2032||x>4079||y<2544||y>4079)return null;
            var start=lodestones.OrderBy(l=>Math.Pow(l.Value.X-x,2)+Math.Pow(l.Value.Y-y,2)).First();
            string dir=new[]{"east","north-east","north","north-west","west","south-west","south","south-east"}[((int)Math.Round(Math.Atan2(y-start.Value.Y,x-start.Value.X)*4/Math.PI)+8)%8];
            return new ReferenceClue{Type="coordinates",Card=new MasterClue{Id=-(x*8192+y+1),Tier="coordinate",Reference=true,Equipment=new string[0],Emote="Dig",Area=new Rectangle(x,y,1,1),Location="Dig "+dir+" of "+start.Key,Travel="Start at the "+start.Key+". The dig spot lies "+dir+" of the lodestone. Follow the pin on the surface map.\n\nCalculated tile "+x+", "+y+"."}};
        }
        private DetectedPuzzle Result(ReferenceClue clue,Rectangle paper)
        {return clue==null?null:new DetectedPuzzle{Kind=PuzzleKind.EmoteClue,Master=clue.Card,Bounds=paper,Scale=1,Confidence=0.96,Message=clue.Card.Tier+" "+clue.Type};}
        public DetectedPuzzle Detect(Bitmap image,Pixels pixels)
        {
            foreach(Rectangle paper in Papers(pixels).Take(3))
            {
                SearchCancellation.Check();ulong hash=pixels.Hash(paper);
                if(hasCache&&cached!=null&&cachedSize==image.Size&&cachedPaper==paper&&cachedHash==hash)return Result(cached,paper);
                ReferenceClue known;if(recent.TryGetValue(hash,out known)){if(known!=null)return Result(known,paper);continue;}
                ReferenceClue found=null;
                MapSignature signature=MapSignature.Read(pixels,paper);
                if(signature!=null)
                {
                    var ranked=maps.Concat(variants).GroupBy(m=>m.Key).Select(group=>new{Id=group.Key,Score=group.Max(m=>m.Value.Compare(signature))}).OrderByDescending(v=>v.Score).Take(2).ToArray();
                    MapScores=paper+" "+signature.Info+" "+String.Join(" / ",ranked.Select(v=>v.Id+":"+v.Score.ToString("0.000")));
                    if(ranked.Length>0&&ranked[0].Score>=0.83&&(ranked.Length<2||ranked[0].Score-ranked[1].Score>=0.07))found=Clues.First(c=>c.Card.Id==ranked[0].Id);
                }
                if(found==null)
                {
                    if(Progress!=null)Progress("Reading clue...");
                    // Upscale the parchment, retaining antialiasing and real ink.
                    double scale=Math.Min(2,2000.0/Math.Max(paper.Width,paper.Height));
                    using(Bitmap crop=Pixels.Crop(image,paper,new Size((int)(paper.Width*scale),(int)(paper.Height*scale))))
                    {LastText=ocr.Read(crop);LastError=ocr.Unavailable;found=MatchText(LastText);}
                }
                cachedHash=hash;cachedSize=image.Size;cachedPaper=paper;cached=found;hasCache=true;
                recent[hash]=found;recentOrder.Enqueue(hash);while(recentOrder.Count>24)recent.Remove(recentOrder.Dequeue());
                if(found!=null)return Result(found,paper);
            }
            return null;
        }
    }
    internal sealed class MapSignature
    {
        private double[] values;private double aspect; public string Info;
        public static MapSignature Read(Pixels p,Rectangle paper)
        {
            int w=paper.Width,h=paper.Height,left=w,right=0,top=h,bottom=0,count=0;
            bool[] ink=new bool[w*h];
            for(int y=h/9;y<h-h/9;y++)for(int x=w/9;x<w-w/9;x++)
            {Color c=p.Get(paper.X+x,paper.Y+y);if(c.R<145&&c.R>30&&c.G<133&&c.B<110&&c.R>=c.G&&c.R-c.G<55&&c.G>=c.B){ink[y*w+x]=true;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);count++;}}
            if(count<250||right-left<35||bottom-top<35)return null;
            int width=right-left+1,height=bottom-top+1;var v=new double[48*36];
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)if(ink[y*w+x])v[Math.Min(35,(y-top)*36/height)*48+Math.Min(47,(x-left)*48/width)]++;
            double cell=width*height/(48.0*36);for(int i=0;i<v.Length;i++)v[i]=Math.Min(1,v[i]/Math.Max(1,cell));
            var smooth=new double[v.Length];for(int y=1;y<35;y++)for(int x=1;x<47;x++)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)smooth[y*48+x]+=v[(y+dy)*48+x+dx]*(dx==0?2:1)*(dy==0?2:1)/16;
            v=smooth;
            return new MapSignature{values=v,aspect=width/(double)height,Info=left+","+top+":"+width+"x"+height};
        }
        public double Compare(MapSignature b)
        {
            if(Math.Abs(Math.Log(aspect/b.aspect))>0.16)return 0;
            double best=0;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {double dot=0,aa=0,bb=0;for(int y=1;y<35;y++)for(int x=1;x<47;x++){double a=values[y*48+x],v=b.values[(y+dy)*48+x+dx];dot+=a*v;aa+=a*a;bb+=v*v;}best=Math.Max(best,dot/Math.Max(1e-9,Math.Sqrt(aa*bb)));}
            return best;
        }
    }
}
