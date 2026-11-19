using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using Timer = System.Windows.Forms.Timer;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace KavrosClueHelper
{
    public sealed class GuidanceOverlay : Form
    {
        private List<GuidanceMark> marks = new List<GuidanceMark>();
        public bool CaptureExcluded { get; private set; }
        public GuidanceOverlay()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            BackColor = Color.Fuchsia; TransparencyKey = Color.Fuchsia; DoubleBuffered = true; AutoScaleMode = AutoScaleMode.None;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams p = base.CreateParams; p.ExStyle |= 0x00080000 | 0x00000020 | 0x08000000 | 0x80; return p; } }
        protected override void WndProc(ref Message m) { if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; } base.WndProc(ref m); }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CaptureExcluded = false;
            // Older Windows treats 0x11 as a black capture surface. Only opt in
            // when exclusion (rather than blanking) is supported.
            Version version = Environment.OSVersion.Version;
            if (version.Major > 10 || (version.Major == 10 && version.Build >= 19041))
                try { CaptureExcluded = Native.SetWindowDisplayAffinity(Handle, 0x11); }
                catch (EntryPointNotFoundException) { }
        }
        public void UpdateGuidance(Rectangle bounds, List<GuidanceMark> next)
        {
            bool changed = Bounds.Size != bounds.Size || !GuidanceDrawing.SameMarks(marks, next);
            if (Bounds != bounds) Bounds = bounds;
            if (changed) marks = next;
            if (!Visible) Show();
            if (changed) Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e) { GuidanceDrawing.Draw(e.Graphics, marks); }
    }

    public sealed partial class PuzzleSuiteForm : Form
    {
        private readonly ComboBox windows = new ComboBox();
        private readonly CheckBox automatic = new KavrosCheckBox(), draw = new KavrosCheckBox(), reverse = new KavrosCheckBox();
        private readonly CheckBox towerCountdown = new KavrosCheckBox { Checked = true }, towerSkipBlank = new KavrosCheckBox();
        private readonly CheckBox wholeRowClicks=new KavrosCheckBox();
        private readonly ToolTip sliderTips=new ToolTip();
        private readonly Label detected = new PaintedLabel { Renderer = StatusText.DrawDetection }, summary = new Label(), status = new Label(), connection = new PaintedLabel { Renderer = StatusText.DrawConnection };
        private readonly PictureBox preview = new PreviewBox();
        private readonly SuiteCanvas canvas = new SuiteCanvas();
        private readonly MasterClueView masterView = new MasterClueView();
        private Label instructions;
        private readonly GuidanceOverlay overlay = new GuidanceOverlay();
        private readonly CaptureSearch captureSearch = new CaptureSearch();
        private readonly OverlayPreferences preferences=new OverlayPreferences();
        private readonly CheckBox floatingEnabled=new KavrosCheckBox();
        private readonly CheckBox checkUpdates=new KavrosCheckBox();
        private FloatingCluePanel floatingPanel;
        private CompassSession compassSession;
        private NotifyIcon tray;
        private readonly Timer timer = new Timer();
        private readonly BackgroundWorker analysisWorker = new BackgroundWorker();
        private PuzzleAnalysisEngine analysisEngine;
        private readonly object engineGate=new object();
        private readonly string assetPath;
        private DetectedPuzzle current, lastLocated;
        private readonly ClueRetention retainedClue=new ClueRetention();
        private volatile string analysisStage;
        private readonly ProgressBar scanProgress=new KavrosProgressBar();
        private string pendingKey;
        private List<GuidanceMark> marks = new List<GuidanceMark>();
        private bool busy, offline;
        private KnotPuzzle knotHistory;
        private List<int> slidingMoves;
        private int[] slidingBoard;
        private string slidingTheme;
        private int slidingBatchNumber=1;
        private List<GameWindow> availableWindows = new List<GameWindow>();
        private GameWindow attachedWindow;
        private bool refreshingWindows, requestedScan;
        private DateTime nextDiscovery = DateTime.MinValue;
        private string captureDetails = "No capture attempted yet.";
        private string puzzleDetails = "No puzzle detected yet.";
        private CancellationTokenSource analysisCancellation;
        private int sourceGeneration;
        private bool closing;
        private DateTime nextRead = DateTime.MinValue;
        private AnalysisJob activeJob, pendingJob;
        private Bitmap diagnosticCapture, offlineSource;
        private DetectedPuzzle panelPuzzle;
        private string panelText, panelStage;
        private bool panelSaved;

        public bool IsAnalyzing { get { return busy || pendingJob!=null; } }
        private sealed class AnalysisJob
        {
            public Bitmap Image;
            public int Generation;
            public IntPtr Window;
            public Point CaptureOffset;
            public bool Stable, Offline, Reverse;
            public bool WholeRows;
            public bool Cropped;
            public Size ClientSize;
            public DateTime CapturedAt;
        }

        public PuzzleSuiteForm(bool startWithPanel=true)
        {
            Text = "Kavros Clue Helper · " + AppBranding.DisplayVersion;
            Icon = AppBranding.LoadScroll();
            ClientSize = new Size(516, 1004); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
            BackColor = KavrosTheme.Bg; ForeColor = KavrosTheme.Text; Font = KavrosTheme.Ui(9F, false);
            Label title = LabelAt("CLUE HELPER", 90, 27, 400, 30); title.Font = KavrosTheme.Display(16F); title.UseMnemonic = false;
            Label subtitle = LabelAt("Easy / Medium / Hard / Elite / Master / Puzzle helpers", 92, 58, 400, 18); subtitle.ForeColor = KavrosTheme.TextFaint; subtitle.Font = KavrosTheme.Ui(8.5F, false);
            connection.SetBounds(16, 84, 484, 22); connection.Font = new Font("Segoe UI", 8F); connection.ForeColor = Color.Gold; connection.Text = "Looking for the Kavros game window..."; Controls.Add(connection);
            windows.SetBounds(28, 134, 355, 28); windows.DropDownStyle = ComboBoxStyle.DropDownList; KavrosTheme.StyleCombo(windows); Controls.Add(windows);
            ButtonAt("Refresh", 393, 134, 95, RefreshWindows);
            ButtonAt("Scan now", 28, 168, 80, RequestScan, ButtonTone.Primary);
            ButtonAt("Open image", 116, 168, 110, OpenScreenshot);
            AllowDrop = true;
            DragEnter += delegate(object sender, DragEventArgs e) { e.Effect = DroppedImage(e.Data) == null ? DragDropEffects.None : DragDropEffects.Copy; };
            DragDrop += delegate(object sender, DragEventArgs e)
            {
                string path = DroppedImage(e.Data);
                if (path != null) OpenImageFile(path);
            };
            sliderTips.SetToolTip(preview, "Drop a PNG, JPEG or BMP here, or press Ctrl+V to paste a screenshot.");
            ButtonAt("Examples", 234, 168, 78, ShowExamples);
            ButtonAt("Save image", 320, 168, 84, SaveCapture);
            ButtonAt("Details", 412, 168, 76, CopyDetails);
            automatic.Text = "Auto detect & follow"; automatic.SetBounds(28, 238, 220, 24); Controls.Add(automatic);
            draw.Text = "Show overlay"; draw.Checked = true; draw.SetBounds(264, 238, 224, 24); Controls.Add(draw);
            CheckBox pin = new KavrosCheckBox { Text = "Pin panel", Left = 28, Top = 262, Width = 220, Height = 24 }; pin.CheckedChanged += delegate { TopMost = pin.Checked; }; Controls.Add(pin);
            automatic.CheckedChanged += delegate
            {
                if(offline){retainedClue.Reset();if(automatic.Checked&&offlineSource!=null){offlineSource.Dispose();offlineSource=null;}}
                offline=false;pendingKey=null;nextRead=DateTime.MinValue;CancelAnalysis();overlay.Hide();
                if(automatic.Checked)
                {
                    captureSearch.Reset();lastLocated=null;current=null;marks.Clear();requestedScan=false;
                    canvas.Puzzle=null;canvas.Invalidate();ShowMasterView(false);
                    status.Text="Automatic reading resumed. Searching the full Kavros window.";
                }
                else status.Text="Automatic reading paused. Use Scan now for one capture.";
            };
            draw.CheckedChanged += delegate { ShowGuidance(); };
            windows.SelectedIndexChanged += delegate
            {
                if(!refreshingWindows)
                {
                    offline=false;CancelAnalysis();captureSearch.Reset();lastLocated=null;current=null;pendingKey=null;marks.Clear();overlay.Hide();
                    canvas.Puzzle=null;canvas.Invalidate();ShowMasterView(false);AttachWindow();nextRead=DateTime.MinValue;
                    requestedScan=SelectedWindow!=IntPtr.Zero;
                }
            };

            detected.SetBounds(16, 304, 484, 34); detected.Font = new Font("Segoe UI", 12F, FontStyle.Bold); detected.ForeColor = KavrosTheme.Ok; detected.Text = "Waiting for a puzzle"; Controls.Add(detected);
            preview.SetBounds(28, 366, 460, 222); preview.SizeMode = PictureBoxSizeMode.Zoom; preview.BackColor = KavrosTheme.BgDeep; Controls.Add(preview);
            canvas.SetBounds(28, 602, 260, 235); Controls.Add(canvas);
            instructions = new PaintedLabel { Text = InstructionsText(), Left = 300, Top = 602, Width = 188, Height = 120, Renderer = StatusText.DrawLegend }; Controls.Add(instructions);
            masterView.SetBounds(28,366,460,471); masterView.Visible=false; Controls.Add(masterView);
            summary.SetBounds(16, 857, 484, 36); summary.Font = KavrosTheme.Ui(9F, true); summary.ForeColor = KavrosTheme.Ok; summary.Text = "Open a clue or puzzle and keep it visible in Kavros."; Controls.Add(summary);
            status.SetBounds(16, 893, 484, 32); status.ForeColor = KavrosTheme.TextSoft; status.Text = "Ctrl+Shift+L: scan  /  Ctrl+Shift+H: hide overlay"; Controls.Add(status);
            scanProgress.SetBounds(16,927,484,3);scanProgress.Style=ProgressBarStyle.Marquee;scanProgress.MarqueeAnimationSpeed=25;scanProgress.Visible=false;Controls.Add(scanProgress);
            reverse.Text = "Reverse lockbox cycle"; reverse.SetBounds(300, 772, 188, 24); Controls.Add(reverse);
            reverse.CheckedChanged += delegate { if (current != null && current.Kind == PuzzleKind.Lockbox) SolveAndShow(current); };
            towerCountdown.Text = "Tower countdown"; towerCountdown.SetBounds(300, 724, 188, 24); Controls.Add(towerCountdown);
            towerSkipBlank.Text = "5 wraps straight to 1"; towerSkipBlank.SetBounds(300, 748, 188, 24); Controls.Add(towerSkipBlank);
            towerCountdown.CheckedChanged += delegate { instructions.Text = InstructionsText(); towerSkipBlank.Enabled = towerCountdown.Checked; if (current != null && current.Kind == PuzzleKind.Towers) SolveAndShow(current); };
            towerSkipBlank.CheckedChanged += delegate { if (current != null && current.Kind == PuzzleKind.Towers) SolveAndShow(current); };
            SetupShortestSlider();
            wholeRowClicks.Text="Whole-row slider clicks";wholeRowClicks.SetBounds(300,796,188,24);wholeRowClicks.Checked=preferences.WholeRowClicks;Controls.Add(wholeRowClicks);
            sliderTips.SetToolTip(wholeRowClicks,"Enable only if clicking farther along the empty tile's row or column slides the whole line in Kavros.");
            wholeRowClicks.CheckedChanged+=delegate
            {
                preferences.WholeRowClicks=wholeRowClicks.Checked;preferences.Save();slidingBatchNumber=1;
                if(current!=null&&current.Kind==PuzzleKind.Sliding || IsAnalyzing&&analysisStage=="Optimising slider route...")
                {
                    slidingBoard=null;slidingMoves=null;slidingTheme=null;
                    if(offline){if(offlineSource!=null)StartOffline(new Bitmap(offlineSource));}
                    else RequestScan();
                }
            };

            assetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
            if (!Directory.Exists(assetPath)) throw new Exception("The assets folder is missing. Extract the entire ZIP before running the program.");
            ThreadPool.QueueUserWorkItem(delegate{try{EnsureEngine();}catch(Exception){/* Scan jobs report any asset error on the panel. */}});
            compassSession=new CompassSession(assetPath);
            floatingPanel=new FloatingCluePanel(assetPath,compassSession,preferences);
            floatingPanel.SettingsRequested+=ShowSettings;
            floatingPanel.ScanRequested+=RequestScan;
            floatingPanel.PauseRequested+=delegate{automatic.Checked=!automatic.Checked;};
            floatingPanel.ExitRequested+=ExitHelper;
            floatingPanel.QuickSliderRequested += UseQuickRoute;
            floatingPanel.ClearRequested+=delegate{CancelAnalysis();retainedClue.Clear(current);compassSession.NewClue();ShowMasterView(false);masterView.SetClue(null,assetPath);canvas.Puzzle=null;canvas.Lines=null;canvas.Values=null;canvas.Invalidate();captureSearch.ForceFullWindow();nextRead=DateTime.MinValue;UpdateFloatingPanel();};
            floatingPanel.UserMoved+=delegate{Rectangle game=SelectedWindow==IntPtr.Zero?Rectangle.Empty:Native.ClientBounds(SelectedWindow);preferences.Offset=new Point(floatingPanel.Left-game.Left,floatingPanel.Top-game.Top);preferences.Save();};
            floatingEnabled.Text="Floating clue panel";floatingEnabled.SetBounds(16,939,190,26);floatingEnabled.Checked=preferences.Enabled;Controls.Add(floatingEnabled);
            floatingEnabled.CheckedChanged+=delegate{preferences.Enabled=floatingEnabled.Checked;preferences.Save();UpdateFloatingPanel();};
            ButtonAt("Open panel",232,938,128,OpenPanel);
            ButtonAt("Exit helper",372,938,128,ExitHelper);
            checkUpdates.Text="Check for updates at startup";checkUpdates.SetBounds(16,972,255,26);checkUpdates.Checked=preferences.CheckForUpdates;Controls.Add(checkUpdates);
            sliderTips.SetToolTip(checkUpdates,"When this is on, startup asks GitHub whether a newer release exists.");
            checkUpdates.CheckedChanged+=delegate{preferences.CheckForUpdates=checkUpdates.Checked;preferences.Save();};
            ButtonAt("Check for updates",305,971,195,delegate{AppUpdates.Check(this,true);});
            ButtonAt("What's new",16,1012,160,ToggleWhatsNew);
            ClientSize=new Size(ClientSize.Width,1048);
            AppUpdates.Present=ShowToast;
            var trayMenu=new ContextMenuStrip();trayMenu.Items.Add("Open panel",null,delegate{OpenPanel();});trayMenu.Items.Add("Settings",null,delegate{ShowSettings();});trayMenu.Items.Add("Exit helper",null,delegate{ExitHelper();});
            AppBranding.ArrangeSettings(this);
            KavrosTheme.AddMainCards(this);
            tray=new NotifyIcon{Icon=this.Icon,Text="Kavros Clue Helper",ContextMenuStrip=trayMenu,Visible=true};tray.DoubleClick+=delegate{ShowSettings();};
            Shown+=delegate{if(startWithPanel&&preferences.Enabled){Hide();UpdateFloatingPanel();}};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!closing&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();OpenPanel();}};
            analysisWorker.DoWork += AnalyzeFrame;
            analysisWorker.RunWorkerCompleted += AnalysisCompleted;
            RefreshWindows();
            automatic.Checked = true;
            InitializeTracking();
            timer.Interval = 50; timer.Tick += delegate { TickReader(); }; timer.Start();
            FormClosed += delegate { closing=true; CancelAnalysis(); StopTracking(); timer.Stop(); timer.Dispose(); overlay.Dispose();floatingPanel.Dispose();tray.Visible=false;tray.Dispose();trayMenu.Dispose();if (Icon != null) Icon.Dispose(); if (preview.Image != null) preview.Image.Dispose(); if(diagnosticCapture!=null) diagnosticCapture.Dispose(); if(offlineSource!=null) offlineSource.Dispose(); Native.UnregisterHotKey(Handle, 11); Native.UnregisterHotKey(Handle, 12); };
        }
        private Panel toastBar; private Label toastText; private Button toastAction; private Timer toastTimer; private TextBox whatsNew; private Panel whatsNewPanel; private Action toastClick;
        internal void ShowToast(string message, bool error, string action, Action click)
        {
            if (toastBar == null)
            {
                toastBar = new Panel { Name = "UpdateToast", Height = 40, Dock = DockStyle.Bottom, Visible = false };
                toastText = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
                toastAction = new Button { Dock = DockStyle.Right, Width = 120, FlatStyle = FlatStyle.Flat, Visible = false };
                toastAction.Click += delegate { if (toastClick != null) toastClick(); toastBar.Visible = false; if (toastTimer != null) toastTimer.Stop(); };
                toastBar.Controls.Add(toastText); toastBar.Controls.Add(toastAction);
                toastTimer = new Timer { Interval = 5000 };
                toastTimer.Tick += delegate { toastTimer.Stop(); toastBar.Visible = false; };
                Controls.Add(toastBar);
            }
            toastBar.BackColor = error ? KavrosTheme.Danger : KavrosTheme.Panel;
            toastText.ForeColor = error ? KavrosTheme.GoldInk : KavrosTheme.Text;
            toastText.Text = message;
            toastClick = click;
            toastAction.Text = action ?? "";
            toastAction.Visible = action != null;
            toastAction.BackColor = KavrosTheme.Gold;
            toastAction.ForeColor = KavrosTheme.GoldInk;
            toastBar.Visible = true; toastBar.BringToFront();
            toastTimer.Stop();
            // Everything fades after 5 seconds except the update offer, which waits for a click.
            if (click == null || action != "Update now") toastTimer.Start();
            if (floatingPanel != null && !floatingPanel.IsDisposed) floatingPanel.ShowBanner(message, error);
        }
        internal void ShowWhatsNew()
        {
            ShowSettings();
            if (whatsNewPanel == null)
            {
                whatsNewPanel = new Panel { BackColor = KavrosTheme.Bg, Visible = false };
                Panel header = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = KavrosTheme.BgRaise };
                Label title = new Label { Text = "What's new", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), ForeColor = KavrosTheme.Gold, Font = KavrosTheme.Ui(10F, true) };
                Button close = new OpaqueButton { Text = "Close  X", Dock = DockStyle.Right, Width = 96, FlatStyle = FlatStyle.Flat, BackColor = KavrosTheme.Button, ForeColor = ForeColor, Tone = ButtonTone.Secondary };
                close.FlatAppearance.BorderColor = KavrosTheme.ButtonLine;
                close.Click += delegate { HideWhatsNew(); };
                header.Controls.Add(title); header.Controls.Add(close);
                whatsNew = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = KavrosTheme.Ui(9F, false), BackColor = KavrosTheme.Bg, ForeColor = KavrosTheme.Text };
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
                whatsNew.Text = File.Exists(path) ? File.ReadAllText(path) : "What's new is listed in CHANGELOG.md next to the program.";
                // Fill first, then the header, so docking keeps the header on top.
                whatsNewPanel.Controls.Add(whatsNew); whatsNewPanel.Controls.Add(header);
                Controls.Add(whatsNewPanel);
            }
            whatsNewPanel.Bounds = new Rectangle(16, 88, Math.Max(40, ClientSize.Width - 32), Math.Max(40, ClientSize.Height - 140));
            whatsNewPanel.Visible = true; whatsNewPanel.BringToFront();
            whatsNew.SelectionStart = 0; whatsNew.SelectionLength = 0;
        }
        private void HideWhatsNew() { if (whatsNewPanel != null) whatsNewPanel.Visible = false; }
        private void ToggleWhatsNew() { if (whatsNewPanel != null && whatsNewPanel.Visible) HideWhatsNew(); else ShowWhatsNew(); }
        private void ShowSettings(){if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Show();Activate();}
        internal void CheckForUpdatesOnStartup(){if(preferences.CheckForUpdates)AppUpdates.Check(this,false);}
        internal void PrepareForUpdate(){closing=true;}
        internal void SetUpdateStatus(string message){status.Text=message;}
        private void OpenPanel(){draw.Checked=true;floatingEnabled.Checked=true;UpdateFloatingPanel();if(SelectedWindow==IntPtr.Zero||!Native.IsIconic(SelectedWindow)){floatingPanel.Show();floatingPanel.Activate();}}
        private void ExitHelper(){closing=true;Close();}
        private void UpdateFloatingPanel()
        {
            if(closing||floatingPanel==null||floatingPanel.IsDisposed)return;
            bool exists=SelectedWindow!=IntPtr.Zero&&Native.IsWindow(SelectedWindow);
            if(floatingPanel.IsHandleCreated)try{Native.SetOwner(floatingPanel.Handle,exists?SelectedWindow:IntPtr.Zero);}catch(System.DllNotFoundException){}catch(EntryPointNotFoundException){}
            bool minimised=exists&&Native.IsIconic(SelectedWindow);
            bool foreground=exists&&WindowLocator.IsForeground(SelectedWindow);
            if(!draw.Checked||!FloatingPanelLayout.Visible(floatingEnabled.Checked,offline,exists,minimised,foreground,floatingPanel.ContainsFocus,ContainsFocus)){floatingPanel.Hide();return;}
            floatingPanel.SetPaused(!automatic.Checked);
            DetectedPuzzle displayed=retainedClue.Display(current);
            bool saved = current == null && displayed != null;
            string message = displayed != null ? summary.Text : exists ? "Open a clue or puzzle in Kavros." : "Waiting for Kavros.";
            string stage = busy && displayed == null ? analysisStage : null;
            if (displayed != panelPuzzle || message != panelText || saved != panelSaved || stage != panelStage)
            {
                if (displayed != null) floatingPanel.SetPuzzle(displayed, message);
                else floatingPanel.SetWaiting(message);
                floatingPanel.SetRetained(saved);
                floatingPanel.SetReading(stage);
                scanProgress.Visible = stage != null;
                panelPuzzle = displayed; panelText = message; panelSaved = saved; panelStage = stage;
            }
            floatingPanel.RefreshCompass(false);
            Rectangle game=exists?Native.ClientBounds(SelectedWindow):Rectangle.Empty;
            Rectangle desktop=exists?Screen.FromRectangle(game).WorkingArea:Screen.PrimaryScreen.WorkingArea;
            Rectangle placement=FloatingPanelLayout.Place(game,floatingPanel.Size,preferences.Offset,desktop);
            if(!floatingPanel.IsDragging&&floatingPanel.Bounds!=placement)floatingPanel.Bounds=placement;
            floatingPanel.TopMost=true;
            if(!floatingPanel.Visible)floatingPanel.Show();
        }
        private void ShowMasterView(bool show)
        {
            masterView.Visible=show;
            preview.Visible=canvas.Visible=instructions.Visible=reverse.Visible=towerCountdown.Visible=towerSkipBlank.Visible=wholeRowClicks.Visible=!show;
        }
        private string InstructionsText()
        {
            return "Automatic detection\n\nKeep the full puzzle visible.\n\nGreen = next action\nGold = other actions\n" + (towerCountdown.Checked ? "Towers = clicks left\nGreen 0 = square complete" : "Towers = target heights");
        }
        private Label LabelAt(string text, int x, int y, int w, int h) { Label l = new Label { Text = text, Left = x, Top = y, Width = w, Height = h }; Controls.Add(l); return l; }
        private void ButtonAt(string text, int x, int y, int w, Action action, ButtonTone tone = ButtonTone.Secondary)
        {
            Button b = new OpaqueButton { Text = text, Left = x, Top = y, Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = KavrosTheme.Button, ForeColor = ForeColor, Tone = tone }; b.FlatAppearance.BorderColor = KavrosTheme.ButtonLine;
            b.Click += delegate { try { action(); } catch (Exception ex) { overlay.Hide(); status.Text = ex.Message; } }; Controls.Add(b);
        }
        private IntPtr SelectedWindow { get { return attachedWindow == null ? IntPtr.Zero : attachedWindow.Handle; } }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            KavrosTheme.ApplyWindowChrome(Handle);
            bool scanKey = Native.RegisterHotKey(Handle, 11, 0x4006, (uint)Keys.L);
            bool drawKey = Native.RegisterHotKey(Handle, 12, 0x4006, (uint)Keys.H);
            if (!scanKey || !drawKey) status.Text = "Ctrl+Shift+" + (!scanKey ? "L" : "H") + " is taken by another program (or another copy of the helper). Use the buttons instead.";
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312) { if (m.WParam.ToInt32() == 11) RequestScan(); if (m.WParam.ToInt32() == 12) draw.Checked = !draw.Checked; }
            base.WndProc(ref m);
        }
        private void RefreshWindows()
        {
            GameWindow previousChoice = windows.SelectedItem as GameWindow;
            bool manual = previousChoice != null && !previousChoice.Automatic;
            IntPtr selected = manual ? previousChoice.Handle : IntPtr.Zero;
            availableWindows = WindowLocator.Enumerate(Process.GetCurrentProcess().Id);
            refreshingWindows = true;
            windows.BeginUpdate();
            try
            {
                windows.Items.Clear();
                windows.Items.Add(new GameWindow { Automatic = true, Title = "Auto - find the Kavros game window" });
                foreach (GameWindow window in availableWindows) windows.Items.Add(window);
                int index = manual ? availableWindows.FindIndex(w => w.Handle == selected) : -1;
                windows.SelectedIndex = index < 0 ? 0 : index + 1;
            }
            finally { windows.EndUpdate(); refreshingWindows = false; }
            AttachWindow();
            nextDiscovery = DateTime.UtcNow.AddSeconds(2.5);
        }
        private void AttachWindow()
        {
            GameWindow choice = windows.SelectedItem as GameWindow;
            uint boundProcess = attachedWindow == null ? 0 : attachedWindow.ProcessId;
            GameWindow next = choice != null && !choice.Automatic && Native.IsWindow(choice.Handle) ? choice : WindowLocator.Choose(availableWindows, SelectedWindow, boundProcess, Native.GetForegroundWindow());
            IntPtr previous = SelectedWindow;
            attachedWindow = next;
            if (SelectedWindow != previous)
            {
                if(compassSession!=null)compassSession.NewClue();
                retainedClue.Reset();
                CancelAnalysis();
                captureSearch.Reset(); nextRead=DateTime.MinValue;
                lastLocated = null; current = null; pendingKey = null; knotHistory = null; slidingMoves = null; slidingBoard = null; marks.Clear();
                canvas.Puzzle = null; canvas.Values = null; canvas.Invalidate(); overlay.Hide();
                ShowMasterView(false); masterView.SetClue(null,assetPath);
                captureDetails = "No capture from this window yet."; puzzleDetails = "No puzzle detected yet.";
            }
            if (next == null)
            {
                bool launcher = availableWindows.Any(w => WindowLocator.IsLauncher(w) && ((w.Title + " " + w.ProcessName).IndexOf("kavros", StringComparison.OrdinalIgnoreCase) >= 0));
                connection.ForeColor = Color.Gold; connection.Text = launcher ? "Kavros launcher found - waiting for the game client." : "Looking for Kavros - it can be opened after this helper.";
                if (!offline) { detected.Text = "Waiting for Kavros"; status.Text = "The window list refreshes automatically. You can also pick a window manually."; }
            }
            else { connection.ForeColor = Color.FromArgb(87, 220, 181); connection.Text = "Game found: " + next.Title; }
        }
        private void TickReader()
        {
            ShowGuidance();
            PollShortestSlider();
            TickTracking();
            if (!windows.DroppedDown && (DateTime.UtcNow >= nextDiscovery || (SelectedWindow != IntPtr.Zero && !Native.IsWindow(SelectedWindow)))) RefreshWindows();
            else if ((windows.SelectedItem as GameWindow) != null && ((GameWindow)windows.SelectedItem).Automatic) AttachWindow();
            if (busy || DateTime.UtcNow < nextRead) return;
            if (requestedScan)
            {
                if (SelectedWindow == IntPtr.Zero) { overlay.Hide(); status.Text = "Waiting for Kavros to reopen before the requested scan."; }
                else { Scan(false); }
            }
            else if (automatic.Checked && !offline) Scan(true);
                    }
        private void RequestScan()
        {
            CancelAnalysis(); nextRead=DateTime.MinValue;
            captureSearch.ForceFullWindow(); lastLocated=null;current=null;marks.Clear();canvas.Puzzle=null;canvas.Invalidate();ShowMasterView(false);
            if (offlineSource != null) { offlineSource.Dispose(); offlineSource = null; }
            offline = false; RefreshWindows();

            requestedScan = true; pendingKey = null; overlay.Hide();
            status.Text = "Reading the visible Kavros window...";
            if(!busy) Scan(false);
        }
        private void CopyDetails()
        {
            RefreshWindows();
            string report = WindowLocator.Describe(availableWindows, attachedWindow) + "Automatic reading: " + automatic.Checked + "\r\nScreenshot mode: " + offline + "\r\nCapture: " + captureDetails + "\r\nPuzzle: " + puzzleDetails + "\r\nState: " + status.Text+"\r\nMinimap: "+(positionReader==null?"Not started":positionReader.Diagnostics);
            Clipboard.SetText(report); status.Text = "Troubleshooting details copied. Paste them into our chat.";
        }
        private void Scan(bool stable)
        {
            if (busy) return;
            IntPtr window = SelectedWindow;
            if (window == IntPtr.Zero || !Native.IsWindow(window)) { overlay.Hide(); status.Text = "Waiting for Kavros. The game window will be selected automatically."; return; }
            if (Native.IsIconic(window)) { overlay.Hide(); status.Text = "Game found, but minimized. Restore Kavros to read the puzzle."; return; }
            Rectangle bounds = Native.ClientBounds(window);
            Rectangle visible = CaptureSearch.VisibleClient(bounds, SystemInformation.VirtualScreen);
            if (visible.Width < 60 || visible.Height < 60) { overlay.Hide(); status.Text = "Bring the clue into view to capture it."; return; }
            Rectangle visibleScreen = visible; visibleScreen.Offset(bounds.Location);
            IntPtr overlayHandle=overlay.IsHandleCreated ? overlay.Handle : IntPtr.Zero;
            if(!GameVisibility.Any(visibleScreen,p=>WindowLocator.AtPoint(window,p,overlayHandle)))
            { overlay.Hide(); status.Text="Kavros is covered by another window. Keep the puzzle visible."; nextRead=DateTime.UtcNow.AddMilliseconds(300); return; }
            try
            {
                if (overlay.Visible && GuidanceDrawing.HideForCapture(overlay.CaptureExcluded, current == null ? PuzzleKind.None : current.Kind))
                { overlay.Hide(); Native.DwmFlush(); }
                Rectangle fullClient=new Rectangle(Point.Empty,bounds.Size);
                Rectangle captureArea=captureSearch.Choose(bounds.Size,lastLocated==null||!DetectionPolicy.IsBoardPuzzle(lastLocated.Kind) ? (Rectangle?)null : lastLocated.Bounds);
                if(lastLocated!=null&&lastLocated.Kind==PuzzleKind.Sliding&&captureArea!=fullClient)captureArea=Rectangle.Intersect(fullClient,Rectangle.Inflate(captureArea,20,20));
                captureArea = Rectangle.Intersect(captureArea, visible);
                if (captureArea.Width < 60 || captureArea.Height < 60) captureArea = visible;
                Rectangle screenArea=captureArea; screenArea.Offset(bounds.Location);
                Bitmap screenshot=ScreenReader.Capture(screenArea);
                // Never detect a puzzle in this helper's own screenshot preview
                // when the panel is sitting above the game.
                foreach(Form surface in new Form[]{this,floatingPanel})
                {
                    if(surface==null||!surface.Visible)continue;
                    Rectangle panelOverlap=Rectangle.Intersect(surface.Bounds,screenArea);
                    if(panelOverlap.Width>0 && panelOverlap.Height>0 && GameVisibility.Any(panelOverlap,p=>WindowLocator.AtPoint(surface.Handle,p,IntPtr.Zero)))
                    {
                        Rectangle cover=panelOverlap; cover.Offset(-screenArea.X,-screenArea.Y);
                        using(Graphics graphics=Graphics.FromImage(screenshot)) graphics.FillRectangle(Brushes.Black,cover);
                    }
                }
                bool cropped=captureArea!=visible;
                captureDetails=(cropped?"Puzzle region: ":"Full game window: ")+screenshot.Width+" x "+screenshot.Height+" pixels at "+DateTime.Now.ToString("HH:mm:ss")+"; offset "+captureArea.Location+"; background analysis";
                status.Text=cropped ? "Reading the current puzzle area..." : "Searching the full Kavros window...";
                requestedScan=false;
                QueueAnalysis(new AnalysisJob { Image=screenshot,Generation=sourceGeneration,Window=window,CaptureOffset=captureArea.Location,Cropped=cropped,ClientSize=bounds.Size,CapturedAt=DateTime.UtcNow,Stable=stable,Offline=false,Reverse=reverse.Checked,WholeRows=wholeRowClicks.Checked });
                ShowGuidance();
            }
            catch (Exception ex) { ClearUnreadableFrame(); status.Text = ex.Message; }
        }
        private void ClearUnreadableFrame()
        {
            current=null;lastLocated=null;marks.Clear();overlay.Hide();captureSearch.ForceFullWindow();
            if(compassSession!=null)compassSession.Observe(null,DateTime.UtcNow);
        }
        private void CancelAnalysis()
        {
            waitingSlider = null;
            quickForThisSlider = false;
            if (analysisEngine != null) analysisEngine.Proofs.Cancel();
            sourceGeneration++;
            analysisStage=null;
            if(compassSession!=null)compassSession.Observe(null,DateTime.UtcNow);
            if(analysisCancellation!=null) analysisCancellation.Cancel();
            if(pendingJob!=null) { pendingJob.Image.Dispose(); pendingJob=null; }
        }
        private void QueueAnalysis(AnalysisJob job)
        {
            if(closing) { job.Image.Dispose(); return; }
            if(analysisWorker.IsBusy)
            {
                if(analysisCancellation!=null) analysisCancellation.Cancel();
                if(pendingJob!=null) pendingJob.Image.Dispose(); pendingJob=job; return;
            }
            busy=true;analysisStage=null; activeJob=job; analysisCancellation=new CancellationTokenSource();
            analysisWorker.RunWorkerAsync(job);
        }
        private void AnalyzeFrame(object sender, DoWorkEventArgs args)
        {
            AnalysisJob job=(AnalysisJob)args.Argument;
            ThreadPriority priority=Thread.CurrentThread.Priority;
            bool retained=false;
            try
            {
                try { Thread.CurrentThread.Priority=ThreadPriority.BelowNormal; } catch { }
                if(analysisEngine==null)analysisStage="Preparing reader...";
                PuzzleAnalysisEngine engine=EnsureEngine();
                engine.Progress=message=>analysisStage=message;
                analysisStage=null;
                AnalysisResult result=engine.Analyze(job.Image,job.Generation,job.Reverse,analysisCancellation.Token,!job.Offline,job.WholeRows, job.Offline ? (Point?)null : job.CaptureOffset);
                if(result.Plan==null || (result.Plan.Puzzle.Kind==PuzzleKind.Towers && result.Plan.Towers==null))
                { result.DiagnosticImage=job.Image; retained=true; }
                args.Result=result;
            }
            catch(OperationCanceledException) { args.Cancel=true; }
            finally { if(!retained) job.Image.Dispose(); try { Thread.CurrentThread.Priority=priority; } catch { } }
        }
        private PuzzleAnalysisEngine EnsureEngine(){lock(engineGate){if(analysisEngine==null)analysisEngine=new PuzzleAnalysisEngine(assetPath);return analysisEngine;}}
        private void AnalysisCompleted(object sender, RunWorkerCompletedEventArgs args)
        {
            AnalysisJob job=activeJob; activeJob=null; busy=false;analysisStage=null;
            if(analysisCancellation!=null) { analysisCancellation.Dispose(); analysisCancellation=null; }
            AnalysisResult result=!args.Cancelled && args.Error==null ? args.Result as AnalysisResult : null;
            try
            {
                if(closing || IsDisposed || job==null || job.Generation!=sourceGeneration) return;
                if(args.Error!=null) { ClearUnreadableFrame(); status.Text="Could not read this frame: "+args.Error.Message; nextRead=DateTime.UtcNow.AddMilliseconds(250); return; }
                if(result==null) {compassSession.Observe(null,DateTime.UtcNow);return;}
                if(diagnosticCapture!=null) { diagnosticCapture.Dispose(); diagnosticCapture=null; }
                diagnosticCapture=result.DiagnosticImage; result.DiagnosticImage=null;
                nextRead=job.CapturedAt.AddMilliseconds(result.Plan==null?75:125);
                captureDetails += "; last analysis "+result.Milliseconds+" ms"+(result.Unchanged?"; cached":"");
                if(result.Pending&&current!=null&&current.Kind==PuzzleKind.Sliding)
                {status.Text="Waiting for the tiles to settle...";nextRead=DateTime.UtcNow.AddMilliseconds(75);ShowGuidance();return;}
                if(result.Pending&&current!=null&&DetectionPolicy.IsBoardPuzzle(current.Kind)&&current.Kind!=PuzzleKind.Knot&&current.Kind!=PuzzleKind.Sliding)
                {status.Text="Puzzle covered. Keeping the last reading.";nextRead=DateTime.UtcNow.AddMilliseconds(120);ShowGuidance();return;}
                if(result.Pending&&current!=null&&current.Kind==PuzzleKind.Knot)
                {marks=marks.Select(m=>new GuidanceMark{Box=m.Box,Text=m.Text,Colour=Color.Silver,Corner=m.Corner,Compact=m.Compact,Outline=m.Outline}).ToList();summary.Text="Updating counts - grey numbers show the last confirmed reading.";nextRead=DateTime.UtcNow.AddMilliseconds(75);ShowGuidance();return;}
                if(!job.Offline && captureSearch.Complete(job.Cropped,DetectionPolicy.CanTrack(result.Plan)))
                {
                    compassSession.Observe(null,DateTime.UtcNow);
                    lastLocated=null;current=null;pendingKey=null;marks.Clear();canvas.Puzzle=null;canvas.Invalidate();ShowMasterView(false);overlay.Hide();
                    captureSearch.ForceFullWindow();requestedScan=true;nextRead=DateTime.MinValue;
                    detected.Text="Rechecking the full game window";
                    status.Text="The previous puzzle area changed. Searching the whole window now.";
                    return;
                }
                if(!job.Offline && result.Plan!=null)
                {
                    Rectangle client=Native.ClientBounds(job.Window);
                    Rectangle area=result.Plan.Puzzle.Bounds;area.Offset(job.CaptureOffset);area.Offset(client.Location);
                    IntPtr visibleOverlay=overlay.IsHandleCreated ? overlay.Handle : IntPtr.Zero;
                    if(client.Size!=job.ClientSize)
                    {
                        compassSession.Observe(null,DateTime.UtcNow);
                        lastLocated=null;current=null;marks.Clear();overlay.Hide();captureSearch.ForceFullWindow();requestedScan=true;nextRead=DateTime.MinValue;
                        detected.Text="Rechecking the resized game window";status.Text="Window size changed. Recalculating the capture area.";return;
                    }
                    if(!GameVisibility.Clear(area,point=>WindowLocator.AtPoint(job.Window,point,visibleOverlay)))
                    {
                        if(current!=null&&DetectionPolicy.IsBoardPuzzle(current.Kind))
                        {status.Text="Puzzle covered. Keeping the last reading.";nextRead=DateTime.UtcNow.AddMilliseconds(120);ShowGuidance();return;}
                        compassSession.Observe(null,DateTime.UtcNow);
                        lastLocated=null;current=null;marks.Clear();overlay.Hide();captureSearch.ForceFullWindow();
                        detected.Text="Keep the puzzle fully visible";
                        status.Text="The puzzle moved or is covered. Move the helper away from the puzzle.";
                        return;
                    }
                }
                if(result.Preview!=null)
                { Image old=preview.Image; preview.Image=result.Preview; result.Preview=null; if(old!=null) old.Dispose(); }
                if(result.Plan==null)
                {
                    retainedClue.Missing();
                    compassSession.Observe(null,DateTime.UtcNow);
                    lastLocated=null; pendingKey=null; current=null; marks.Clear(); canvas.Puzzle=null; canvas.Invalidate(); overlay.Hide();
                    bool savedMaster=retainedClue.Saved!=null&&retainedClue.Saved.Kind==PuzzleKind.EmoteClue;
                    ShowMasterView(savedMaster);
                    masterView.SetClue(savedMaster?retainedClue.Saved.Master:null,assetPath);
                    puzzleDetails="Capture succeeded; no supported puzzle matched."; detected.Text="Game captured / puzzle not recognised";
                    summary.Text=retainedClue.Saved==null?"Open a clue or puzzle. Text clues use Windows English OCR.":"Last clue saved. Guidance remains visible while you travel.";
                    status.Text=retainedClue.Saved==null?"Looking for a clue. Use Screenshot or Details if one is not recognised.":"New clues replace the saved card automatically. Clear removes it."; return;
                }
                DetectedPuzzle p=result.Plan.Puzzle.OffsetBy(Point.Empty);
                if(!job.Offline&&p.Kind==PuzzleKind.Sliding&&result.Milliseconds>=350)
                {
                    // The player can move tiles while a better route is being
                    // calculated. Re-read before showing any newly found route.
                    Rectangle screen=p.Bounds;screen.Offset(job.CaptureOffset);screen.Offset(Native.ClientBounds(job.Window).Location);
                    Rectangle localGrid=p.Grid;localGrid.Offset(-p.Bounds.Left,-p.Bounds.Top);
                    using(Bitmap freshImage=ScreenReader.Capture(screen))
                    {
                        DetectedPuzzle fresh=analysisEngine.ReadOnlySlider(freshImage,new DetectedPuzzle{Kind=PuzzleKind.Sliding,Bounds=new Rectangle(Point.Empty,freshImage.Size),Grid=localGrid,Scale=p.Scale});
                        if(fresh==null||!fresh.Board.SequenceEqual(p.Board))
                        {
                            List<int> remaining;
                            if(fresh!=null&&SlidingRoute.Advance(p.Board,result.Plan.Sliding,fresh.Board,out remaining))
                            {
                                int consumed=result.Plan.Sliding.Count-remaining.Count;
                                bool optimal = result.Plan.SlidingOptimal;
                                if(result.Plan.SlidingWholeRows&&SlidingRoute.CompletedClicks(p.Board,result.Plan.Sliding,consumed)!=SlidingRoute.ClickRoute(p.Board,result.Plan.Sliding.Take(consumed).ToList()).Count)optimal=false;
                                p.Board=fresh.Board;result.Plan=new PuzzlePlan { Puzzle=p, Sliding=remaining, SlidingWholeRows=result.Plan.SlidingWholeRows, SlidingOptimal=optimal };
                            }
                            else
                            {
                                retainedClue.Reset();current=null;lastLocated=null;marks.Clear();canvas.Puzzle=null;canvas.Invalidate();ShowMasterView(false);overlay.Hide();captureSearch.ForceFullWindow();requestedScan=true;nextRead=DateTime.MinValue;
                                status.Text="Tiles changed while finding the route. Reading the latest board...";return;
                            }
                        }
                    }
                }
                Rectangle body=p.Bounds,grid=p.Grid; body.Offset(job.CaptureOffset); grid.Offset(job.CaptureOffset);
                DetectedPuzzle puzzle=new DetectedPuzzle { Kind=p.Kind,Bounds=body,Grid=grid,Scale=p.Scale,Board=p.Board,Towers=p.Towers,Knot=p.Knot,Master=p.Master,Compass=p.Compass,Scan=p.Scan,Theme=p.Theme,Message=p.Message,Confidence=p.Confidence };
                puzzleDetails=puzzle.Kind+"; "+puzzle.Bounds+"; scale "+puzzle.Scale+"; confidence "+puzzle.Confidence.ToString("0.000");
                if(puzzle.Kind==PuzzleKind.Towers) puzzleDetails+="; clues "+String.Join(",",puzzle.Towers.Top.Concat(puzzle.Towers.Bottom).Concat(puzzle.Towers.Left).Concat(puzzle.Towers.Right));
                if(puzzle.Kind==PuzzleKind.Compass)puzzleDetails+="; "+puzzle.Compass.Heading+"; arrow pixels "+puzzle.Compass.Pixels+"; error "+(puzzle.Compass.Error*180/Math.PI).ToString("0.000")+" degrees; bearings "+compassSession.Bearings.Count;
                if(job.Stable && pendingKey!=puzzle.Key && DetectionPolicy.NeedsConfirmation(result.Plan)) { pendingKey=puzzle.Key; status.Text="Puzzle found - confirming the next frame..."; nextRead=DateTime.UtcNow.AddMilliseconds(100); return; }
                lastLocated=puzzle;
                retainedClue.Accept(puzzle);
                if(puzzle.Kind==PuzzleKind.Compass)compassSession.Observe(puzzle.Compass,job.Offline?DateTime.UtcNow:job.CapturedAt);
                else if(compassSession.HasHistory||compassSession.Current!=null)compassSession.NewClue();
                pendingKey=puzzle.Key; offline=job.Offline;
                if(!result.Unchanged || current==null || current.Bounds!=puzzle.Bounds || current.Grid!=puzzle.Grid || current.Key!=puzzle.Key) { current=puzzle; SolveAndShow(puzzle,result.Plan); }
                else ShowGuidance();
            }
            finally
            {
                if(result!=null) result.Dispose();
                if(pendingJob!=null && !closing)
                { AnalysisJob queued=pendingJob; pendingJob=null; if(queued.Generation==sourceGeneration) QueueAnalysis(queued); else queued.Image.Dispose(); }
            }
        }
        private void UpdatePreview(Bitmap source, Rectangle rectangle)
        {
            using (Bitmap crop = Pixels.Crop(source, rectangle, rectangle.Size))
            { Image old = preview.Image; preview.Image = crop == null ? null : (Image)crop.Clone(); if (old != null) old.Dispose(); }
        }
        private Rectangle Cell(DetectedPuzzle p, int index)
        {
            return GuidanceDrawing.Cell(p, index);
        }
        private void SolveAndShow(DetectedPuzzle puzzle, PuzzlePlan prepared = null)
        {
            List<GuidanceMark> previousKnotMarks=puzzle.Kind==PuzzleKind.Knot?marks:null;
            marks = new List<GuidanceMark>(); canvas.Puzzle = puzzle; canvas.Values = null; canvas.Next = -1; canvas.Lines = null;
            ShowMasterView(puzzle.Kind==PuzzleKind.EmoteClue);
            if(puzzle.Kind==PuzzleKind.Scan)
            {
                detected.Text=puzzle.Scan.Region.Tier.ToUpperInvariant()+" SCAN / "+puzzle.Scan.Region.Name.ToUpperInvariant();summary.Text="Use the floating scan map. Auto follow reads your minimap; enable Meerkat when active.";
                canvas.Lines=new[]{puzzle.Scan.Region.Name,puzzle.Scan.Region.Spots.Count+" known spots","Range: "+puzzle.Scan.Region.BaseRange+" paces","Use the floating map."};
                canvas.Invalidate();overlay.Hide();ShowGuidance();return;
            }
            if(puzzle.Kind==PuzzleKind.Compass)
            {
                detected.Text="ELITE COMPASS";summary.Text=compassSession.Summary;
                canvas.Lines=new[]{puzzle.Compass.Heading,"Use the floating panel","to record positions.","Reopened clues check old lines."};
                canvas.Invalidate();overlay.Hide();ShowGuidance();return;
            }
            if(puzzle.Kind==PuzzleKind.EmoteClue)
            {
                knotHistory=null;
                masterView.SetClue(puzzle.Master,assetPath);
                detected.Text=puzzle.Master.Tier.ToUpperInvariant()+" CLUE / "+puzzle.Master.Emote.ToUpperInvariant();
                summary.Text=puzzle.Master.Reference?puzzle.Master.Location:"Equip the listed items, then "+puzzle.Master.Emote.ToLowerInvariant()+" in the marked area.";
                status.Text=offline ? "Screenshot mode. Clue recognised locally." : "Clue recognised. Guidance switches automatically.";
                overlay.Hide();ShowGuidance(); return;
            }
            detected.Text = puzzle.Kind == PuzzleKind.Sliding ? "SLIDING BOX / " + puzzle.Theme.Replace('_', ' ') : puzzle.Kind == PuzzleKind.Knot ? "CELTIC KNOT" : puzzle.Kind.ToString().ToUpperInvariant();
            if (puzzle.Kind != PuzzleKind.Knot) knotHistory = null;
            if (puzzle.Kind == PuzzleKind.Lockbox)
            {
                Solution result = prepared==null ? LockboxSolver.Solve(puzzle.Board, reverse.Checked ? -1 : 1) : prepared.Lockbox;
                if (result == null) { overlay.Hide(); summary.Text = "No valid lockbox plan. Check the symbols."; canvas.Invalidate(); return; }
                canvas.Values = result.Clicks; canvas.Next = Array.FindIndex(result.Clicks, v => v > 0);
                for (int i = 0; i < 25; i++) if (result.Clicks[i] > 0) marks.Add(new GuidanceMark { Box = Cell(puzzle, i), Text = result.Clicks[i].ToString(), Colour = i == canvas.Next ? Color.Lime : Color.Gold, Corner = true });
                summary.Text = result.Total == 0 ? "All symbols match. Lockbox solved." : result.Total + " clicks. Each number is the click count for that tile.";
            }
            else if (puzzle.Kind == PuzzleKind.Towers)
            {
                int readable=puzzle.Towers.Top.Concat(puzzle.Towers.Bottom).Concat(puzzle.Towers.Left).Concat(puzzle.Towers.Right).Count(v=>v>0);
                if(readable<20) { overlay.Hide(); summary.Text="Towers found: "+readable+" of 20 clues read. Waiting for a clearer frame."; status.Text="Copy details includes the recognised clue numbers."; canvas.Invalidate(); return; }
                List<int[]> solutions = prepared==null ? TowerSolver.Solve(puzzle.Towers, false) : prepared.Towers;
                if (solutions == null || solutions.Count == 0) { overlay.Hide(); summary.Text = "No valid tower solution. Rescan the clue numbers."; canvas.Invalidate(); return; }
                canvas.Values = solutions[0];
                marks = GuidanceDrawing.TowerMarks(puzzle, solutions[0], towerCountdown.Checked, towerSkipBlank.Checked);
                bool complete = puzzle.Towers.Cells.SequenceEqual(solutions[0]);
                summary.Text = complete ? "Tower heights match. Press CHECK in the game." : towerCountdown.Checked ? "Corners show clicks left. Green 0 means that square is complete." : "Set each square to its small corner number, then press CHECK.";
            }
            else if (puzzle.Kind == PuzzleKind.Sliding)
            {
                if (!SlidingSolver.IsSolvable(puzzle.Board)) { overlay.Hide(); summary.Text = "Tile recognition gave an impossible board. Rescan before moving."; canvas.Invalidate(); return; }
                if (AwaitShortestSlider(puzzle, ref prepared)) return;
                FollowSlider(puzzle,prepared==null?null:prepared.Sliding);
                if (slidingMoves.Count == 0) summary.Text = "Sliding puzzle solved.";
                else
                {
                    List<int> clicks=wholeRowClicks.Checked?SlidingRoute.ClickRoute(puzzle.Board,slidingMoves):slidingMoves;
                    canvas.Next=clicks[0];
                    marks=GuidanceDrawing.SliderMarks(puzzle,clicks,slidingBatchNumber,1);
                    if(marks.Count>0){marks[0].Colour=KavrosTheme.Gold;marks[0].Pulse=true;int gap=Array.IndexOf(puzzle.Board,24);marks[0].ArrowX=gap%5-clicks[0]%5;marks[0].ArrowY=gap/5-clicks[0]/5;}
                    summary.Text=clicks.Count+" left. "+(prepared!=null&&prepared.SlidingOptimal?"Shortest route.":"Best route so far.");
                }
            }
            else if (puzzle.Kind == PuzzleKind.Knot)
            {
                if(prepared!=null&&prepared.KnotMoving)
                {
                    if(previousKnotMarks!=null)marks=previousKnotMarks.Select(m=>new GuidanceMark{Box=m.Box,Text=m.Text,Colour=Color.Silver,Corner=m.Corner,Compact=m.Compact,Outline=m.Outline}).ToList();
                    canvas.Lines=new[]{"Updating ring counts...","Grey numbers are the","last confirmed counts."};summary.Text="Updating counts - grey numbers show the last confirmed reading.";
                    canvas.Invalidate();ShowGuidance();return;
                }
                if(prepared==null) { puzzle.Knot = KnotImageReader.Merge(puzzle.Knot, knotHistory); knotHistory = puzzle.Knot; }
                KnotSolution solution = prepared==null ? KnotSolver.Solve(puzzle.Knot) : prepared.Knot;
                if (solution == null)
                {
                    canvas.Lines = new[] { "Some runes are hidden.", "Rotate any ring once,", "then let it scan again." };
                    summary.Text = "Rotate a ring once to reveal more runes; the reader combines scans.";
                }
                else
                {
                    string[] colourNames = { "Blue", "Red", "Dark blue", "Gold", "Grey" };
                    List<string> lines = new List<string>();
                    for (int i = 0; i < solution.Offsets.Length; i++)
                    {
                        int amount = solution.Offsets[i];
                        lines.Add(colourNames[puzzle.Knot.Colours[i]] + ": " + (amount == 0 ? "done" : Math.Abs(amount) + (amount > 0 ? " clockwise" : " counterclockwise")));
                        if (amount != 0)
                        {
                            Point button = puzzle.Knot.Buttons[i][amount > 0 ? 0 : 1];
                            Rectangle box = new Rectangle(puzzle.Bounds.X + (int)((button.X - 13) * puzzle.Scale), puzzle.Bounds.Y + (int)((button.Y - 13) * puzzle.Scale), (int)(26 * puzzle.Scale), (int)(26 * puzzle.Scale));
                            marks.Add(new GuidanceMark { Box = box, Text = Math.Abs(amount).ToString(), Colour = marks.Count == 0 ? Color.Lime : Color.Gold });
                        }
                    }
                    canvas.Lines = lines.ToArray();
                    summary.Text = solution.Cost == 0 ? "Intersections match. Check the knot in the game." : (solution.Certain ? "Rotate the marked arrows by their shown counts." : "One possible plan remains; rotate and rescan to verify.");
                }
            }
            canvas.Invalidate();
            status.Text = offline ? "Screenshot mode. The game overlay is disabled." : "Detected automatically. Guidance follows the visible puzzle.";
            ShowGuidance();
        }
        private void FollowSlider(DetectedPuzzle puzzle,List<int> preparedRoute=null)
        {
            List<int> remaining;
            if(slidingTheme==puzzle.Theme&&slidingBoard!=null&&slidingMoves!=null)
            {
                if(slidingBoard.SequenceEqual(puzzle.Board))return;
                int oldBlank=Array.IndexOf(slidingBoard,24),nextBlank=Array.IndexOf(puzzle.Board,24);
                bool adjacent=oldBlank>=0&&SlidingSolver.Neighbours(oldBlank).Contains(nextBlank);
                if(!adjacent)return;
                if(SlidingRoute.Advance(slidingBoard,slidingMoves,puzzle.Board,out remaining))
                {
                    int consumed=slidingMoves.Count-remaining.Count;
                    slidingBatchNumber+=wholeRowClicks.Checked?SlidingRoute.CompletedClicks(slidingBoard,slidingMoves,consumed):consumed;slidingMoves=remaining;
                    slidingBoard=(int[])puzzle.Board.Clone();return;
                }
            }
            if(!SlidingSolver.IsSolvable(puzzle.Board))return;
            slidingMoves=preparedRoute==null?(wholeRowClicks.Checked?SlidingClickOptimizer.Solve(puzzle.Board).Moves:SlidingOptimizer.Solve(puzzle.Board,SliderBudget.Milliseconds).Moves):new List<int>(preparedRoute);slidingBatchNumber=1;
            slidingBoard=(int[])puzzle.Board.Clone();slidingTheme=puzzle.Theme;
        }
        private void ShowGuidance()
        {
            if (floatingPanel != null) floatingPanel.SetQuickOption(waitingSlider != null && current != null && current.Kind == PuzzleKind.Sliding);
            UpdateFloatingPanel();
            if (offline || !draw.Checked || current == null || marks.Count == 0 || SelectedWindow == IntPtr.Zero || !WindowLocator.IsForeground(SelectedWindow))
            { overlay.Hide(); return; }
            Rectangle client = Native.ClientBounds(SelectedWindow);
            Rectangle screen = current.Bounds; screen.Offset(client.Location);
            overlay.UpdateGuidance(screen, marks.Select(mark => mark.Offset(-current.Bounds.X, -current.Bounds.Y)).ToList());
        }
        private static string DroppedImage(IDataObject data)
        {
            string[] paths = data == null ? null : data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length != 1) return null;
            string extension = Path.GetExtension(paths[0]).ToLowerInvariant();
            return new[]{".png", ".bmp", ".jpg", ".jpeg"}.Contains(extension) ? paths[0] : null;
        }
        private void OpenImageFile(string path)
        {
            try
            {
                using (var image = new Bitmap(path)) StartOffline(new Bitmap(image));
            }
            catch (Exception e) { status.Text = "Could not open that image. " + e.Message; }
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (keys == Keys.Escape && whatsNewPanel != null && whatsNewPanel.Visible) { HideWhatsNew(); return true; }
            if (keys == (Keys.Control | Keys.V))
            {
                try
                {
                    using (Image image = Clipboard.GetImage())
                    {
                        if (image != null) { StartOffline(new Bitmap(image)); return true; }
                    }
                }
                catch (System.Runtime.InteropServices.ExternalException) { status.Text = "Clipboard is busy. Try pasting again."; return true; }
            }
            return base.ProcessCmdKey(ref message, keys);
        }
        private void OpenScreenshot()
        {
            using (var dialog = new OpenFileDialog { Filter = "Screenshots|*.png;*.jpg;*.jpeg;*.bmp", Title = "Open a puzzle screenshot" })
                if (dialog.ShowDialog(this) == DialogResult.OK) OpenImageFile(dialog.FileName);
        }
        private void SaveCapture()
        {
            Image image=diagnosticCapture??preview.Image;
            if(image==null) { status.Text="Capture a puzzle first, then save its image."; return; }
            using(Bitmap snapshot=(Bitmap)image.Clone())
            using(SaveFileDialog dialog=new SaveFileDialog { Filter="PNG image|*.png",FileName="Kavros-capture-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",Title="Save the last captured puzzle" })
            { if(dialog.ShowDialog(this)==DialogResult.OK) { snapshot.Save(dialog.FileName,System.Drawing.Imaging.ImageFormat.Png); status.Text="Capture saved. You can attach that file when reporting a reading problem."; } }
        }
        private void ShowExamples()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (string kind in new[] { "Lockbox", "Towers", "Sliding", "Knot" }) { string name = kind; menu.Items.Add(name, null, delegate { LoadExample(name); }); }
            ToolStripMenuItem masters=new ToolStripMenuItem("Master emote clues");
            foreach(MasterClue clue in MasterClues.All) { MasterClue selected=clue; masters.DropDownItems.Add(clue.Emote+" / "+clue.Location,null,delegate { StartOffline(new Bitmap(Path.Combine(assetPath,"test_assets","master",selected.Id+".png"))); }); }
            menu.Items.Add(masters);
            menu.Closed += delegate { menu.Dispose(); }; menu.Show(this, new Point(226, 160));
        }
        public void LoadExample(string kind)
        {
            requestedScan = false;
            automatic.Checked = false; overlay.Hide(); offline = true; knotHistory = null;
            int[] board = Enumerable.Range(0, 25).ToArray(); board[24] = 23; board[23] = 24;
            StartOffline(kind == "Towers" ? TestFixtures.TowerModal("rev944") : kind == "Sliding" ? TestFixtures.SliderImage("castle", board, "rev944") : kind == "Knot" ? TestFixtures.KnotModal("rev944") : TestFixtures.LockboxImage("rev944"));
        }
        private void StartOffline(Bitmap image)
        {
            if (offlineSource != null) offlineSource.Dispose();
            offlineSource = new Bitmap(image);
            retainedClue.Reset();
            CancelAnalysis(); requestedScan=false; automatic.Checked=false; offline=true; current=null; pendingKey=null; marks.Clear(); overlay.Hide();
            ShowMasterView(false); masterView.SetClue(null,assetPath);
            detected.Text="Reading image..."; status.Text="The image is being processed in the background.";
            QueueAnalysis(new AnalysisJob { Image=image,Generation=sourceGeneration,Offline=true,Stable=false,Reverse=reverse.Checked,WholeRows=wholeRowClicks.Checked });
        }
    }

    public sealed class SuiteCanvas : Control
    {
        public DetectedPuzzle Puzzle; public int[] Values; public int Next = -1; public string[] Lines;
        public SuiteCanvas() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(KavrosTheme.Input);
            if (Puzzle == null || (Puzzle.Board == null && Puzzle.Kind != PuzzleKind.Towers)) { TextRenderer.DrawText(e.Graphics, "Open a puzzle in Kavros\nto see its solution here.", Font, ClientRectangle, KavrosTheme.TextSoft, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
            if (Lines != null)
            {
                using (Font f = new Font("Segoe UI", 10F, FontStyle.Bold))
                    for (int i = 0; i < Lines.Length; i++) TextRenderer.DrawText(e.Graphics, Lines[i], f, new Rectangle(10, 20 + i * 38, Width - 20, 34), Color.FromArgb(110, 227, 190), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                return;
            }
            using (Font f = new Font("Segoe UI", 15F, FontStyle.Bold)) using (Font small = new Font("Segoe UI", 8F, FontStyle.Bold))
                for (int i = 0; i < 25; i++)
                {
                    Rectangle cell = new Rectangle(i % 5 * Width / 5 + 2, i / 5 * Height / 5 + 2, Width / 5 - 4, Height / 5 - 4);
                    using (Brush b = new SolidBrush(i == Next ? Color.FromArgb(22, 69, 61) : Color.FromArgb(30, 41, 56))) e.Graphics.FillRectangle(b, cell);
                    using (Pen pen = new Pen(i == Next ? Color.Lime : Color.FromArgb(60, 78, 100))) e.Graphics.DrawRectangle(pen, cell);
                    string text;
                    if (Puzzle.Kind == PuzzleKind.Towers) text = Values == null ? "?" : Values[i].ToString();
                    else if (Puzzle.Kind == PuzzleKind.Lockbox) text = new[] { "M", "R", "W" }[Puzzle.Board[i]];
                    else text = Puzzle.Board[i] == 24 ? "" : (Puzzle.Board[i] + 1).ToString();
                    Color colour = Puzzle.Kind == PuzzleKind.Towers ? Color.Cyan : Color.FromArgb(225, 233, 243);
                    TextRenderer.DrawText(e.Graphics, text, f, cell, colour, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    if (Puzzle.Kind == PuzzleKind.Lockbox && Values != null && Values[i] > 0)
                        TextRenderer.DrawText(e.Graphics, Values[i].ToString(), small, new Rectangle(cell.Right - 16, cell.Top + 1, 15, 15), Color.Gold, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
        }
    }
}
