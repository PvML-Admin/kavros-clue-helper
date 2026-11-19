using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace KavrosClueHelper
{
    public static class WindowRegressionTests
    {
        private static int assertions;
        private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception("FAILED: " + message); }
        private static GameWindow Window(int handle, string title, string process, string className)
        { return new GameWindow { Handle = new IntPtr(handle), Title = title, ProcessName = process, ClassName = className, Client = new Rectangle(0, 0, 1920, 1080) }; }
        public static int Run(string report)
        {
            try
            {
                GameWindow browser = Window(1, "Browser", "chrome", "Chrome_WidgetWin_1");
                GameWindow launcher = Window(2, "com.kavros.launcher-siw", "KavrosLauncher", "SunAwtFrame");
                GameWindow game = Window(3, "Kavros 944 | NXT LIVE", "rs2client", "JagWindow");
                GameWindow oldHelper = Window(4, "Kavros Clue Helper | v0.2", "KavrosClueHelper", "WindowsForms10.Window");
                Check(WindowLocator.Choose(new[] { browser, launcher, oldHelper }, IntPtr.Zero, browser.Handle) == null, "no arbitrary window or launcher selected before game opens");
                Check(WindowLocator.Choose(new[] { browser, launcher, game, oldHelper }, IntPtr.Zero, browser.Handle) == game, "client opened after helper is selected");
                GameWindow replacement = Window(5, "KAVROS 944 | nxt live", "rs2client", "JagWindow");
                Check(WindowLocator.Choose(new[] { browser, launcher, replacement }, game.Handle, browser.Handle) == replacement, "restarted game reconnects by new handle");
                Check(WindowLocator.Choose(new[] { browser, launcher, game }, game.Handle, browser.Handle) == game, "foreground browser does not replace connected game");
                Check(WindowLocator.Choose(new[] { game, replacement }, game.Handle, replacement.Handle) == game, "foreground client does not replace the chosen client");
                Check(WindowLocator.Choose(new[] { game, replacement }, game.Handle, browser.Handle) == game, "existing valid selection remains stable without focus change");
                game.ProcessId = 44; replacement.ProcessId = 55;
                GameWindow restarted = Window(15, "Kavros 944 | NXT LIVE", "rs2client", "JagWindow"); restarted.ProcessId = 44;
                Check(WindowLocator.Choose(new[] { restarted, replacement }, game.Handle, 44, replacement.Handle) == restarted, "lost handle reattaches only to the same process");
                Check(WindowLocator.Choose(new[] { replacement }, game.Handle, 44, replacement.Handle) == null, "a different client's process is not adopted when the chosen one closes");
                Check(WindowLocator.Choose(new[] { game, replacement }, IntPtr.Zero, replacement.Handle) == replacement, "first selection may use the focused client");
                Check(WindowLocator.Score(replacement) >= 100, "case-insensitive client title recognition");
                GameWindow titledLauncher = Window(6, "Kavros", "KavrosLauncher", "SunAwtFrame");
                Check(WindowLocator.Score(titledLauncher) < 100, "process-only launcher hint excludes launcher");
                GameWindow sharedLauncherProcess = Window(7, "Kavros 944 | NXT LIVE", "KavrosLauncher", "JagWindow");
                Check(WindowLocator.Score(sharedLauncherProcess) >= 100, "real client may share launcher's process name");
                GameWindow processOnly = Window(8, "Game", "KavrosClient", "GLFW30");
                Check(WindowLocator.Score(processOnly) < 100, "a title without Kavros is not a client");
                GameWindow tiny = Window(9, "Kavros", "java", "Splash"); tiny.Client = new Rectangle(0, 0, 120, 80);
                Check(WindowLocator.Choose(new[] { game, tiny }, IntPtr.Zero, browser.Handle) == game, "game preferred over small splash window");
                GameWindow official = Window(10, "RuneScape", "rs2client", "JagWindow");
                Check(WindowLocator.Choose(new[] { game, official }, game.Handle, official.Handle) == game, "lower-confidence NXT process does not steal Kavros selection");
                Check(WindowLocator.Choose(new GameWindow[0], game.Handle, IntPtr.Zero) == null, "closed game cleanly disconnects");
                GameWindow discord = Window(11, "Kavros - Discord", "Discord", "Chrome_WidgetWin_1");
                GameWindow archive = Window(12, "KavrosClueHelper-v0.2.zip", "WinRAR", "WinRarWindow");
                GameWindow folder = Window(13, "KavrosClueHelper-v0.2", "explorer", "CabinetWClass");
                Check(WindowLocator.Choose(new[] { discord, archive, folder, launcher }, IntPtr.Zero, discord.Handle) == null, "Kavros mentions in Discord, archives and folders are not game windows");
                Check(FloatingPanelLayout.Visible(true, false, true, false, false, false, false), "another app in front leaves the panel visible");
                Check(!FloatingPanelLayout.Visible(true, false, true, true, true, false, false), "minimising the chosen client hides the panel");
                Check(!FloatingPanelLayout.Visible(true, true, true, false, true, false, false), "screenshot mode does not show the live panel");
                File.AppendAllText(report, "\r\nV0.2.1 WINDOW REGRESSIONS PASS: " + assertions + " assertions.\r\nCovers late launch, restart, launcher exclusion, renamed client, multiple windows and stable selection.\r\n");
                return 0;
            }
            catch (Exception ex) { File.AppendAllText(report, ex + "\r\n"); return 1; }
        }
    }
}
