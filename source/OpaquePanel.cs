using System;
using System.Drawing;
using System.Windows.Forms;
namespace KavrosClueHelper
{
    // Own the whole container surface. Child HWNDs keep their normal painting;
    // only our own pixels are buffered, without WS_EX_COMPOSITED on the form.
    public sealed class OpaquePanel : Panel
    {
        public OpaquePanel()
        {
            BackColor=Color.FromArgb(18,23,32);ForeColor=Color.White;
            SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {using(Brush fill=new SolidBrush(BackColor))e.Graphics.FillRectangle(fill,ClientRectangle);}
        protected override void OnPaint(PaintEventArgs e)
        {
            using(Brush fill=new SolidBrush(BackColor))e.Graphics.FillRectangle(fill,ClientRectangle);
            base.OnPaint(e);
        }
    }
    public sealed class OpaqueButton : Button
    {
        private bool hover,pressed;
        public OpaqueButton()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnEnabledChanged(EventArgs e){pressed=false;Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaintBackground(PaintEventArgs e){using(Brush fill=new SolidBrush(BackColor))e.Graphics.FillRectangle(fill,ClientRectangle);}
        protected override void OnPaint(PaintEventArgs e)
        {
            Color background=!Enabled?Color.FromArgb(27,36,48):pressed?Color.FromArgb(24,42,57):hover?Color.FromArgb(48,67,86):BackColor;
            using(Brush fill=new SolidBrush(background))e.Graphics.FillRectangle(fill,ClientRectangle);
            using(Pen border=new Pen(FlatAppearance.BorderColor))e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(150,162,175),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
        }
    }
}
