using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
namespace KavrosClueHelper
{
    public sealed class SlidingGuideView : Control
    {
        private readonly string assets;
        private Bitmap picture;
        private string theme;
        public SlidingGuideView(string directory){assets=directory;DoubleBuffered=true;BackColor=Color.FromArgb(18,23,32);}
        public void SetTheme(string value)
        {
            if(theme==value)return;theme=value;if(picture!=null){picture.Dispose();picture=null;}
            if(!string.IsNullOrEmpty(value))picture=new Bitmap(Path.Combine(assets,"data","sliders","sliders",value+".png"));
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            using(Brush fill=new SolidBrush(BackColor))e.Graphics.FillRectangle(fill,ClientRectangle);
            if(picture!=null){e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(picture,new Rectangle(0,20,160,160));e.Graphics.DrawRectangle(Pens.SlateGray,0,20,159,159);}
            TextRenderer.DrawText(e.Graphics,"Finished picture",Font,new Point(0,0),Color.White);
            TextRenderer.DrawText(e.Graphics,"Click the numbered tiles\nin order.\n\nGreen = next click\nThen cyan, orange, purple\n\nGuidance updates as\nyou move the tiles.",Font,new Rectangle(174,18,Width-174,Height-18),Color.FromArgb(123,232,191),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);
        }
        protected override void Dispose(bool disposing){if(disposing&&picture!=null)picture.Dispose();base.Dispose(disposing);}
    }
}
