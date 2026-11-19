using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KavrosClueHelper
{
    public sealed class MasterClue
    {
        public int Id, Floor;
        public bool Reference;
        public string Tier="master";
        public string Location, Emote, Travel;
        public string[] Equipment;
        public Rectangle Area;
        public string MapFile { get { return Id + ".png"; } }
    }
    public static class MasterClues
    {
        public static readonly MasterClue[] All = {
            new MasterClue { Id=236, Location="Green Ghost inn, Port Phasmatys", Emote="Laugh", Equipment=new[]{"Iban's staff", "Ghostly cloak", "Black cavalier"}, Area=new Rectangle(3671,3489,11,11), Travel="Fastest: Fairy ring ALQ (Haunted Woods) (portable fairy ring required to be the fastest), then run east to Port Phasmatys' west gate, then on to the inn. Alternatively: Ectophial (Ectofuntus, Port Phasmatys), run south-east to the Green Ghost inn. Third option: Amulet of farming > Herb Patches > 3. Haunted Woods Herb Patch, run south-east to Port Phasmatys' west gate, then on to the inn. Portable fairy ring can be made in ;;ig. An unlimited version can be bought for 5 Kavros dollars. Final step: Laugh. Wear: Iban's staff, Ghostly cloak, Black cavalier." },
            new MasterClue { Id=240, Location="Charm sprite hunter area", Emote="Bow or curtsy", Equipment=new[]{"Enhanced yaktwee stick", "Dagon'hai hat", "Amulet of ranging"}, Area=new Rectangle(2391,3362,26,22), Travel="Fastest: Ctrl+T > Skilling > Agility > 1/85 - Gnome Course, then run south-west to the charm sprite hunter area, leaving the stronghold by its main gate. Alternatively: Traveller's necklace > The Outpost, run north-west to the charm sprite hunter area. Third option: Amulet of farming > Tree Patches > 5. Gnome Stronghold Tree Patch, run south-west to the charm sprite hunter area, leaving the stronghold by its main gate. Final step: Bow or curtsy. Wear: Enhanced yaktwee stick, Dagon'hai hat, Amulet of ranging." },
            new MasterClue { Id=234, Location="Inside the Slayer Tower", Emote="Headbang", Equipment=new[]{"Imp-hide hood", "Prifddinian musician's robe top", "Amulet of magic"}, Area=new Rectangle(3400,3541,47,34), Travel="Fastest: Ctrl+T > Skilling > Slayer > Slayer Tower, then run north to the Slayer Tower door, then go in on the ground floor. Alternatively: Fairy ring CKS (Canifis), run north to the Slayer Tower entrance. Third option: Canifis lodestone, run west to the Slayer Tower entrance. Portable fairy ring can be made in ;;ig. An unlimited version can be bought for 5 Kavros dollars. Final step: Headbang. Wear: Imp-hide hood, Prifddinian musician's robe top, Amulet of magic." },
            new MasterClue { Id=233, Location="Sulphur pit, TzHaar City", Emote="Cheer", Equipment=new[]{"Fire cape", "Toktz-ket-xil", "Spork"}, Area=new Rectangle(4630,5104,14,14), Travel="Fastest: Fairy ring BLP (TzHaar) (portable fairy ring required to be the fastest), then go to the sulphur pit highlighted on the card map (the ring drops you inside TzHaar City). Alternatively: Karamja lodestone, run east to Karamja Volcano, climb down into the volcano, go south through the dungeon to the TzHaar City entrance and enter, then go to the sulphur pit. Third option: 120 Dungeoneering cape > 4. Karamja Volcano lesser demon dungeon, run east to the TzHaar City entrance, enter TzHaar City, then go to the sulphur pit. Portable fairy ring can be made in ;;ig. An unlimited version can be bought for 5 Kavros dollars. Final step: Cheer. Wear: Fire cape, Toktz-ket-xil, Spork." },
            new MasterClue { Id=242, Location="Lumbridge Fishing Supplies shop", Emote="Shrug", Equipment=new[]{"Demon slayer gloves", "A boater", "Cape of legends"}, Area=new Rectangle(3194,3251,5,8), Travel="Fastest: Ctrl+T > Skilling > Farming > Lumbridge Tree, then run north to the fishing shop by the River Lum. Alternatively: Modern spellbook > Lumbridge Teleport, run north-west to the fishing shop. Third option: Lumbridge lodestone, run north-west to the fishing shop. Final step: Shrug. Wear: Demon slayer gloves, A boater, Cape of legends." },
            new MasterClue { Id=246, Location="Max Guild Garden, Prifddinas", Emote="Salute", Equipment=new[]{"Nothing equipped - remove every worn item"}, Area=new Rectangle(2268,3318,17,18), Floor=1, Travel="Fastest: Prifddinas lodestone, then run south-east to the Max Guild garden in south-east Prifddinas (upper level, outside the guild building). Alternatively: 120 Slayer cape > 8. Morvran, run east to the Max Guild garden (upper level). Final step: Salute. Wear: Nothing equipped - remove every worn item." },
            new MasterClue { Id=239, Location="Edgeville Monastery garden", Emote="Jig", Equipment=new[]{"Holy cithara", "Saradomin's murmur", "Ring of devotion"}, Area=new Rectangle(3046,3501,12,8), Travel="Fastest: Ctrl+T > Locations > Edgeville Monastery, then run north to the monastery's ground-floor garden. Alternatively: Edgeville lodestone, run west to the monastery garden. Third option: Amulet of glory > Edgeville, run west to the monastery garden. Final step: Jig. Wear: Holy cithara, Saradomin's murmur, Ring of devotion." },
            new MasterClue { Id=244, Location="Inside the Invention Guild", Emote="Have an idea", Equipment=new[]{"Lab coat top", "Lab coat legs", "Staff of light"}, Area=new Rectangle(6160,1039,18,36), Travel="Fastest: ;;ig (Invention Guild), then enter the Invention Guild; the emote spot is the interior shown on the card map. Alternatively: Ctrl+T > Skilling > Invention > Invention Guild, enter the guild and go to the interior shown on the card map. Third option: Skills necklace > Invention Guild, walk a few steps north-east to the guild entrance, then go inside. Final step: Have an idea. Wear: Lab coat top, Lab coat legs, Staff of light." }
        };
        public static MasterClue ById(int id) { return All.First(c => c.Id == id); }
    }

    public static class MasterClueDrawing
    {
        public static void Draw(Graphics g, Rectangle bounds, MasterClue clue, Image map)
        {Draw(g,bounds,clue,map,1,PointF.Empty);}
        public static void Draw(Graphics g, Rectangle bounds, MasterClue clue, Image map,float zoom,PointF pan)
        {
            g.Clear(KavrosTheme.Panel);
            float sx=bounds.Width/460F, sy=bounds.Height/471F;
            System.Drawing.Drawing2D.GraphicsState state=g.Save();
            g.TranslateTransform(bounds.X,bounds.Y); g.ScaleTransform(sx,sy);
            using(Font heading=(Font)KavrosTheme.DisplayPixels(14F).Clone())
            using(Font body=new Font("Segoe UI",13F,FontStyle.Regular,GraphicsUnit.Pixel))
            using(Font small=new Font("Segoe UI",11F,FontStyle.Regular,GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g,clue.Location,heading,new Rectangle(10,2,440,27),Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
                Rectangle viewport=new Rectangle(10,34,440,234);
                if(map!=null)
                {
                    float sourceWidth=map.Width/zoom,sourceHeight=sourceWidth*viewport.Height/viewport.Width;
                    RectangleF source=new RectangleF((map.Width-sourceWidth)/2+pan.X*4,(map.Height-sourceHeight)/2-pan.Y*4,sourceWidth,sourceHeight);
                    var mapState=g.Save();g.SetClip(viewport);
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.DrawImage(map,viewport,source,GraphicsUnit.Pixel);
                    float factor=viewport.Width/source.Width;
                    RectangleF area=new RectangleF(viewport.Left+(256-clue.Area.Width*2-source.Left)*factor,viewport.Top+(256-clue.Area.Height*2-source.Top)*factor,clue.Area.Width*4*factor,clue.Area.Height*4*factor);
                    using(Brush fill=new SolidBrush(Color.FromArgb(65,70,255,150))) g.FillRectangle(fill,area);
                    using(Pen border=new Pen(Color.FromArgb(100,255,169),2)) g.DrawRectangle(border,area.X,area.Y,area.Width,area.Height);
                    float cx=viewport.Left+(256-source.Left)*factor, cy=viewport.Top+(256-source.Top)*factor;
                    g.FillEllipse(Brushes.Black,cx-7,cy-7,14,14); g.FillEllipse(Brushes.Gold,cx-4,cy-4,8,8);
                    g.FillRectangle(Brushes.Black,viewport.Right-29,viewport.Top+4,25,21);
                    TextRenderer.DrawText(g,"N",heading,new Rectangle(viewport.Right-26,viewport.Top+5,19,19),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPadding);
                    g.FillRectangle(Brushes.Black,viewport.Left+3,viewport.Bottom-19,145,17);
                    g.Restore(mapState);
                }
                TextRenderer.DrawText(g,"Perform the emote in the green area.",small,new Rectangle(10,272,440,18),KavrosTheme.Ok,TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g,"DO: "+clue.Emote,heading,new Rectangle(10,296,440,25),Color.FromArgb(110,227,190),TextFormatFlags.NoPadding);
                for(int i=0;i<clue.Equipment.Length;i++) TextRenderer.DrawText(g,"Wear: "+clue.Equipment[i],body,new Rectangle(10,327+i*21,440,21),Color.White,TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g,"Double agent: defeat it, then repeat the emote for Uri.",small,new Rectangle(10,395,440,21),Color.Gold,TextFormatFlags.NoPadding|TextFormatFlags.WordBreak);
                TextRenderer.DrawText(g,clue.Travel,body,new Rectangle(10,424,440,44),Color.FromArgb(172,191,212),TextFormatFlags.NoPadding|TextFormatFlags.WordBreak);
            }
            g.Restore(state);
        }
    }

    public sealed class ReferenceDetailsView : ScrollableControl
    {
        private bool measuring;
        public ReferenceDetailsView(){AutoScroll=true;DoubleBuffered=true;TabStop=true;MouseDown+=delegate{Focus();};}
        private void MeasureContent()
        {
            if(measuring)return;measuring=true;
            try{Size size=TextRenderer.MeasureText(Text,Font,new Size(Math.Max(1,ClientSize.Width-24),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);AutoScrollMinSize=new Size(0,size.Height+8);}
            finally{measuring=false;}
            Invalidate();
        }
        protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);AutoScrollPosition=Point.Empty;MeasureContent();}
        protected override void OnResize(EventArgs e){base.OnResize(e);MeasureContent();}
        protected override void OnPaint(PaintEventArgs e)
        {e.Graphics.Clear(BackColor);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(4,4+AutoScrollPosition.Y,Math.Max(1,ClientSize.Width-24),Math.Max(Height,AutoScrollMinSize.Height)),ForeColor,TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);}
    }
    public sealed class MasterClueView : Control
    {
        private MasterClue clue;
        private Bitmap map;
        private bool detailed;
        private Bitmap overview;
        private bool overviewShared;
        private bool regionalOverview;
        private float zoom=1;
        private PointF pan;
        private Point? drag;
        private string assetsPath="";
        private string mapPath="";
        public string MapPath { get { return mapPath; } }
        private float Span {get{return Math.Max(72,Math.Max(clue.Area.Width,clue.Area.Height)*3)/zoom;}}
        private Rectangle MapBox {get{return new Rectangle(8,43,Math.Max(1,Width-16),139);}}
        private readonly ReferenceDetailsView details=new ReferenceDetailsView();
        public MasterClueView()
        {
            DoubleBuffered=true;
            TabStop=true;
            MouseDown+=delegate(object sender,MouseEventArgs e){if(clue==null||!MapBox.Contains(e.Location))return;Focus();if(e.Button==MouseButtons.Left){drag=e.Location;Capture=true;}if(e.Button==MouseButtons.Right){pan=PointF.Empty;zoom=1;Invalidate();}};
            MouseMove+=delegate(object sender,MouseEventArgs e){if(!drag.HasValue||clue==null)return;float units=Span/MapBox.Width;pan.X-=(e.X-drag.Value.X)*units;pan.Y+=(e.Y-drag.Value.Y)*units;drag=e.Location;Invalidate();};
            MouseUp+=delegate{drag=null;Capture=false;};
            MouseCaptureChanged+=delegate{if(!Capture)drag=null;};
            details.BackColor=KavrosTheme.Panel;details.ForeColor=KavrosTheme.TextSoft;details.Font=new Font("Segoe UI",9F);details.Visible=false;Controls.Add(details);
            Resize+=delegate{details.SetBounds(8,202,Math.Max(1,Width-16),Math.Max(1,Height-208));};
        }
        private static string HideCoordinates(string value)
        {
            if(value==null)return "";
            var kept=new List<string>();
            foreach(string line in value.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
            {
                string trimmed=line.Trim();
                if(trimmed.StartsWith("Tile ")||trimmed.StartsWith("Coordinates:")||trimmed.StartsWith("Calculated tile "))continue;
                kept.Add(line);
            }
            while(kept.Count>0&&kept[kept.Count-1].Trim().Length==0)kept.RemoveAt(kept.Count-1);
            return String.Join("\n",kept.ToArray());
        }
        public void SetClue(MasterClue value,string assets)
        {
            if(clue==value) return;
            if(map!=null) { map.Dispose(); map=null; }
            if(overview!=null){if(!overviewShared)overview.Dispose();overview=null;}regionalOverview=false;overviewShared=false;
            clue=value;zoom=1;pan=PointF.Empty;drag=null;detailed=false;mapPath="";assetsPath=assets??"";
            if(value!=null)
            {
                string detail=Path.Combine(assets,"location-maps",value.Id+".jpg");
                detailed=File.Exists(detail);
                string file=detailed?detail:Path.Combine(assets,"compass","world.png");
                mapPath=file;
                using(Bitmap loaded=new Bitmap(file))map=(Bitmap)loaded.Clone();
                if(detailed)
                {
                    string regional=Path.Combine(assets,"location-maps",value.Id+"-wide.jpg");
                    regionalOverview=File.Exists(regional);
                    if(regionalOverview||value.Floor==0&&value.Area.X>=2032&&value.Area.X<4080&&value.Area.Y>=2544&&value.Area.Y<4080)
                    {
                        overviewShared=!regionalOverview;
                        overview=regionalOverview?new Bitmap(regional):WorldMap.Get(assets);
                    }
                }
            }
            string travel=value==null?"":HideCoordinates(value.Travel);
            details.Visible=travel.Length>0;
            details.Text=travel;
            details.SetBounds(8,202,Math.Max(1,Width-16),Math.Max(1,Height-208));
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if(clue==null){base.OnPaint(e);return;}
            Graphics g=e.Graphics;g.Clear(KavrosTheme.Panel);
            TextRenderer.DrawText(g,clue.Location,Font,new Rectangle(8,2,Width-16,38),KavrosTheme.Text,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
            Rectangle box=MapBox;
            float cx=clue.Area.X+clue.Area.Width/2F,cy=clue.Area.Y+clue.Area.Height/2F;
            if(map!=null&&(detailed||cx>=2032&&cx<=4079&&cy>=2544&&cy<=4079))
            {
                float span=Span,height=span*box.Height/box.Width;
                float resolution=detailed?map.Width/80F:2;
                if(detailed&&overview==null){pan.X=Math.Max(-Math.Max(0,(80-span)/2),Math.Min(Math.Max(0,(80-span)/2),pan.X));pan.Y=Math.Max(-Math.Max(0,(80-height)/2),Math.Min(Math.Max(0,(80-height)/2),pan.Y));}
                RectangleF source=detailed?new RectangleF((40+pan.X-span/2)*resolution,(40-pan.Y-height/2)*resolution,span*resolution,height*resolution):new RectangleF((cx+pan.X-span/2-2031.5F)*2,(4079.5F-cy-pan.Y-height/2)*2,span*2,height*2);
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                if(overview!=null){
                    float k=overview.Width/320F;
                    RectangleF wide=regionalOverview?new RectangleF((160+pan.X-span/2)*k,(160-pan.Y-height/2)*k,span*k,height*k):new RectangleF((cx+pan.X-span/2-2031.5F)*2,(4079.5F-cy-pan.Y-height/2)*2,span*2,height*2);
                    g.DrawImage(overview,box,wide,GraphicsUnit.Pixel);
                }
                RectangleF visible=RectangleF.Intersect(source,new RectangleF(0,0,map.Width,map.Height));
                if(visible.Width>0&&visible.Height>0){RectangleF dest=new RectangleF(box.Left+(visible.Left-source.Left)*box.Width/source.Width,box.Top+(visible.Top-source.Top)*box.Height/source.Height,visible.Width*box.Width/source.Width,visible.Height*box.Height/source.Height);g.DrawImage(map,dest,visible,GraphicsUnit.Pixel);}
                float unit=box.Width/span;
                float mx=box.Left+box.Width/2F-pan.X*unit,my=box.Top+box.Height/2F+pan.Y*unit;
                var clip=g.Save();g.SetClip(box);
                using(Pen green=new Pen(Color.Lime,2))g.DrawRectangle(green,mx-clue.Area.Width*unit/2,my-clue.Area.Height*unit/2,Math.Max(3,clue.Area.Width*unit),Math.Max(3,clue.Area.Height*unit));
                g.FillEllipse(Brushes.Black,mx-5,my-5,10,10);g.FillEllipse(Brushes.Gold,mx-3,my-3,6,6);
                LodestoneIcons.DrawInView(g,box,cx,cy,span,pan.X,pan.Y,assetsPath);
                LodestoneIcons.DrawCompass(g,Font,box,assetsPath);
                g.Restore(clip);
            }
            else TextRenderer.DrawText(g,clue.Area.IsEmpty?"This spot is not on the surface map.":"Interior or dungeon. Follow the directions.",Font,box,KavrosTheme.Ice,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if(clue!=null&&MapBox.Contains(e.Location)&&e.Delta!=0)
            {zoom=(float)Math.Max(0.04,Math.Min(8,zoom*Math.Pow(1.35,e.Delta/120.0)));Invalidate();}
            base.OnMouseWheel(e);
        }
        protected override void Dispose(bool disposing) { if(disposing && map!=null) { map.Dispose(); map=null; } if(disposing && overview!=null){if(!overviewShared)overview.Dispose();overview=null;} base.Dispose(disposing); }
    }
}
