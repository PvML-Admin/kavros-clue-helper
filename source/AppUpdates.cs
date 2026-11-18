using System;
using System.Threading;
using System.Windows.Forms;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace KavrosClueHelper
{
    internal static class AppUpdates
    {
        internal const string RepositoryUrl = "https://github.com/Zenity-sys/kavros-clue-helper";
        private static int busy;

        internal static void Start()
        {
            VelopackApp.Build().Run();
        }

        internal static void Check(Form owner, bool interactive)
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            {
                if (interactive) Show(owner, "An update check is already running.", MessageBoxIcon.Information);
                return;
            }
            if (interactive) SetStatus(owner, "Checking GitHub for updates...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { CheckWorker(owner, interactive); }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }

        private static void CheckWorker(Form owner, bool interactive)
        {
            try
            {
                GithubSource source = new GithubSource(RepositoryUrl, null, false, null);
                UpdateManager manager = new UpdateManager(source, null, null);
                if (!manager.IsInstalled)
                {
                    if (interactive)
                    {
                        Show(owner, "This copy cannot update itself. Install Setup.exe, or run the portable package from the GitHub release. Releases: " + RepositoryUrl + "/releases", MessageBoxIcon.Information);
                        SetStatus(owner, "This copy was not installed with Setup.exe.");
                    }
                    return;
                }
                UpdateInfo info = manager.CheckForUpdates();
                if (info == null || info.TargetFullRelease == null)
                {
                    if (interactive)
                    {
                        string current = manager.CurrentVersion == null ? AppBranding.DisplayVersion : manager.CurrentVersion.ToString();
                        Show(owner, "You are on the latest version (" + current + ").", MessageBoxIcon.Information);
                        SetStatus(owner, "No update is available.");
                    }
                    return;
                }
                string version = info.TargetFullRelease.Version.ToString();
                DialogResult answer = Ask(owner, "Version " + version + " is available. Download it and restart now?", MessageBoxButtons.YesNo);
                if (answer != DialogResult.Yes) return;
                SetStatus(owner, "Downloading version " + version + "...");
                manager.DownloadUpdates(info, null);
                Action prepare = delegate
                {
                    PuzzleSuiteForm suite = owner as PuzzleSuiteForm;
                    if (suite != null && !suite.IsDisposed) suite.PrepareForUpdate();
                };
                try
                {
                    if (owner != null && !owner.IsDisposed && owner.InvokeRequired) owner.Invoke(prepare);
                    else prepare();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                manager.ApplyUpdatesAndRestart(info.TargetFullRelease, null);
            }
            catch (NotInstalledException)
            {
                if (interactive) Show(owner, "This copy cannot update itself. Releases: " + RepositoryUrl + "/releases", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (interactive)
                {
                    Show(owner, "Could not check GitHub for updates. " + ex.Message, MessageBoxIcon.Warning);
                    SetStatus(owner, "Update check failed.");
                }
            }
        }

        private static void SetStatus(Form owner, string message)
        {
            PuzzleSuiteForm suite = owner as PuzzleSuiteForm;
            if (suite == null || suite.IsDisposed) return;
            Action set = delegate { suite.SetUpdateStatus(message); };
            try
            {
                if (suite.InvokeRequired) suite.BeginInvoke(set);
                else set();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private static void Show(Form owner, string message, MessageBoxIcon icon)
        {
            Ask(owner, message, MessageBoxButtons.OK, icon);
        }

        private static DialogResult Ask(Form owner, string message, MessageBoxButtons buttons)
        {
            return Ask(owner, message, buttons, MessageBoxIcon.Information);
        }

        private static DialogResult Ask(Form owner, string message, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            DialogResult result = DialogResult.None;
            Action ask = delegate
            {
                if (owner != null && !owner.IsDisposed && owner.Visible)
                    result = MessageBox.Show(owner, message, "Kavros Clue Helper", buttons, icon);
                else
                    result = MessageBox.Show(message, "Kavros Clue Helper", buttons, icon);
            };
            try
            {
                if (owner != null && !owner.IsDisposed && owner.InvokeRequired) owner.Invoke(ask);
                else ask();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { ask(); }
            return result;
        }
    }
}
