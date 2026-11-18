using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

namespace KavrosClueHelper
{
    public static class WindowLocator
    {
        private static bool Contains(string text, string value) { return (text ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0; }
        public static bool IsLauncher(GameWindow window)
        {
            bool gameTitle = Contains(window.Title, "nxt") || Contains(window.Title, "944") || Contains(window.ClassName, "jag") || Contains(window.ClassName, "glfw");
            return Contains(window.Title, "launcher") || Contains(window.ClassName, "launcher") || (Contains(window.ProcessName, "launcher") && !gameTitle);
        }
        public static int Score(GameWindow window)
        {
            if (window == null || window.Automatic || window.Handle == IntPtr.Zero || Contains(window.Title, "Kavros Clue Helper") || Contains(window.ProcessName, "KavrosClueHelper")) return -1000;
            if (Contains(window.ClassName, "CabinetWClass") || Contains(window.ClassName, "WinRarWindow") || Contains(window.ClassName, "Chrome_WidgetWin") || Contains(window.ClassName, "ApplicationFrameWindow")) return -1000;
            if (IsLauncher(window)) return -1000;
            // Title must contain Kavros. A rev number such as 944 or 951 is not required,
            // and another server's rs2client window is not a match.
            bool titleMatch = Contains(window.Title, "kavros");
            if (!titleMatch) return -1000;
            int score = 400;
            if (Contains(window.ProcessName, "kavros")) score += 80;
            if (Contains(window.ClassName, "jag") || Contains(window.ClassName, "glfw") || Contains(window.ClassName, "lwjgl")) score += 70;
            if (window.Client.Width >= 600 && window.Client.Height >= 350) score += 40;
            if (window.Client.Width < 250 || window.Client.Height < 150) score -= 250;
            return score;
        }
        public static GameWindow Choose(IEnumerable<GameWindow> windows, IntPtr current, IntPtr foreground)
        {
            List<GameWindow> games = windows.Where(w => Score(w) >= 100).OrderByDescending(Score).ThenBy(w => w.Title).ToList();
            GameWindow focused = games.Find(w => w.Handle == foreground);
            if (focused != null && Score(focused) >= Score(games[0]) - 50) return focused;
            GameWindow existing = games.Find(w => w.Handle == current);
            if (existing != null && (games.Count == 0 || Score(games[0]) <= Score(existing))) return existing;
            return games.FirstOrDefault();
        }
        public static List<GameWindow> Enumerate(int excludedProcessId)
        {
            List<GameWindow> list = new List<GameWindow>();
            Dictionary<uint, string> processNames = new Dictionary<uint, string>();
            Native.EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                if (!Native.IsWindowVisible(window)) return true;
                uint pid; Native.GetWindowThreadProcessId(window, out pid);
                if (pid == excludedProcessId) return true;
                StringBuilder title = new StringBuilder(1024); Native.GetWindowText(window, title, title.Capacity);
                if (title.Length == 0) return true;
                StringBuilder className = new StringBuilder(256); Native.GetClassName(window, className, className.Capacity);
                string processName;
                if (!processNames.TryGetValue(pid, out processName))
                {
                    try { using (Process process = Process.GetProcessById((int)pid)) processName = process.ProcessName; }
                    catch { processName = ""; }
                    processNames[pid] = processName;
                }
                if (Contains(processName, "KavrosClueHelper") || Contains(title.ToString(), "Kavros Clue Helper")) return true;
                list.Add(new GameWindow { Handle = window, Title = title.ToString(), ProcessId = pid, ProcessName = processName, ClassName = className.ToString(), Client = Native.ClientBounds(window) });
                return true;
            }, IntPtr.Zero);
            return list.OrderByDescending(Score).ThenBy(w => w.Title).ToList();
        }
        public static bool IsForeground(IntPtr game)
        {
            if (game == IntPtr.Zero) return false;
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            return foreground == game || Native.GetAncestor(foreground, 2) == game || (Native.GetAncestor(foreground, 3) == Native.GetAncestor(game, 3) && Native.GetAncestor(game, 3) != IntPtr.Zero);
        }
        public static bool AtPoint(IntPtr window,Point point,IntPtr transparentOverlay)
        {
            IntPtr hit=Native.WindowFromPoint(new Native.PointStruct { X=point.X,Y=point.Y });
            return hit!=IntPtr.Zero && (hit==transparentOverlay || hit==window || Native.GetAncestor(hit,2)==window);
        }
        public static string Describe(IEnumerable<GameWindow> all, GameWindow selected)
        {
            StringBuilder report = new StringBuilder("Kavros Clue Helper " + AppBranding.DisplayVersion + "\r\n");
            report.AppendLine("Windows: " + Environment.OSVersion.VersionString);
            if (selected == null) report.AppendLine("Game target: none");
            else
            {
                report.AppendLine("Game target: " + selected.Title);
                report.AppendLine("Process: " + selected.ProcessName + " / " + selected.ProcessId);
                report.AppendLine("Window class: " + selected.ClassName);
                report.AppendLine("Client rectangle: " + Native.ClientBounds(selected.Handle));
                report.AppendLine("Minimized: " + Native.IsIconic(selected.Handle));
                report.AppendLine("Game foreground: " + IsForeground(selected.Handle));
            }
            foreach (GameWindow window in all.Where(w => Score(w) >= 100 || (IsLauncher(w) && (Contains(w.Title, "kavros") || Contains(w.ProcessName, "kavros")))))
                report.AppendLine("Candidate: " + window.Title + " | " + window.ProcessName + " | " + window.ClassName + " | score " + Score(window) + " | " + window.Client);
            return report.ToString();
        }
    }
}
