using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KavrosClueHelper
{
    // Failures are written to disk. A player must never get a crash dialog.
    internal static class CrashLog
    {
        internal static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosClueHelper", "crash.log");
            }
        }

        internal static void Install()
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args) { Write(args.Exception); };
            }
            catch (Exception ex) { Write(ex); }
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Exception exception = args.ExceptionObject as Exception;
                Write(exception ?? new Exception(args.ExceptionObject == null ? "Unknown failure" : args.ExceptionObject.ToString()));
            };
            try
            {
                TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs args)
                {
                    Write(args.Exception);
                    args.SetObserved();
                };
            }
            catch (Exception ex) { Write(ex); }
        }

        internal static void Write(Exception exception)
        {
            Write(exception, null);
        }

        internal static void Write(Exception exception, string path)
        {
            try
            {
                if (exception == null) return;
                string file = string.IsNullOrEmpty(path) ? FilePath : path;
                string directory = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + exception + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }

    // The 50ms tick is what keeps an overlay stuck to a moving game window.
    // With no game and no overlay there is nothing to follow, so the tick slows down.
    public static class IdleTiming
    {
        public const int ActiveMilliseconds = 50;
        public const int IdleMilliseconds = 250;
        public static int Interval(bool gameAttached, bool overlayVisible, bool busy)
        {
            return gameAttached || overlayVisible || busy ? ActiveMilliseconds : IdleMilliseconds;
        }
    }
}
