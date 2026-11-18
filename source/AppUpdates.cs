using System;
using System.Collections.Generic;
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
        internal static string RestartedVersion;
        internal static Action<string, bool, string, Action> Present;
        private static int busy;

        internal static void Start()
        {
            VelopackApp.Build().OnRestarted(delegate(SemanticVersion version) { RestartedVersion = version == null ? "" : version.ToString(); }).Run();
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
                    if (interactive) Tell(owner, "This copy cannot update itself. Download Setup.exe from GitHub Releases.", true, null, null);
                    return;
                }
                UpdateInfo info = manager.CheckForUpdates();
                if (info == null || info.TargetFullRelease == null)
                {
                    if (interactive)
                    {
                        string current = manager.CurrentVersion == null ? AppBranding.DisplayVersion : manager.CurrentVersion.ToString();
                        Tell(owner, "You are on the latest version (" + current + ").", false, null, null);
                    }
                    return;
                }
                string version = info.TargetFullRelease.Version.ToString();
                Tell(owner, "Version " + version + " is available.", false, "Update now", delegate
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            Tell(owner, "Downloading version " + version + "...", false, null, null);
                            manager.DownloadUpdates(info, null);
                            PuzzleSuiteForm suite = owner as PuzzleSuiteForm;
                            if (suite != null && !suite.IsDisposed) suite.PrepareForUpdate();
                            manager.ApplyUpdatesAndRestart(info.TargetFullRelease, null);
                        }
                        catch (Exception ex) { Tell(owner, Describe(ex), true, null, null); }
                    });
                });
            }
            catch (NotInstalledException)
            {
                if (interactive) Tell(owner, "This copy cannot update itself. Download Setup.exe from GitHub Releases.", true, null, null);
            }
            catch (Exception ex)
            {
                if (interactive) Tell(owner, "Could not check GitHub for updates." + Environment.NewLine + Describe(ex), true, null, null);
            }
        }

        private static void Tell(Form owner, string message, bool error, string action, Action click)
        {
            Action show = delegate
            {
                if (Present != null) Present(message, error, action, click);
                else SetStatus(owner, message);
            };
            try
            {
                if (owner != null && !owner.IsDisposed && owner.InvokeRequired) owner.BeginInvoke(show);
                else show();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private static string Describe(Exception ex)
        {
            List<string> parts = new List<string>();
            Exception current = ex;
            while (current != null)
            {
                if (!string.IsNullOrEmpty(current.Message)) parts.Add(current.GetType().Name + ": " + current.Message);
                current = current.InnerException;
            }
            return string.Join(Environment.NewLine, parts.ToArray());
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
