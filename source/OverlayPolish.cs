using System;
using System.Drawing;
using System.Windows.Forms;

namespace KavrosClueHelper
{
    public sealed partial class FloatingCluePanel
    {
        private void ApplyOverlayPolish()
        {
            ((PaintedLabel)title).Renderer = DrawOverlayTitle;
            title.AccessibleName = "Clue status";
            summary.Font = KavrosTheme.Ui(9.5F, false);
            heading.Font = KavrosTheme.Ui(9F, true);
            summary.ForeColor = KavrosTheme.Ok;
            foreach (Control c in footer.Controls)
            {
                OpaqueButton b = c as OpaqueButton;
                if (b == null) continue;
                b.Tone = b.Text == "Rescan" ? ButtonTone.Primary : b.Text == "Exit" ? ButtonTone.Ghost : ButtonTone.Secondary;
            }
            foreach (Control c in Controls)
            {
                OpaqueButton b = c as OpaqueButton;
                if (b != null) { b.Tone = ButtonTone.Ghost; if (b.Top < title.Bottom) b.Surface = KavrosTheme.BgRaise; }
                NumericUpDown number = c as NumericUpDown;
                if (number != null) { number.BackColor = KavrosTheme.Input; number.ForeColor = KavrosTheme.Text; }
            }
            foreach (NumericUpDown number in new[] { x, y, radius }) { number.BackColor = KavrosTheme.Input; number.ForeColor = KavrosTheme.Text; }
            positions.BackColor = KavrosTheme.Input; positions.ForeColor = KavrosTheme.Text;
            KavrosTheme.StyleCombo(scanFloor);
            // Keep the 400px panel width and every mode's original geometry:
            // compass/scan/slider tools and footer must continue to fit.
        }

        private void DrawOverlayTitle(Graphics g, PaintedLabel label)
        {
            Color surface = KavrosTheme.BgRaise;
            using (Brush b = new SolidBrush(surface)) g.FillRectangle(b, label.ClientRectangle);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) g.FillRectangle(gold, 1, 1, label.Width - 1, 2);
            string text = label.Text ?? "";
            bool searching = mode == PuzzleKind.None || !String.IsNullOrEmpty(ReadingStage);
            bool notFound = searching && summary.Text.StartsWith("Waiting for Kavros", StringComparison.OrdinalIgnoreCase);
            string pill = notFound ? "Not found" : searching ? "Searching" : mode == PuzzleKind.EmoteClue ? "Clue found" : "Found";
            PillTone tone = notFound ? PillTone.Bad : searching ? PillTone.Warn : PillTone.Ok;
            int width = KavrosTheme.DrawPill(g, 10, 17, pill, tone, surface);
            // Existing status/title remains visible alongside the pill. The
            // unchanged Text value remains the accessibility / test source.
            Font font = KavrosTheme.Display(8F);
            KavrosTheme.DrawTracked(g, text, font, width + 18, 17 - font.Height / 2, KavrosTheme.Text, surface, 1.4F, label.Width - 6);
            using (Pen line = new Pen(KavrosTheme.LineSoft)) g.DrawLine(line, 0, label.Height - 1, label.Width, label.Height - 1);
        }
    }
}
