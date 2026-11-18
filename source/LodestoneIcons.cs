using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
namespace KavrosClueHelper
{
    internal static class LodestoneIcons
    {
        private static readonly Dictionary<string,Bitmap> cache=new Dictionary<string,Bitmap>();
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
        private static Bitmap Load(string assets, string name)
        {
            string key=Key(name);
            Bitmap found;
            if(cache.TryGetValue(key,out found))return found;
            string file=Path.Combine(assets,"compass","lodestones",key+".png");
            if(!File.Exists(file)){cache[key]=null;return null;}
            using(Bitmap loaded=new Bitmap(file))found=new Bitmap(loaded);
            cache[key]=found;
            return found;
        }
    }
}
