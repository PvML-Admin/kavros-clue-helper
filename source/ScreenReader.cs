using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KavrosClueHelper
{
    public sealed class Reading
    {
        public int[] Board = new int[25];
        public bool[] Certain = new bool[25];
        public int Uncertain;
    }

    public static class ScreenReader
    {
        public static Bitmap Capture(Rectangle bounds)
        {
            if (bounds.Width < 60 || bounds.Height < 60)
                throw new ArgumentException("Select the whole 5 x 5 grid, from its outer top-left to outer bottom-right corner.");
            Bitmap image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(image))
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                return image;
            }
            catch { image.Dispose(); throw; }
        }

        // Legacy 944 symbols have distinctive colours: blue hats, gold bows,
        // and pale swords. Ignore cell edges and vote on the icon's interior.
        // Unrecognised/ambiguous cells stay unknown rather than becoming swords.
        public static Reading Read(Bitmap source)
        {
            if (source.Width < 60 || source.Height < 60) throw new ArgumentException("Grid image is too small.");
            Reading result = new Reading();
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 5; column++)
                {
                    int left = (int)Math.Round((column + 0.18) * source.Width / 5.0);
                    int right = (int)Math.Round((column + 0.82) * source.Width / 5.0);
                    int top = (int)Math.Round((row + 0.18) * source.Height / 5.0);
                    int bottom = (int)Math.Round((row + 0.82) * source.Height / 5.0);
                    int blue = 0, gold = 0, pale = 0, pixels = 0;
                    for (int y = top; y < bottom; y++)
                        for (int x = left; x < right; x++)
                        {
                            Color p = source.GetPixel(x, y);
                            pixels++;
                            if (p.B > 78 && p.B > p.R * 1.30 && p.B > p.G * 1.14 && p.B - p.R > 28) blue++;
                            if (p.R > 112 && p.G > 62 && p.R > p.G * 1.12 && p.G > p.B * 1.18 && p.R - p.B > 38) gold++;
                            int high = Math.Max(p.R, Math.Max(p.G, p.B));
                            int low = Math.Min(p.R, Math.Min(p.G, p.B));
                            if (low > 112 && high - low < 62) pale++;
                        }
                    int index = row * 5 + column;
                    int threshold = Math.Max(3, (int)(pixels * 0.025));
                    if (blue >= threshold && blue > gold * 0.55)
                    { result.Board[index] = 2; result.Certain[index] = true; }
                    else if (gold >= threshold && gold > blue * 1.5)
                    { result.Board[index] = 1; result.Certain[index] = true; }
                    else if (pale >= threshold && blue < threshold && gold < threshold)
                    { result.Board[index] = 0; result.Certain[index] = true; }
                    else { result.Board[index] = -1; result.Uncertain++; }
                }
            return result;
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct PointStruct { public int X, Y; }
        public delegate bool EnumProc(IntPtr window, IntPtr param);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int length);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref PointStruct point);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(PointStruct point);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong32(IntPtr window, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
        public static void SetOwner(IntPtr child, IntPtr owner)
        {
            if (child == IntPtr.Zero) return;
            if (IntPtr.Size == 8) SetWindowLongPtr64(child, -8, owner);
            else SetWindowLong32(child, -8, owner.ToInt32());
        }
        public static Rectangle ClientBounds(IntPtr window)
        {
            Rect r;
            PointStruct p = new PointStruct();
            if (!GetClientRect(window, out r) || !ClientToScreen(window, ref p)) return Rectangle.Empty;
            return new Rectangle(p.X, p.Y, r.Right - r.Left, r.Bottom - r.Top);
        }
    }
}
