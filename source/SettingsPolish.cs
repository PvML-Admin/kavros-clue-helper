using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KavrosClueHelper
{
    internal static partial class KavrosTheme
    {
        public static void StyleCombo(ComboBox combo)
        {
            combo.BackColor = Input; combo.ForeColor = Text; combo.FlatStyle = FlatStyle.Flat;
        }

        public static void AddMainCards(PuzzleSuiteForm form) { form.ArrangePolish(); }
    }

    public sealed partial class PuzzleSuiteForm
    {
        // Cards drawn behind the existing controls. Coordinates match the
        // control bounds set in the constructor; nothing is re-parented.
        internal void ArrangePolish()
        {
            MainSurface body = Controls.OfType<MainSurface>().Single();
            ((PreviewBox)preview).IsOffline = delegate { return offline; };
            body.Cards.Add(new MainSurface.Card { Label = "CAPTURE", Bounds = new Rectangle(16, 112, 484, 96) });
            body.Cards.Add(new MainSurface.Card { Label = "DETECTION OPTIONS", Bounds = new Rectangle(16, 216, 484, 80) });
            body.Cards.Add(new MainSurface.Card { Label = "SOLUTION", Bounds = new Rectangle(16, 346, 484, 503) });
            foreach (CheckBox c in body.Controls.OfType<CheckBox>())
                if (c != floatingEnabled && c != checkUpdates && c != blurPreview) c.BackColor = KavrosTheme.Panel;
            instructions.BackColor = KavrosTheme.Panel;
        }
    }
}
