using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Text.RegularExpressions;
namespace KavrosClueHelper
{
    public sealed class ScanRegion
    {
        public int Id,BaseRange,Level;
        public string Name,Route;
        public string Tier="master";
        public List<PointF> Spots;
        public List<int> SpotLevels;
        public string[] Aliases;
        public int[] Levels {get{return SpotLevels==null?new[]{Level}:SpotLevels.Distinct().OrderBy(v=>v).ToArray();}}
        public string AssetKey {get{return Tier=="elite"?Id+"-"+Level:Id.ToString();}}
        public RectangleF MapBounds;
    }
    public sealed class ScanReading
    {
        public ScanRegion Region;
        public bool DifferentLevel;
    }
    public sealed class ScanVision
    {
        public Action<string> Progress;
        private sealed class Model {public ScanRegion Region;public float Scale;public ImagePattern Title,Level;public Size Source;public Point TitleOrigin;public Point[] Empty;}
        private readonly List<Model> models=new List<Model>();
        private readonly NativeClueOcr ocr=new NativeClueOcr();
        private readonly ScanTitleReader titles;
        private Rectangle lastOcrArea;private ulong lastOcrHash;private DetectedPuzzle lastOcr;private bool ocrCached;
        public string LastText;
        public readonly List<ScanRegion> Regions=new List<ScanRegion>();
        public ScanVision(string assets)
        {
            titles=new ScanTitleReader(assets);
            string dir=Path.Combine(assets,"scan");
            foreach(int id in new[]{368,371})
            {
                string[] map=File.ReadAllText(Path.Combine(dir,id+"-map.tsv")).Trim().Split(new[]{'\t'});
                float[] box=map.Select(v=>Single.Parse(v,CultureInfo.InvariantCulture)).ToArray();
                var region=new ScanRegion{Id=id,Name=id==368?"Prifddinas":"Heart of Gielinor",BaseRange=id==368?30:49,Level=1,MapBounds=RectangleF.FromLTRB(box[0],box[1],box[2],box[3]),
                    Spots=File.ReadAllLines(Path.Combine(dir,id+".tsv")).Select(l=>l.Split(new[]{'\t'})).Select(p=>new PointF(Int32.Parse(p[0]),Int32.Parse(p[1]))).ToList()};
                Regions.Add(region);
                int[] meta=File.ReadAllText(Path.Combine(dir,id+"-title.tsv")).Split(new[]{'\t'}).Select(Int32.Parse).ToArray();
                foreach(float scale in new[]{1F,0.8F,1.05F,0.95F,1.25F,1.5F,2F,0.6F})
                using(Bitmap original=new Bitmap(Path.Combine(dir,id+"-title.png")))
                using(Bitmap image=Pixels.Crop(original,new Rectangle(Point.Empty,original.Size),new Size((int)Math.Round(original.Width*scale),(int)Math.Round(original.Height*scale))))
                using(Bitmap oldLevel=new Bitmap(Path.Combine(dir,id+"-level.png")))
                using(Bitmap level=Pixels.Crop(oldLevel,new Rectangle(Point.Empty,oldLevel.Size),new Size((int)Math.Round(oldLevel.Width*scale),(int)Math.Round(oldLevel.Height*scale))))
                {
                    Pixels mask=new Pixels(image);var empty=new List<Point>();
                    for(int yy=0;yy<image.Height;yy+=2)for(int xx=0;xx<image.Width;xx+=2)if(mask.Get(xx,yy).A<5)empty.Add(new Point(xx,yy));
                    models.Add(new Model{Region=region,Scale=scale,Title=new ImagePattern(image),Level=new ImagePattern(level),Source=new Size(meta[2],meta[3]),TitleOrigin=new Point(meta[0],meta[1]),Empty=empty.Where((q,i)=>i%Math.Max(1,empty.Count/24)==0).Take(24).ToArray()});
                }
            }
            string elite=Path.Combine(assets,"data","elite-scans.xml");
            if(File.Exists(elite))foreach(XElement node in XDocument.Load(elite).Root.Elements("scan"))
            {
                int id=Int32.Parse((string)node.Attribute("id"));
                var spots=node.Elements("spot").ToArray();
                var region=new ScanRegion{Id=id,Tier="elite",Name=(string)node.Attribute("name"),BaseRange=Int32.Parse((string)node.Attribute("range")),
                    MapBounds=RectangleF.FromLTRB(Single.Parse((string)node.Attribute("left"),CultureInfo.InvariantCulture),Single.Parse((string)node.Attribute("bottom"),CultureInfo.InvariantCulture),Single.Parse((string)node.Attribute("right"),CultureInfo.InvariantCulture),Single.Parse((string)node.Attribute("top"),CultureInfo.InvariantCulture)),
                    Spots=spots.Select(s=>new PointF(Int32.Parse((string)s.Attribute("x")),Int32.Parse((string)s.Attribute("y")))).ToList(),SpotLevels=spots.Select(s=>Int32.Parse((string)s.Attribute("level"))).ToList()};
                region.Level=region.Levels[0];region.Aliases=Aliases(id,region.Name);Regions.Add(region);
            }
            foreach(string line in File.ReadAllLines(Path.Combine(assets,"data","scan-routes.tsv"))){
                string[] fields=line.Split('\t');int id;
                if(fields.Length==2&&Int32.TryParse(fields[0],out id)){
                    ScanRegion region=Regions.Find(r=>r.Id==id);if(region!=null)region.Route=fields[1];
                }
            }
        }
        private static string[] Aliases(int id,string name)
        {
            var names=new Dictionary<int,string>{{36,"deep wilderness|wilderness"},{349,"desert"},{350,"isafdar|lletya"},{351,"varrock"},{352,"ardougne"},{353,"keldagrim"},{354,"menaphos"},{355,"piscatoris"},{356,"brimhaven dungeon"},{357,"taverley dungeon"},{358,"mos le harmless"},{359,"haunted woods"},{360,"kharazi jungle"},{361,"zanaris"},{362,"lumbridge swamp caves|caves beneath lumbridge swamp"},{363,"jatizso|neitiznot"},{364,"falador"},{365,"dorgesh kaan"},{366,"fremennik slayer dungeon|fremennik slayer dungeons"},{367,"wilderness crater|crater in the wilderness"}};
            string aliases;return (names.TryGetValue(id,out aliases)?aliases+"|":"").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries).Concat(new[]{name}).ToArray();
        }
        private static bool ContainsRegion(string text,string alias)
        {
            string[] words=text.Replace("rn","m").Split(' '),expected=ReferenceClueReader.Normalize(alias).Replace("rn","m").Split(' ');
            for(int start=0;start+expected.Length<=words.Length;start++)
            {
                bool same=true;for(int i=0;i<expected.Length;i++)
                {string a=expected[i],b=words[start+i];if(a!=b&&(a.Length<5||b.Length<5||ReferenceClueReader.Similarity(a,b)<0.82)){same=false;break;}}
                if(same)return true;
            }
            return false;
        }
        public ScanRegion MatchEliteText(string raw)
        {
            string text=ReferenceClueReader.Normalize(raw);
            text=Regex.Replace(text,@"\b([0-9il])\s+([0-9il]) paces\b",m=>m.Groups[1].Value+m.Groups[2].Value+" paces");
            text=Regex.Replace(text,@"\b[0-9il]{1,2} paces\b",m=>m.Value.Replace('i','1').Replace('l','1'));
            if(!text.Contains("scan")||!text.Contains("range")||!text.Contains("orb")||!text.Contains("paces"))return null;
            var matches=Regions.Where(r=>r.Tier=="elite"&&Regex.IsMatch(text,@"\b"+r.BaseRange+@" paces\b")&&(r.Aliases.Any(a=>ContainsRegion(text,a))||r.Id==367&&ContainsRegion(text,"wilderness volcano")||r.Id==349&&ContainsRegion(text,"east of the elid and north of nardah"))).ToArray();
            return matches.Length==1?matches[0]:null;
        }
        private ScanRegion MatchScrollTranscript(string raw)
        {
            ScanRegion exact=MatchEliteText(raw);if(exact!=null)return exact;
            string text=ReferenceClueReader.Normalize(raw);
            Match range=Regex.Match(text,@"\b([0-9il]{1,2})\s+pace[a-z]?\b");
            if(!range.Success)return null;
            int value;if(!Int32.TryParse(range.Groups[1].Value.Replace('i','1').Replace('l','1'),out value))return null;
            var matches=Regions.Where(r=>r.Tier=="elite"&&r.BaseRange==value&&(r.Aliases.Any(a=>ContainsRegion(text,a))||r.Id==367&&ContainsRegion(text,"wilderness volcano")||r.Id==349&&ContainsRegion(text,"east of the elid and north of nardah"))).ToArray();
            return matches.Length==1?matches[0]:null;
        }
        public DetectedPuzzle Find(Bitmap image,Pixels p)
        {
            // Search each title once. Overlapping scenery regions otherwise
            // repeat the same white-pixel candidates hundreds of times.
            List<Rectangle> areas=new List<Rectangle>{new Rectangle(Point.Empty,image.Size)};
            foreach(Model model in models)
            foreach(Rectangle area in areas)
            foreach(Point hit in model.Title.Find(p,area,8,35,q=>PaperAround(p,q,model)))
            {
                float s=model.Scale;Rectangle body=new Rectangle(hit.X-(int)Math.Round(model.TitleOrigin.X*s),hit.Y-(int)Math.Round(model.TitleOrigin.Y*s),(int)Math.Round(model.Source.Width*s),(int)Math.Round(model.Source.Height*s));
                body=Rectangle.Intersect(body,new Rectangle(Point.Empty,image.Size));
                if(body.Width<90||body.Height<120)continue;
                int paperPixels=0,samples=0;
                for(int yy=body.Top+body.Height/10;yy<body.Top+body.Height*9/10;yy+=4)
                for(int xx=body.Left+body.Width*17/100;xx<body.Left+body.Width*83/100;xx+=4)
                {Color c=p.Get(xx,yy);samples++;if(c.R>40&&c.G>35&&c.B>20&&c.B<165&&c.R-c.G>-10&&c.R-c.G<75&&c.G-c.B>9)paperPixels++;}
                if(paperPixels<samples*0.72)continue;
                if(Progress!=null)Progress("Reading scan clue...");
                int white=0;
                for(int y=hit.Y+(int)(25*s);y<Math.Min(body.Bottom,hit.Y+(int)(94*s));y++)
                for(int x=Math.Max(body.X,hit.X-(int)(50*s));x<Math.Min(body.Right,hit.X+(int)(135*s));x++)
                {Color c=p.Get(x,y);if(c.R>185&&c.G>185&&c.B>185&&Math.Abs(c.R-c.G)<25)white++;}
                if(white<65*s*s)continue;
                return new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=new ScanReading{Region=model.Region,DifferentLevel=model.Level.Find(p,body,1,35).Count>0},Bounds=body,Scale=s,Confidence=0.95,Message="Scan clue"};
            }
            return ReadElite(image,p);
        }
        internal DetectedPuzzle FindNativeTitle(Pixels p)
        {
            foreach(Rectangle area in ScrollAreas.Find(p,true)){
                ScanReading found=titles.Read(p,area,Regions);
                if(found!=null)return new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=found,Bounds=area,Scale=1,Confidence=.97};
            }
            return null;
        }
        private DetectedPuzzle ReadElite(Bitmap image,Pixels p)
        {
            if(!Regions.Any(r=>r.Tier=="elite"))return null;
            foreach(Rectangle area in ScrollAreas.Find(p,true).Take(3))
            {
                ulong hash=p.Hash(area);if(ocrCached&&hash==lastOcrHash&&area==lastOcrArea&&lastOcr!=null)return lastOcr;
                ScanReading title=titles.Read(p,area,Regions);
                if(title!=null)return new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=title,Bounds=area,Scale=1,Confidence=.97};
                int bright=0;for(int y=area.Top;y<area.Bottom;y+=2)for(int x=area.Left;x<area.Right;x+=2){Color c=p.Get(x,y);if(c.R>190&&c.G>190&&c.B>190&&Math.Abs(c.R-c.B)<35)bright++;}
                if(bright<30)continue;
                if(Progress!=null)Progress("Reading scan clue...");
                double scale=Math.Min(1,1600.0/Math.Max(area.Width,area.Height));
                using(Bitmap crop=Pixels.Crop(image,area,new Size((int)(area.Width*scale),(int)(area.Height*scale))))
                {
                    LastText=ocr.Read(crop);string originalText=LastText;ScanRegion originalMatch=MatchScrollTranscript(LastText);
                    if(originalMatch!=null)
                    {lastOcrHash=hash;lastOcrArea=area;ocrCached=true;lastOcr=new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=new ScanReading{Region=originalMatch,DifferentLevel=ReferenceClueReader.Normalize(LastText).Contains("different level")},Bounds=area,Scale=1,Confidence=0.96};return lastOcr;}
                    using(Bitmap large=Pixels.Crop(crop,new Rectangle(Point.Empty,crop.Size),new Size(crop.Width*2,crop.Height*2)))
                    {
                        LastText=ocr.Read(large);ScanRegion scaledMatch=MatchScrollTranscript(LastText);
                        if(scaledMatch!=null){lastOcrHash=hash;lastOcrArea=area;ocrCached=true;lastOcr=new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=new ScanReading{Region=scaledMatch,DifferentLevel=ReferenceClueReader.Normalize(LastText).Contains("different level")},Bounds=area,Scale=1,Confidence=0.96};return lastOcr;}
                        originalText+=" UPSCALED: "+LastText;
                    }
                    // Scan scrolls use white text on tan paper. Isolate the ink
                    // before OCR so the parchment texture cannot become letters.
                    var cp=new Pixels(crop);using(Graphics g=Graphics.FromImage(crop))g.Clear(Color.White);
                    for(int y=0;y<crop.Height;y++)for(int x=0;x<crop.Width;x++){Color c=cp.Get(x,y);if(c.R>185&&c.G>185&&c.B>185&&Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<45)crop.SetPixel(x,y,Color.Black);}
                    LastText=ocr.Read(crop);string maskedText=LastText;ScanRegion region=MatchScrollTranscript(LastText);
                    if(region==null)LastText="ORIGINAL: "+originalText+" MASK: "+maskedText;lastOcrHash=hash;ocrCached=true;lastOcr=region==null?null:new DetectedPuzzle{Kind=PuzzleKind.Scan,Scan=new ScanReading{Region=region,DifferentLevel=ReferenceClueReader.Normalize(LastText).Contains("different level")},Bounds=area,Scale=1,Confidence=0.96};
                    if(lastOcr!=null)return lastOcr;
                }
            }
            return null;
        }
        private static bool PaperAround(Pixels p,Point q,Model model)
        {
            float s=model.Scale;int left=q.X-(int)(model.TitleOrigin.X*s),top=q.Y-(int)(model.TitleOrigin.Y*s),good=0;
            Color above=p.Get(q.X+model.Title.Size.Width/2,q.Y-Math.Max(2,(int)Math.Round(5*s)));
            if(above.B>=170||above.G-above.B<8||above.R<70)return false;
            foreach(Point sample in new[]{new Point(65,55),new Point(model.Source.Width-65,55),new Point(75,model.Source.Height-65),new Point(model.Source.Width-75,model.Source.Height-65),new Point(model.Source.Width/2,45),new Point(model.Source.Width/2,model.Source.Height-45)})
            {
                Color c=p.Get(left+(int)(sample.X*s),top+(int)(sample.Y*s));
                if(c.R>90&&c.G>75&&c.B>35&&c.R-c.G>-8&&c.R-c.G<75&&c.G-c.B>10)good++;
            }
            if(good<3)return false;
            int wrong=0;
            foreach(Point empty in model.Empty){Color c=p.Get(q.X+empty.X,q.Y+empty.Y);if(c.R>185&&c.G>185&&c.B>185&&++wrong>model.Empty.Length/4)return false;}
            return true;
        }
    }
    public sealed class ScanSession
    {
        public ScanRegion Region;
        public PointF? Position;
        public PointF? LastKnown;
        public bool Meerkat,DifferentLevel;
        public bool AutoTracking=true,PositionTracked;
        public string TrackingStatus="Looking for your position on the minimap...";
        private MinimapMatch pending;
        private DateTime lastMatch;
        private DateTime lastConfirmed;
        private int confirmations;
        public int Range {get{return Region==null?0:Region.BaseRange+(Meerkat?5:0);}}
        public void Observe(ScanReading reading){if(Region==null||Region.Id!=reading.Region.Id){Position=null;LastKnown=null;PositionTracked=false;pending=null;TrackingStatus="Looking for your position on the minimap...";}Region=reading.Region;DifferentLevel=reading.DifferentLevel;}
        public bool InRange(PointF spot,int factor=1){return (!AutoTracking||PositionTracked)&&!DifferentLevel&&Position.HasValue&&Math.Max(Math.Abs(spot.X-Position.Value.X),Math.Abs(spot.Y-Position.Value.Y))<=Range*factor;}
        public void Clear(){Region=null;Position=null;LastKnown=null;DifferentLevel=false;PositionTracked=false;pending=null;}
        public void SetAutomatic(bool enabled){AutoTracking=enabled;Position=null;LastKnown=null;PositionTracked=false;pending=null;TrackingStatus=enabled?"Looking for your position on the minimap...":"Right-click your location on the map.";}
        public void SetManual(PointF position){SetAutomatic(false);Position=position;}
        public void PrepareRecheck(){if(LastKnown.HasValue)return;Position=null;PositionTracked=false;pending=null;TrackingStatus="Minimap found - checking your current position...";}
        public void ApplyMatch(MinimapMatch match,DateTime now)
        {
            if(!AutoTracking)return;
            if(match==null)
            {
                confirmations=0;
                PositionTracked=false;
                if((now-lastMatch).TotalMilliseconds>650)Position=null;
                TrackingStatus=LastKnown.HasValue?"Grey marker shows your last position. Rechecking minimap.":"Looking for your minimap. You can follow the marked spots now.";
                return;
            }
            bool consistent=match!=null&&pending!=null&&(now-lastMatch).TotalSeconds<2&&CompassMath.Distance(match.Position,pending.Position)<=18;
            confirmations=consistent?confirmations+1:1;consistent=consistent&&confirmations>=2;
            pending=match;lastMatch=now;
            PositionTracked=consistent;Position=consistent?(PointF?)match.Position:null;
            if(consistent){LastKnown=match.Position;lastConfirmed=now;}
            TrackingStatus=consistent?"Following your minimap automatically.":"Position found - confirming the next frame...";
        }
        public void Expire(DateTime now)
        {if(AutoTracking&&Position.HasValue&&(now-lastMatch).TotalMilliseconds>1500){Position=null;PositionTracked=false;pending=null;TrackingStatus="Position lost. Looking for the minimap...";}}

        public string Summary {get{return Region==null?"":Region.Name+" | "+Range+" paces"+(Meerkat?" (meerkat)":"")+" | Dig locations: level "+Region.Level+"\n"+(AutoTracking?TrackingStatus:Position.HasValue?"Manual position: "+Math.Round(Position.Value.X)+", "+Math.Round(Position.Value.Y)+". Enable Auto follow to track movement.":"Right-click your current location on the map.")+(AutoTracking&&!PositionTracked&&LastKnown.HasValue?"\nGrey marker: last confirmed "+Math.Max(0,(int)(DateTime.UtcNow-lastConfirmed).TotalSeconds)+"s ago.":"");}}

    }
    public sealed class ScanMap : Control
    {
        private readonly string assets;private Bitmap background;private int loaded;
        private float zoom=1;
        private PointF pan;
        private Point? drag;
        public readonly ScanSession Session;
        public event Action PositionChanged;
        public ScanMap(string directory,ScanSession session){assets=directory;Session=session;DoubleBuffered=true;TabStop=true;}
        private RectangleF View
        {
            get {
                RectangleF r=Session.Region.MapBounds;
                float aspect=Math.Max(1,Width)/(float)Math.Max(1,Height);
                float w=Math.Max(r.Width,r.Height*aspect)/zoom,h=w/aspect;
                float limitX=Math.Max(0,(r.Width-w)/2),limitY=Math.Max(0,(r.Height-h)/2);
                pan.X=Math.Max(-limitX,Math.Min(limitX,pan.X));
                pan.Y=Math.Max(-limitY,Math.Min(limitY,pan.Y));
                return new RectangleF(r.Left+(r.Width-w)/2+pan.X,r.Top+(r.Height-h)/2+pan.Y,w,h);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);if(Session.Region==null)return;Focus();
            if(e.Button==MouseButtons.Left){drag=e.Location;Capture=true;}
            if(e.Button==MouseButtons.Right){Session.SetManual(World(e.Location));if(PositionChanged!=null)PositionChanged();Invalidate();}
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);if(!drag.HasValue||Session.Region==null)return;
            RectangleF view=View;
            pan.X-=(e.X-drag.Value.X)*view.Width/Math.Max(1,Width);
            pan.Y+=(e.Y-drag.Value.Y)*view.Height/Math.Max(1,Height);
            drag=e.Location;Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);drag=null;Capture=false;}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)drag=null;}
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);if(Session.Region==null)return;
            PointF anchor=World(e.Location);
            zoom=(float)Math.Max(1,Math.Min(8,zoom*Math.Pow(1.35,e.Delta/120.0)));
            PointF after=World(e.Location);pan.X+=anchor.X-after.X;pan.Y+=anchor.Y-after.Y;Invalidate();
        }
        private PointF ScreenPoint(PointF p){RectangleF r=View;return new PointF((p.X-r.Left)/r.Width*Width,Height-(p.Y-r.Top)/r.Height*Height);}
        private PointF World(Point p){RectangleF r=View;return new PointF((float)Math.Round(r.Left+p.X*r.Width/Width),(float)Math.Round(r.Top+(Height-p.Y)*r.Height/Height));}
        public void RefreshMap(){Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;g.Clear(Color.FromArgb(18,30,38));if(Session.Region==null)return;
            ScanRegion region=Session.Region;
            int mapId=region.Id*4+region.Level;
            if(loaded!=mapId){if(background!=null)background.Dispose();using(Bitmap loadedImage=new Bitmap(Path.Combine(assets,"scan",region.AssetKey+".png")))background=Pixels.Crop(loadedImage,new Rectangle(Point.Empty,loadedImage.Size),loadedImage.Size);loaded=mapId;zoom=1;pan=PointF.Empty;drag=null;}
            PointF tl=ScreenPoint(new PointF(region.MapBounds.Left,region.MapBounds.Bottom)),br=ScreenPoint(new PointF(region.MapBounds.Right,region.MapBounds.Top));
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(background,RectangleF.FromLTRB(tl.X,tl.Y,br.X,br.Y));
            PointF? displayed=Session.Position??(Session.AutoTracking?Session.LastKnown:null);
            bool oldPosition=Session.AutoTracking&&!Session.PositionTracked;
            if(displayed.HasValue)
            {
                PointF centre=displayed.Value;
                foreach(int factor in new[]{2,1})
                {
                    PointF a=ScreenPoint(new PointF(centre.X-Session.Range*factor,centre.Y+Session.Range*factor)),b=ScreenPoint(new PointF(centre.X+Session.Range*factor,centre.Y-Session.Range*factor));
                    RectangleF box=RectangleF.FromLTRB(a.X,a.Y,b.X,b.Y);
                    using(Pen pen=new Pen(Session.AutoTracking&&!Session.PositionTracked?Color.Silver:factor==1?Color.Lime:Color.Orange,2)){if(factor==2||Session.AutoTracking&&!Session.PositionTracked)pen.DashStyle=DashStyle.Dash;g.DrawRectangle(pen,box.X,box.Y,box.Width,box.Height);}
                    if(factor==1)using(Brush fill=new SolidBrush(oldPosition?Color.FromArgb(20,170,170,170):Color.FromArgb(30,40,255,100)))g.FillRectangle(fill,box);
                }

            }
            for(int i=0;i<region.Spots.Count;i++){if(region.SpotLevels!=null&&region.SpotLevels[i]!=region.Level)continue;PointF spot=region.Spots[i],p=ScreenPoint(spot);g.FillEllipse(Brushes.Black,p.X-5,p.Y-5,10,10);g.FillEllipse(Session.InRange(spot)?Brushes.Lime:Session.InRange(spot,2)?Brushes.Orange:Brushes.Cyan,p.X-3,p.Y-3,6,6);}
            // Keep the player above candidate markers, including at a dig spot.
            if(displayed.HasValue){PointF p=ScreenPoint(displayed.Value);g.FillEllipse(Brushes.Black,p.X-7,p.Y-7,14,14);g.FillEllipse(oldPosition?Brushes.Silver:Brushes.Yellow,p.X-4,p.Y-4,8,8);g.DrawEllipse(Pens.White,p.X-6,p.Y-6,12,12);}
            TextRenderer.DrawText(g,"RuneApps / Jagex  |  Wheel to zoom  |  Drag to pan",Font,new Rectangle(3,Height-19,Width-6,18),Color.White,Color.FromArgb(18,25,32),TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g,"N",Font,new Point(Width-20,3),Color.White,Color.Black);
        }
        protected override void Dispose(bool disposing){if(disposing&&background!=null)background.Dispose();base.Dispose(disposing);}
    }
}

