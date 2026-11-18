using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace KavrosClueHelper
{
    public sealed class GuidanceMark
    {
        public Rectangle Box;
        public string Text;
        public Color Colour;
        public bool Corner, Compact, Sliding, Pulse;
        public bool Outline = true;
        public int ArrowX, ArrowY;
        public GuidanceMark Offset(int x, int y)
        {
            Rectangle box = Box; box.Offset(x, y);
            return new GuidanceMark { Box = box, Text = Text, Colour = Colour, Corner = Corner, Compact = Compact, Outline = Outline, Sliding=Sliding, Pulse=Pulse, ArrowX=ArrowX, ArrowY=ArrowY };
        }
    }

    // Layout and painting are independent of any windows, so they can be
    // checked on bitmaps without displaying an overlay or touching the game.
    public static class GuidanceDrawing
    {
        public static Rectangle Cell(DetectedPuzzle puzzle, int index)
        {
            int pitch = puzzle.Kind == PuzzleKind.Sliding ? 56 : puzzle.Kind == PuzzleKind.Towers ? 44 : 38;
            int size = puzzle.Kind == PuzzleKind.Sliding ? 49 : puzzle.Kind == PuzzleKind.Towers ? 42 : 30;
            return new Rectangle(puzzle.Grid.X + (int)Math.Round(index % 5 * pitch * puzzle.Scale), puzzle.Grid.Y + (int)Math.Round(index / 5 * pitch * puzzle.Scale), (int)Math.Round(size * puzzle.Scale), (int)Math.Round(size * puzzle.Scale));
        }
        public static List<GuidanceMark> SliderMarks(DetectedPuzzle puzzle,IList<int> moves,int firstNumber=1,int maximum=4)
        {
            var marks=new List<GuidanceMark>();var used=new HashSet<int>();
            for(int i=0;i<Math.Min(maximum,moves.Count);i++)
            {
                if(!used.Add(moves[i]))break;
                Color[] colours={Color.Lime,Color.Cyan,Color.Orange,Color.MediumPurple};
                marks.Add(new GuidanceMark{Box=Cell(puzzle,moves[i]),Text=(firstNumber+i).ToString(),Colour=colours[i%colours.Length],Sliding=true});
            }
            return marks;
        }
        public static int TowerClicksRemaining(int current, int target, bool skipBlank)
        {
            if (current < 0 || current > 5 || target < 1 || target > 5) throw new ArgumentOutOfRangeException("Tower heights must be blank or 1-5.");
            if (current == 0) return target;
            int cycle = skipBlank ? 5 : 6;
            return (target - current + cycle) % cycle;
        }
        public static List<GuidanceMark> TowerMarks(DetectedPuzzle puzzle, int[] solution, bool countdown = false, bool skipBlank = false)
        {
            List<GuidanceMark> result = new List<GuidanceMark>();
            for (int i = 0; i < 25; i++)
            {
                int value = countdown ? TowerClicksRemaining(puzzle.Towers.Cells[i], solution[i], skipBlank) : solution[i];
                result.Add(new GuidanceMark { Box = Cell(puzzle, i), Text = value.ToString(), Colour = countdown && value == 0 ? Color.Lime : Color.White, Corner = true, Compact = true, Outline = false });
            }
            return result;
        }
        public static float CompactFontSize(Rectangle box) { return Math.Max(8F, Math.Min(13F, box.Height * 0.24F)); }
        public static Rectangle LabelBounds(GuidanceMark mark)
        {
            if (mark.Compact)
            {
                float size = CompactFontSize(mark.Box);
                int inset = (int)Math.Ceiling(Math.Max(2F, mark.Box.Width * 0.055F));
                int width = (int)Math.Ceiling(size * 0.85F), height = (int)Math.Ceiling(size + 3);
                return new Rectangle(mark.Box.Right - inset - width, mark.Box.Top + inset, width, height);
            }
            return mark.Corner ? new Rectangle(mark.Box.X + 1, mark.Box.Y + 1, Math.Max(12, mark.Box.Width / 2), Math.Max(15, mark.Box.Height / 2)) : mark.Box;
        }
        public static bool SameMarks(IList<GuidanceMark> first, IList<GuidanceMark> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                GuidanceMark a = first[i], b = second[i];
                if (a.Box != b.Box || a.Text != b.Text || a.Colour.ToArgb() != b.Colour.ToArgb() || a.Corner != b.Corner || a.Compact != b.Compact || a.Outline != b.Outline || a.Sliding!=b.Sliding || a.Pulse!=b.Pulse || a.ArrowX!=b.ArrowX || a.ArrowY!=b.ArrowY) return false;
            }
            return true;
        }
        public static bool HideForCapture(bool captureExcluded, PuzzleKind kind)
        {
            // Tower badges avoid the grid lines and centred game digits, so
            // even on older Windows they can remain visible during a capture.
            return !captureExcluded && kind != PuzzleKind.Towers && kind != PuzzleKind.Sliding && kind != PuzzleKind.Knot;
        }
        public static void Draw(Graphics graphics, IEnumerable<GuidanceMark> marks)
        {
            foreach (GuidanceMark mark in marks)
            {
                if(mark.Sliding)
                {
                    int grow=mark.Pulse?(((Environment.TickCount/180)%2==0)?4:8):2;
                    Rectangle box=Rectangle.Inflate(mark.Box,grow,grow);
                    using(Pen shadow=new Pen(Color.Black,mark.Pulse?7:5))graphics.DrawRectangle(shadow,box);
                    using(Pen ink=new Pen(mark.Colour,mark.Pulse?4:2))graphics.DrawRectangle(ink,box);
                    if(mark.ArrowX!=0||mark.ArrowY!=0)
                    {
                        int x1=mark.Box.Left+mark.Box.Width/2,y1=mark.Box.Top+mark.Box.Height/2;
                        int x2=x1+mark.ArrowX*mark.Box.Width,y2=y1+mark.ArrowY*mark.Box.Height;
                        using(Pen arrow=new Pen(mark.Colour,3)){arrow.EndCap=System.Drawing.Drawing2D.LineCap.ArrowAnchor;graphics.DrawLine(arrow,x1,y1,x2,y2);}
                    }
                    int size=Math.Max(12,Math.Min(22,(int)Math.Round(mark.Box.Width*0.40)));Rectangle badge=new Rectangle(mark.Box.Left+1,mark.Box.Top+1,size,size);
                    graphics.FillEllipse(Brushes.Black,badge);
                    using(Font digit=new Font("Segoe UI",size*(mark.Text.Length>2?0.43F:0.65F),FontStyle.Bold,GraphicsUnit.Pixel))TextRenderer.DrawText(graphics,mark.Text,digit,badge,mark.Colour,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
                    continue;
                }
                if (mark.Outline) using (Pen pen = new Pen(mark.Colour, 2)) graphics.DrawRectangle(pen, mark.Box);
                Rectangle label = LabelBounds(mark);
                if (mark.Corner || mark.Compact) graphics.FillRectangle(Brushes.Black, label);
                using (Font font = mark.Compact
                    ? new Font("Segoe UI", CompactFontSize(mark.Box), FontStyle.Bold, GraphicsUnit.Pixel)
                    : new Font("Segoe UI", Math.Max(9, Math.Min(17, mark.Box.Height * 0.38F)), FontStyle.Bold))
                {
                    TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                    if (!mark.Compact)
                    {
                        Rectangle shadow = label; shadow.Offset(1, 1);
                        TextRenderer.DrawText(graphics, mark.Text, font, shadow, Color.Black, flags);
                    }
                    TextRenderer.DrawText(graphics, mark.Text, font, label, mark.Colour, flags);
                }
            }
        }
    }
}
