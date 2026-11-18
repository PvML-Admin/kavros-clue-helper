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
            BackColor=KavrosTheme.Bg;ForeColor=KavrosTheme.Text;
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
        internal ButtonTone Tone = ButtonTone.Secondary;
        // Colour behind the rounded corners; empty means the parent surface.
        internal Color Surface = Color.Empty;
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
            KavrosTheme.DrawButton(e.Graphics, ClientRectangle, Text, Font, Tone, Enabled, hover, pressed, Surface.IsEmpty ? KavrosTheme.SurfaceOf(this) : Surface);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
        }
    }
}
