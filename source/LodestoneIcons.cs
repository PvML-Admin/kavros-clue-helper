using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
namespace KavrosClueHelper
{
    internal static class LodestoneIcons
    {
        private static readonly Dictionary<string,Bitmap> cache=new Dictionary<string,Bitmap>();
        private static readonly List<Stone> stones=new List<Stone>();
        private static string stonesFrom;
        private static Bitmap compass;
        private static string compassFrom;
        private sealed class Stone { public string Name; public float X; public float Y; }
        public static string Key(string name)
        {
            if(name==null)return "";
            string text=name.ToLowerInvariant().Replace(" lodestone","").Replace(" (kavros)","").Replace("'","");
            var chars=new List<char>();
            bool dash=false;
            foreach(char c in text)
            {
                if((c>='a'&&c<='z')||(c>='0'&&c<='9')){chars.Add(c);dash=false;}
                else if(!dash&&chars.Count>0){chars.Add('-');dash=true;}
            }
            string key=new string(chars.ToArray());
            return key.Trim('-');
        }
        public static string Resolve(string assets, string name)
        {
            string key=Key(name);
            if(key.Length==0||string.IsNullOrEmpty(assets))return null;
            string preferred=Path.Combine(assets,"map-icons","lodestones",key+".png");
            if(File.Exists(preferred))return preferred;
            string fallback=Path.Combine(assets,"compass","lodestones",key+".png");
            if(File.Exists(fallback))return fallback;
            return null;
        }
        public static string CompassFile(string assets)
        {
            return Path.Combine(assets??"","map-icons","compass.png");
        }
        public static bool InView(Rectangle box, float cx, float cy, float span, float panX, float panY, float lx, float ly, out PointF screen)
        {
            float unit=box.Width/Math.Max(1F,span);
            float sx=box.Left+box.Width/2F+(lx-cx-panX)*unit;
            float sy=box.Top+box.Height/2F-(ly-cy-panY)*unit;
            screen=new PointF(sx,sy);
            return sx>=box.Left+2&&sy>=box.Top+2&&sx<=box.Right-2&&sy<=box.Bottom-2;
        }
        public static void DrawInView(Graphics g, Rectangle box, float cx, float cy, float span, float panX, float panY, string assets)
        {
            if(g==null||string.IsNullOrEmpty(assets)||span<=0||box.Width<8)return;
            Ensure(assets);
            foreach(Stone stone in stones)
            {
                PointF screen;
                if(!InView(box,cx,cy,span,panX,panY,stone.X,stone.Y,out screen))continue;
                Draw(g,screen,stone.Name,false,assets);
            }
        }
        public static void DrawCompass(Graphics g, Font font, Rectangle bounds, string assets)
        {
            if(g==null||bounds.Width<8)return;
            string file=CompassFile(assets);
            if(File.Exists(file))
            {
                if(compass==null||compassFrom!=file)
                {
                    if(compass!=null)compass.Dispose();
                    using(Bitmap loaded=new Bitmap(file))compass=new Bitmap(loaded);
                    compassFrom=file;
                }
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                int size=28;
                g.DrawImage(compass,bounds.Right-size-4,bounds.Top+4,size,size);
                return;
            }
            Font used=font??SystemFonts.DefaultFont;
            TextRenderer.DrawText(g,"N",used,new Rectangle(bounds.Right-24,bounds.Top+3,20,18),Color.White,Color.Black,TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPadding);
        }
        public static void Draw(Graphics g, PointF p, string name, bool selected, string assets)
        {
            Bitmap icon=Load(assets,name);
            GraphicsState state=g.Save();
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            if(icon!=null)g.DrawImage(icon,p.X-12,p.Y-12,24,24);
            else
            {
                using(Brush fill=new SolidBrush(Color.FromArgb(40,70,90)))g.FillEllipse(fill,p.X-11,p.Y-11,22,22);
                using(Pen ring=new Pen(Color.Gold,1.5F))g.DrawEllipse(ring,p.X-11,p.Y-11,22,22);
            }
            if(selected)using(Pen pen=new Pen(Color.Lime,2.5F))g.DrawEllipse(pen,p.X-14,p.Y-14,28,28);
            g.Restore(state);
        }
        private static void Ensure(string assets)
        {
            if(stonesFrom==assets)return;
            stones.Clear();
            stonesFrom=assets;
            string file=Path.Combine(assets,"compass","positions.tsv");
            if(!File.Exists(file))return;
            foreach(string line in File.ReadAllLines(file))
            {
                string[] fields=line.Split('\t');
                float x,y;
                if(fields.Length<3)continue;
                if(!float.TryParse(fields[1],NumberStyles.Float,CultureInfo.InvariantCulture,out x))continue;
                if(!float.TryParse(fields[2],NumberStyles.Float,CultureInfo.InvariantCulture,out y))continue;
                if(x<2032||x>4079||y<2544||y>4079)continue;
                stones.Add(new Stone{Name=fields[0],X=x,Y=y});
            }
        }
        private static Bitmap Load(string assets, string name)
        {
            string key=Key(name);
            Bitmap found;
            if(cache.TryGetValue(key,out found))return found;
            string file=Resolve(assets,name);
            if(file==null){cache[key]=null;return null;}
            using(Bitmap loaded=new Bitmap(file))found=new Bitmap(loaded);
            cache[key]=found;
            return found;
        }
    }
}
