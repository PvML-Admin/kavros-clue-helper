using System.Drawing;
using System.IO;

namespace KavrosClueHelper
{
    // One decoded copy of compass/world.png shared by every view.
    // UI-thread only; callers must never Dispose the returned bitmap.
    internal static class WorldMap
    {
        private static Bitmap shared;
        public static Bitmap Get(string assets)
        {
            if (shared == null)
                using (Bitmap loaded = new Bitmap(Path.Combine(assets, "compass", "world.png")))
                    shared = new Bitmap(loaded);
            return shared;
        }
    }
}
