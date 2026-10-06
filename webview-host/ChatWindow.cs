using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeepSeekFloat
{
    [DataContract]
    internal sealed class Health
    {
        [DataMember] public bool initialized;
        [DataMember] public bool navigationSucceeded;
        [DataMember] public string navigationError;
        [DataMember] public string sourceHost;
        [DataMember] public string pageFacts;
        [DataMember] public bool pinned;
        [DataMember] public bool minimized;
        [DataMember] public bool visible;
        [DataMember] public int pid;
        [DataMember] public long hwnd;
        [DataMember] public int chromeHeight;
        [DataMember] public int webHeight;
        [DataMember] public int clientHeight;
        [DataMember] public bool captureHotkeyRegistered;
        [DataMember] public string captureShortcut;
        [DataMember] public bool selectionVisible;
        [DataMember] public bool automaticSelectionEnabled;
        [DataMember] public bool automaticSelectionRegistered;
        [DataMember] public bool toggleHotkeyRegistered;
        [DataMember] public string toggleShortcut;
        [DataMember] public string windowMode;
        [DataMember] public bool focusOnOpen;
        [DataMember] public bool hideToTrayOnToggle;
        [DataMember] public string composerFocus;
        [DataMember] public double zoomFactor;
        [DataMember] public int cornerRadius;
        [DataMember] public int resizeBand;
        [DataMember] public bool nativeCornersClipped;
        [DataMember] public bool nativeBorderSuppressed;
        [DataMember] public bool cornerResizeSurfaceReady;
        [DataMember] public bool smoothFrameReady;
        [DataMember] public bool smoothFrameVisible;
        [DataMember] public string smoothFrameError;
        [DataMember] public bool interactiveResize;
        [DataMember] public int smoothFrameUploads;
        [DataMember] public bool smoothCornersAligned;
        [DataMember] public bool guideVisible;
        [DataMember] public bool guideAcknowledged;
        [DataMember] public int windowIconWidth;
        [DataMember] public int trayIconWidth;
        [DataMember] public bool iconBackgroundTransparent;
        [DataMember] public string taskbarAppId;
    }

    internal sealed class ChatWindow : Form
    {
        private WebView2 browser = new WebView2();
        private bool recreateBrowser,browserExited;
        private readonly Panel chrome = new ResizeChrome();
        private readonly WindowFrame frame = new WindowFrame();
        private SmoothFrame smoothFrame;
        private readonly Label title = new Label();
        private readonly Label status = new Label();
        private readonly ChromeButton pin = new ChromeButton("pin","置顶窗口");
        private readonly ChromeButton more = new ChromeButton("settings","设置");
        private readonly ChromeButton minimize = new ChromeButton("minimize","最小化");
        private readonly ChromeButton close = new ChromeButton("close","收起到托盘");
        private readonly ChromeButton mode = new ChromeButton("mode","切换小窗 / 阅读尺寸");
        private readonly ToolTip tips = new ToolTip();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly Icon windowIcon, trayIcon;
        private readonly Timer healthTimer = new Timer();
        private readonly Panel selectionBar = new Panel();
        private readonly PanelChoices selectionAction = new PanelChoices("翻译","解释","提问");
        private readonly Label selectionStatus = new Label();
        private readonly PanelButton insertSelection = new PanelButton("填入") {Chosen=true};
        private readonly PanelButton copySelection = new PanelButton("复制请求") {Ghost=true};
        private readonly ChromeButton dismissSelection = new ChromeButton("close","收起取词栏");
        private bool capturing, inserting, settingsOpen, pendingComposerFocus,layingOut,guideOpen,guideQueued;
        private int focusVersion;
        private WindowSettings settings;
        private HotkeyBindings hotkeys;
        private int selectionVersion;
        private string selectedText, lastInserted;
        private ContextMenuStrip contextMenu;
        private AutoSelectionController autoSelection;
        private bool resourcesDisposed;
        private readonly string diagnostics;
        private readonly bool pageDiagnostics;
        private readonly bool background;
        private float dpiScale;
        private FormWindowState restoreState=FormWindowState.Normal;
        private CoreWebView2Environment environment;
        private bool allowShow, quitting, initializing;
        private readonly Health health = new Health();

        internal ChatWindow(string[] args)
        {
            background = args.Contains("--background");
            diagnostics = args.FirstOrDefault(a => a.StartsWith("--diagnostics=",StringComparison.Ordinal)||a.StartsWith("--window-diagnostics=",StringComparison.Ordinal));
            pageDiagnostics=diagnostics!=null&&diagnostics.StartsWith("--diagnostics=",StringComparison.Ordinal);
            if (diagnostics != null) diagnostics = Path.GetFullPath(diagnostics.Substring(diagnostics.IndexOf('=')+1));
            Text = "DeepSeek-Reflex";
            AutoScaleMode = AutoScaleMode.None;
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) dpiScale = graphics.DpiX / 96f;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(S(360),S(480));
            Bounds = Preferences.Bounds(dpiScale);
            try {
                var iconPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"icon.ico");
                windowIcon=new Icon(iconPath,SystemInformation.IconSize);
                trayIcon=new Icon(iconPath,SystemInformation.SmallIconSize);
                Icon=windowIcon;
                health.windowIconWidth=windowIcon.Width;health.trayIconWidth=trayIcon.Width;
                // Framework Icon.ToBitmap can flatten PNG icon alpha. Verify the
                // actual icon drawing leaves a contrasting background untouched.
                using(var bitmap=new Bitmap(trayIcon.Width,trayIcon.Height)) {
                    using(var graphics=Graphics.FromImage(bitmap)) {
                        graphics.Clear(Color.Magenta);var dc=graphics.GetHdc();
                        try{Native.DrawIconEx(dc,0,0,trayIcon.Handle,trayIcon.Width,trayIcon.Height,0,IntPtr.Zero,3);}
                        finally{graphics.ReleaseHdc(dc);}
                    }
                    health.iconBackgroundTransparent=(bitmap.GetPixel(0,0).ToArgb()&0xffffff)==0xff00ff&&(bitmap.GetPixel(bitmap.Width-1,bitmap.Height-1).ToArgb()&0xffffff)==0xff00ff;
                }
            } catch (IOException) { }
            TopMost = Preferences.ReadBool("pin.json");
            settings = WindowSettings.Load();
            if(settings.Mode=="compact") {
                Bounds=WindowModes.Calculate("compact",Bounds,Screen.FromRectangle(Bounds).WorkingArea,dpiScale);
                SaveBounds();
            }

            chrome.BackColor = Color.White;
            title.Text = "DeepSeek-Reflex";
            title.ForeColor = Color.FromArgb(69,77,92);
            title.Font = new Font("Segoe UI",9f,FontStyle.Regular);
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.AutoEllipsis = true;
            chrome.Controls.Add(title);
            chrome.Controls.AddRange(new Control[]{mode,pin,more,minimize,close});
            chrome.Paint += delegate(object sender,PaintEventArgs e) { using(var pen = new Pen(Color.FromArgb(233,237,243))) e.Graphics.DrawLine(pen,0,chrome.Height-1,chrome.Width,chrome.Height-1); };
            chrome.MouseDown += Drag;
            title.MouseDown += Drag;
            chrome.DoubleClick += delegate { ToggleMaximize(); };
            title.DoubleClick += delegate { ToggleMaximize(); };
            pin.Click += delegate { SetPinned(!TopMost); };
            mode.Click += delegate { SwitchMode(settings.Mode=="reading" ? "compact" : "reading"); };
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            close.Click += delegate { Hide(); WriteHealth(); };
            more.Click += delegate { ShowSettings(); };
            tips.SetToolTip(more,"设置 · 快捷键与窗口");
            tips.SetToolTip(minimize,"最小化 · 快捷键可恢复");
            tips.SetToolTip(close,"收起到托盘 · 快捷键可唤回");
            tips.InitialDelay = 450;
            tips.ReshowDelay = 150;
            tips.ShowAlways = true;
            RefreshPin();
            RefreshMode();

            status.Text = "正在打开 DeepSeek…";
            status.TextAlign = ContentAlignment.MiddleCenter;
            status.Font = new Font("Microsoft YaHei UI",10);
            status.ForeColor = Color.FromArgb(103,112,130);
            status.BackColor = Color.White;
            status.Click += async delegate { await InitializeBrowser(); };
            browser.DefaultBackgroundColor = Color.White;
            browser.AccessibleName = "DeepSeek 官网聊天";
            selectionBar.BackColor=Color.White;
            selectionBar.Font=new Font("Microsoft YaHei UI",9f);
            selectionBar.Paint+=delegate(object sender,PaintEventArgs e){using(var pen=new Pen(PanelTheme.Border))e.Graphics.DrawLine(pen,S(10),selectionBar.Height-1,selectionBar.Width-S(10),selectionBar.Height-1);};
            selectionBar.Visible=false;
            selectionAction.AccessibleName="文字处理方式";
            selectionAction.Controls[1].AccessibleName="学生解释";selectionAction.Controls[2].AccessibleName="理解提问";
            selectionAction.SelectedIndex=0;
            selectionAction.SelectedIndexChanged += async delegate { selectionVersion++; if(selectedText!=null) await FillSelection(); };
            selectionStatus.ForeColor=PanelTheme.Muted;
            selectionStatus.AutoEllipsis=true;
            selectionStatus.TextAlign=ContentAlignment.MiddleLeft;
            insertSelection.Click += async delegate { await FillSelection(); };
            copySelection.Click += delegate {
                if(selectedText==null) return;
                try { Clipboard.SetText(PromptInserter.Compose(selectedText,selectionAction.SelectedIndex)); selectionStatus.Text="请求已复制，可粘贴到对话框"; }
                catch(System.Runtime.InteropServices.ExternalException) { selectionStatus.Text="剪贴板正忙，请重试"; }
            };
            dismissSelection.Click += delegate { selectionVersion++; selectedText=null; selectionBar.Hide(); LayoutContent(); };
            tips.SetToolTip(selectionAction,"选择文字处理方式，填入后由你确认发送");
            tips.SetToolTip(dismissSelection,"收起取词栏");
            selectionBar.Controls.AddRange(new Control[]{selectionAction,selectionStatus,insertSelection,copySelection,dismissSelection});
            Controls.AddRange(new Control[]{browser,status,selectionBar,chrome,frame});
            selectionBar.BringToFront();
            chrome.BringToFront();
            Resize += delegate { if(WindowState!=FormWindowState.Minimized)restoreState=WindowState;LayoutContent(); };
            ResizeBegin += delegate {if(smoothFrame!=null)smoothFrame.BeginInteractiveResize();};
            ResizeEnd += delegate {
                if(smoothFrame!=null)smoothFrame.EndInteractiveResize();
                SaveBounds();
                if(settings.Mode!="custom") {
                    var next=settings.Clone();next.Mode="custom";
                    try { next.Save();settings=next;RefreshMode(); } catch(Exception error) {if(!Preferences.IsStorageFailure(error))throw;}
                }
                WriteHealth();
            };
            Shown += delegate { if(!background) RequestComposerFocus();QueueIntroduction(); };
            FormClosing += delegate(object sender,FormClosingEventArgs e) {
                if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel=true; Hide(); WriteHealth(); }
                else SaveBounds();
            };
            tray.Icon = trayIcon ?? Icon;
            tray.Text = "DeepSeek-Reflex";
            // NotifyIcon supplies the foreground owner and taskbar menu lifecycle.
            // Showing a standalone dropdown here bypasses outside-click dismissal.
            tray.ContextMenuStrip = BuildMenu();
            tray.Visible = true;
            tray.MouseClick += delegate(object sender,MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) ToggleWindow();
            };
            healthTimer.Interval = 500;
            healthTimer.Tick += delegate { WriteHealth(); };
            if (diagnostics != null) healthTimer.Start();
            smoothFrame=new SmoothFrame(this,dpiScale,delegate {if(!resourcesDisposed)LayoutContent();});
            LayoutContent();
        }
        private int S(int value) { return Math.Max(1,(int)Math.Round(value*dpiScale)); }
        internal void ApplyDpi(float value,Rectangle bounds) {
            if(value<=0)return;
            dpiScale=value;
            MinimumSize=new Size(S(360),S(480));
            Bounds=bounds;
            if(smoothFrame!=null)smoothFrame.UpdateScale(value);
            LayoutContent();
        }
        protected override CreateParams CreateParams {
            get {
                var value=base.CreateParams;
                // WinForms calculates borderless restore sizes from these parameters.
                // The native sizing bit is applied separately to the actual HWND.
                value.Style|=0x000a0000; // WS_SYSMENU | WS_MINIMIZEBOX
                value.ExStyle|=0x00040000; // WS_EX_APPWINDOW
                return value;
            }
        }
        private void LayoutContent()
        {
            if (chrome == null||layingOut) return;
            layingOut=true;
            try {
            if(smoothFrame!=null)smoothFrame.SyncOwner();
            int height = S(44), edge = S(4), width = S(32);
            // Leave the top resize strip owned by the form, not covered by child controls.
            chrome.SetBounds(0,S(WindowResize.Border),ClientSize.Width,height-S(WindowResize.Border));
            title.SetBounds(S(112),-S(WindowResize.Border),Math.Max(0,ClientSize.Width-S(224)),height);
            var leftButtons = new[]{minimize,pin,mode};
            for(int i=0;i<leftButtons.Length;i++)leftButtons[i].SetBounds(S(8)+width*i,0,width,S(28));
            var rightButtons = new[]{close,more};
            for(int i=0;i<rightButtons.Length;i++)rightButtons[i].SetBounds(ClientSize.Width-S(8)-width*(i+1),0,width,S(28));
            if(selectionBar.Visible) {
                selectionBar.SetBounds(edge,height,Math.Max(0,ClientSize.Width-edge*2),S(76));
                int choicesWidth=Math.Max(1,selectionBar.Width-S(192));
                selectionAction.SetBounds(S(10),S(8),choicesWidth,S(32));
                insertSelection.SetBounds(S(18)+choicesWidth,S(8),S(48),S(32));
                copySelection.SetBounds(S(72)+choicesWidth,S(8),S(76),S(32));
                dismissSelection.SetBounds(selectionBar.Width-S(38),S(10),S(28),S(28));
                selectionStatus.SetBounds(S(10),S(40),Math.Max(0,selectionBar.Width-S(20)),S(28));
                height+=selectionBar.Height;
            }
            browser.SetBounds(edge,height,Math.Max(0,ClientSize.Width-edge*2),Math.Max(0,ClientSize.Height-height-edge));
            status.Bounds = browser.Bounds;
            frame.UpdateShape(this,dpiScale,smoothFrame!=null&&smoothFrame.Ready);
            }finally{layingOut=false;}
        }
        private async void CaptureSelection()
        {
            if(capturing) return;
            var source=Native.GetForegroundWindow();
            capturing=true;
            try {
                var result=await SelectionCapture.CaptureAsync(source);
                if(resourcesDisposed || quitting || IsDisposed) return;
                await PresentSelection(result);
            } catch(Exception) { if(!resourcesDisposed) { selectionStatus.Text="取词未完成，请复制后从设置导入"; } }
            finally { capturing=false; }
        }
        private async Task PresentSelection(SelectionResult result)
        {
            selectionVersion++;
            selectedText=result.Text;
            selectionBar.Show(); LayoutContent(); ShowChat(); selectionBar.BringToFront(); chrome.BringToFront(); frame.BringToFront();
            insertSelection.Enabled=copySelection.Enabled=selectedText!=null;
            selectionStatus.Text=result.Error ?? ("已取词 "+selectedText.Length+" 字，正在填入…");
            if(selectedText!=null) await FillSelection();
        }
        private async Task FillSelection()
        {
            if(selectedText==null || inserting || resourcesDisposed) return;
            inserting=true;
            int version=selectionVersion, length=selectedText.Length;
            string prompt=PromptInserter.Compose(selectedText,selectionAction.SelectedIndex);
            try {
                var result=await PromptInserter.InsertAsync(browser.CoreWebView2,prompt,lastInserted);
                if(resourcesDisposed || quitting || IsDisposed) return;
                if(result=="filled") lastInserted=prompt;
                if(version!=selectionVersion) return;
                if(result=="filled") { selectionStatus.Text="已填入 "+length+" 字的请求，请确认后发送"; }
                else if(result=="draft") selectionStatus.Text="输入框已有草稿；请保留或清空后点“填入”";
                else if(result=="loading") selectionStatus.Text="页面正在加载，完成后点“填入”";
                else if(result=="no-editor") selectionStatus.Text="请先登录并打开对话，再点“填入”";
                else selectionStatus.Text="暂时无法填入；可点“复制请求”后手动粘贴";
            } finally {
                inserting=false;
                if(!resourcesDisposed && !quitting && version!=selectionVersion && selectedText!=null && IsHandleCreated)
                    BeginInvoke(new Action(async delegate { await FillSelection(); }));
            }
        }
        private void Drag(object sender,MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);
        }
        private void ToggleMaximize() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        private void RefreshPin()
        {
            pin.Active = TopMost;
            pin.AccessibleName = TopMost ? "取消置顶" : "置顶窗口";
            pin.Text=pin.AccessibleName;
            tips.SetToolTip(pin,pin.AccessibleName);
            pin.Invalidate();
        }
        private void SetPinned(bool value)
        {
            try { Preferences.Write("pin.json",value ? "true" : "false"); }
            catch(Exception error) { if(!Preferences.IsStorageFailure(error))throw;tips.SetToolTip(pin,"置顶设置未能保存，请重试。");return; }
            TopMost = value;
            if(smoothFrame!=null)smoothFrame.SyncOwner();
            RefreshPin();
            WriteHealth();
        }
        private void ShowChat()
        {
            allowShow = true;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState=restoreState;
            if(settingsOpen||guideOpen) {
                var dialog=OwnedForms.FirstOrDefault(f=>f is SettingsDialog||f is GettingStartedDialog);
                if(dialog!=null){if(!dialog.Visible)dialog.Show(this);if(dialog.WindowState==FormWindowState.Minimized)dialog.WindowState=FormWindowState.Normal;dialog.Activate();}
                return;
            }
            Activate();
            Native.SetForegroundWindow(Handle);
            browser.Focus();
            RequestComposerFocus();
            QueueIntroduction();
            WriteHealth();
        }
        private void ToggleWindow()
        {
            if(settingsOpen||guideOpen){ShowChat();return;}
            if (Visible && WindowState != FormWindowState.Minimized) {
                pendingComposerFocus=false;focusVersion++;
                if(settings.HideToTrayOnToggle)Hide();else WindowState=FormWindowState.Minimized;
            }
            else ShowChat();
            WriteHealth();
        }
        protected override void SetVisibleCore(bool value)
        {
            if (background && !allowShow && value) { if (!IsHandleCreated) CreateHandle(); base.SetVisibleCore(false); return; }
            base.SetVisibleCore(value);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnableNativeSizing(Handle);
            WindowFrame.ConfigureNativeBorder(Handle);
            frame.UpdateShape(this,dpiScale);
            if(smoothFrame!=null)smoothFrame.SyncOwner();
            hotkeys=new HotkeyBindings(Handle,settings.ToggleKeys,settings.CaptureKeys);
            string shortcutError=hotkeys.Apply(settings.ToggleKeys,settings.CaptureKeys);
            tips.SetToolTip(more,shortcutError ?? "设置 · 快捷键与窗口");
            if(autoSelection==null)autoSelection=new AutoSelectionController(this);
            autoSelection.SetEnabled(Preferences.AutomaticSelectionEnabled());
            BeginInvoke(new Action(async delegate { await InitializeBrowser(); }));
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if(hotkeys!=null)hotkeys.Dispose();
            base.OnHandleDestroyed(e);
        }
        private void RefreshMode()
        {
            mode.Active=settings.Mode=="reading";
            mode.AccessibleName=settings.Mode=="reading" ? "切换到小窗尺寸" : "切换到阅读尺寸";
            mode.Text=mode.AccessibleName;tips.SetToolTip(mode,mode.AccessibleName);mode.Invalidate();
        }
        private void ApplyMode(string value)
        {
            if(value=="custom")return;
            var current=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
            WindowState=FormWindowState.Normal;
            Bounds=WindowModes.Calculate(value,current,Screen.FromRectangle(current).WorkingArea,dpiScale);
            SaveBounds();
        }
        private void SwitchMode(string value)
        {
            var next=settings.Clone();next.Mode=value;
            try { next.Save();settings=next;ApplyMode(value);RefreshMode();WriteHealth(); }
            catch(Exception error) {if(!Preferences.IsStorageFailure(error))throw;MessageBox.Show(this,"尺寸设置未能保存，请重试。","DeepSeek-Reflex");}
        }
        private string ApplySettings(WindowSettings next)
        {
            string error=hotkeys.Apply(next.ToggleKeys,next.CaptureKeys);if(error!=null)return error;
            try { next.Save(); }
            catch(Exception failure) {
                if(!Preferences.IsStorageFailure(failure))throw;
                string rollback=hotkeys.Apply(settings.ToggleKeys,settings.CaptureKeys);
                return rollback==null ? "设置未能保存，原快捷键已恢复，请重试。" : "设置未能保存，原快捷键恢复失败："+rollback;
            }
            string previousMode=settings.Mode;settings=next;
            if(previousMode!=settings.Mode)ApplyMode(settings.Mode);
            RefreshMode();WriteHealth();return null;
        }
        private void ShowSettings()
        {
            if(settingsOpen||guideOpen)return;
            focusVersion++;pendingComposerFocus=false;settingsOpen=true;hotkeys.Suspend();
            try {
                using(var dialog=new SettingsDialog(settings,ApplySettings,delegate(string action) {
                    BeginInvoke(new Action(async delegate {
                        if(action=="refresh")await InitializeBrowser();
                        if(action=="import")await PresentSelection(SelectionCapture.FromClipboard());
                        if(action=="help")ShowHelp();
                    }));
                })) { dialog.Icon=Icon;dialog.ShowDialog(this); }
            }
            finally {
                settingsOpen=false;
                if(!resourcesDisposed&&!quitting&&!IsDisposed&&IsHandleCreated) {
                    string error=hotkeys.Resume();
                    tips.SetToolTip(more,error ?? "设置 · 快捷键与窗口");WriteHealth();
                    if(error!=null)MessageBox.Show(this,error,"快捷键设置");
                }
            }
        }
        private void QueueIntroduction() {
            if(guideQueued||guideOpen||settingsOpen||resourcesDisposed||quitting||!Visible||WindowState==FormWindowState.Minimized||!GettingStarted.NeedsIntroduction)return;
            guideQueued=true;
            BeginInvoke(new Action(delegate{guideQueued=false;if(!resourcesDisposed&&!quitting&&!IsDisposed&&Visible&&WindowState!=FormWindowState.Minimized&&!settingsOpen&&GettingStarted.NeedsIntroduction)ShowHelp();}));
        }
        private void ShowHelp() {
            if(guideOpen||settingsOpen||resourcesDisposed||quitting)return;
            guideOpen=true;focusVersion++;pendingComposerFocus=false;hotkeys.Suspend();
            try {using(var guide=new GettingStartedDialog(settings)){guide.Icon=Icon;guide.ShowDialog(this);}GettingStarted.Acknowledge();}
            finally {
                guideOpen=false;
                if(!resourcesDisposed&&!quitting&&!IsDisposed&&IsHandleCreated){string error=hotkeys.Resume();tips.SetToolTip(more,error??"设置 · 快捷键与窗口");RequestComposerFocus();WriteHealth();}
            }
        }
        private void RequestComposerFocus()
        {
            focusVersion++;pendingComposerFocus=settings.FocusOnOpen;
            if(pendingComposerFocus)FocusComposer(focusVersion);
        }
        private async void FocusComposer(int version)
        {
            if(browser.CoreWebView2==null) { health.composerFocus="loading";return; }
            health.composerFocus=await ComposerFocus.TryFocusAsync(browser.CoreWebView2,delegate {
                return !resourcesDisposed&&!quitting&&!settingsOpen&&!guideOpen&&settings.FocusOnOpen&&version==focusVersion&&Visible&&WindowState!=FormWindowState.Minimized&&Native.GetForegroundWindow()==Handle;
            });
            if(version==focusVersion)pendingComposerFocus=false;
            WriteHealth();
        }
        private ContextMenuStrip BuildMenu()
        {
            if(contextMenu!=null)return contextMenu;
            var menu=new TrayMenu(dpiScale);
            // Finish the dropdown click before opening a modal window or disposing the host.
            menu.AddAction("打开 DeepSeek-Reflex",delegate { BeginInvoke(new Action(ShowChat)); });
            var pinItem=menu.AddAction(TopMost?"取消置顶":"置顶窗口",delegate { BeginInvoke(new Action(delegate { SetPinned(!TopMost); })); });
            menu.AddDivider();
            menu.AddAction("设置",delegate { BeginInvoke(new Action(ShowSettings)); });
            menu.AddAction("退出",delegate { BeginInvoke(new Action(delegate { quitting=true;Close(); })); });
            menu.Opening+=delegate { pinItem.Text=TopMost?"取消置顶":"置顶窗口"; };
            contextMenu=menu;return menu;
        }
        private async Task InitializeBrowser()
        {
            if (initializing||resourcesDisposed||quitting) return;
            if(recreateBrowser&&!browserExited){ShowBrowserError("浏览器正在退出，稍后点此重试");return;}
            if(recreateBrowser) {
                foreach(var popup in OwnedForms.OfType<PopupWindow>().ToArray())popup.Close();
                Controls.Remove(browser);browser.Dispose();
                browser=new WebView2 {DefaultBackgroundColor=Color.White,AccessibleName="DeepSeek 官网聊天"};
                Controls.Add(browser);recreateBrowser=false;browserExited=false;environment=null;health.initialized=false;
                lastInserted=null;LayoutContent();chrome.BringToFront();frame.BringToFront();
            }
            if (browser.CoreWebView2 != null) {
                try{browser.Reload();}catch(Exception error){health.navigationError=error.GetType().Name;ShowBrowserError("刷新未完成，点此重试");}
                return;
            }
            initializing=true;
            status.Text="正在打开 DeepSeek…";
            status.Show();status.BringToFront();chrome.BringToFront();frame.BringToFront();
            try
            {
                // A dedicated, persistent profile; the existing Edge profile is never copied or modified.
                environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Preferences.Root,"webview2-profile"));
                if(resourcesDisposed||quitting)return;
                var currentEnvironment=environment;
                currentEnvironment.BrowserProcessExited+=delegate(object sender,CoreWebView2BrowserProcessExitedEventArgs args) {
                    if(resourcesDisposed||quitting||!Object.ReferenceEquals(environment,currentEnvironment)||args.BrowserProcessExitKind!=CoreWebView2BrowserProcessExitKind.Failed)return;
                    recreateBrowser=true;browserExited=true;health.initialized=false;
                    ShowBrowserError("浏览器已退出，点此重新打开（登录资料保留）");
                };
                await browser.EnsureCoreWebView2Async(environment);
                if(resourcesDisposed||quitting||browser.IsDisposed)return;
                browser.ZoomFactor=WindowFrame.DefaultZoom;
                var core=browser.CoreWebView2;
                core.Settings.AreHostObjectsAllowed=false;
                core.Settings.IsWebMessageEnabled=false;
                core.Settings.AreDevToolsEnabled=pageDiagnostics;
                core.NavigationStarting += delegate(object sender,CoreWebView2NavigationStartingEventArgs e) {
                    lastInserted=null;
                    Uri uri;
                    if (!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="about")) e.Cancel=true;
                };
                core.NavigationCompleted += async delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) {
                    health.navigationSucceeded=e.IsSuccess;
                    health.navigationError=e.IsSuccess ? null : e.WebErrorStatus.ToString();
                    Uri uri;
                    if (Uri.TryCreate(core.Source,UriKind.Absolute,out uri)) {
                        health.sourceHost=uri.Host;
                        if (e.IsSuccess && uri.Scheme=="https" && uri.Host=="chat.deepseek.com") {
                            try{Preferences.Write("webview-last-page.txt",uri.GetLeftPart(UriPartial.Path));}
                            catch(Exception error){if(!Preferences.IsStorageFailure(error))throw;}
                        }
                    }
                    status.Visible=!e.IsSuccess;
                    if (!e.IsSuccess) { status.Text="页面暂时无法打开，点此重试"; status.BringToFront(); chrome.BringToFront(); frame.BringToFront(); }
                    if (pageDiagnostics && e.IsSuccess) await CollectPageFacts();
                    if(e.IsSuccess&&pendingComposerFocus)FocusComposer(focusVersion);
                    WriteHealth();
                };
                core.NewWindowRequested += OpenPopup;
                core.ProcessFailed += delegate(object sender,CoreWebView2ProcessFailedEventArgs args){HandleProcessFailure(args.ProcessFailedKind);};
                health.initialized=true;
                core.Navigate(Preferences.LastPage());
                WriteHealth();
            }
            catch (Exception e)
            {
                health.navigationError=e.GetType().Name;
                status.Text="小窗未能打开，请点此重试";
                WriteHealth();
            }
            finally { initializing=false; }
        }
        private void ShowBrowserError(string text){if(resourcesDisposed||quitting)return;status.Text=text;status.Show();status.BringToFront();chrome.BringToFront();frame.BringToFront();}
        internal void HandleProcessFailure(CoreWebView2ProcessFailedKind kind) {
            var action=BrowserRecovery.For(kind);
            if(action==RecoveryAction.Ignore)return;
            health.navigationError=kind.ToString();lastInserted=null;
            if(action==RecoveryAction.Recreate){recreateBrowser=true;health.initialized=false;ShowBrowserError(browserExited?"浏览器已退出，点此重新打开":"浏览器正在退出，稍后点此重试");}
            else ShowBrowserError("页面暂时停止响应，点此刷新");
            WriteHealth();
        }
        private async void OpenPopup(object sender,CoreWebView2NewWindowRequestedEventArgs e)
        {
            var deferral=e.GetDeferral();
            PopupWindow popup=null;
            try
            {
                Uri uri;
                if (!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri) || uri.Scheme!="https") { e.Handled=true; return; }
                popup=new PopupWindow(uri,dpiScale) {Icon=Icon};
                var child=popup.Browser;
                popup.Show(this);
                if(!await popup.InitializeAsync(environment)){e.Handled=true;return;}
                if(popup.IsDisposed||child.IsDisposed||resourcesDisposed||quitting){e.Handled=true;return;}
                child.CoreWebView2.Settings.AreHostObjectsAllowed=false;
                child.CoreWebView2.Settings.IsWebMessageEnabled=false;
                child.CoreWebView2.WindowCloseRequested += delegate { if(!popup.IsDisposed)popup.Close(); };
                e.NewWindow=child.CoreWebView2;
                e.Handled=true;
            }
            catch (Exception) { e.Handled=true;bool cancelled=resourcesDisposed||quitting||(popup!=null&&popup.IsDisposed);if(popup!=null&&!popup.IsDisposed)popup.ShowError("此窗口未能打开，请关闭后重试。");if(!cancelled&&popup==null)ShowBrowserError("链接窗口未能打开，请重试。"); }
            finally { deferral.Complete(); }
        }
        private async Task CollectPageFacts()
        {
            try
            {
                await Task.Delay(1500);
                health.pageFacts=await browser.CoreWebView2.ExecuteScriptAsync("({ready:document.readyState,loginFields:document.querySelectorAll('input[type=password],input[type=tel]').length,editableFields:document.querySelectorAll('textarea,[contenteditable=true]').length,loginVisible:Array.from(document.querySelectorAll('button,a')).some(e=>/登录|Log in|Sign in/i.test(e.innerText)),width:innerWidth,height:innerHeight})");
            }
            catch (Exception) { health.pageFacts=null; }
        }
        private void SaveBounds()
        {
            var bounds=WindowState==FormWindowState.Normal ? Bounds : RestoreBounds;
            try { if(bounds.Width>0 && bounds.Height>0) Preferences.Write("webview-bounds.txt",string.Join(",",bounds.X,bounds.Y,bounds.Width,bounds.Height)); }
            catch(Exception error){if(!Preferences.IsStorageFailure(error))throw;}
        }
        private void WriteHealth()
        {
            if (diagnostics==null || !IsHandleCreated || IsDisposed) return;
            health.pinned=TopMost; health.minimized=WindowState==FormWindowState.Minimized; health.visible=Visible;
            health.pid=System.Diagnostics.Process.GetCurrentProcess().Id; health.hwnd=Handle.ToInt64();
            health.taskbarAppId=ShellIdentity.CurrentId;
            health.chromeHeight=chrome.Bottom; health.webHeight=browser.Height; health.clientHeight=ClientSize.Height;
            health.captureHotkeyRegistered=hotkeys!=null&&hotkeys.CaptureRegistered;
            health.captureShortcut=HotkeyBindings.Format(settings.CaptureKeys);
            health.toggleHotkeyRegistered=hotkeys!=null&&hotkeys.ToggleRegistered;
            health.toggleShortcut=HotkeyBindings.Format(settings.ToggleKeys);
            health.windowMode=settings.Mode;health.focusOnOpen=settings.FocusOnOpen;health.hideToTrayOnToggle=settings.HideToTrayOnToggle;
            health.zoomFactor=browser.ZoomFactor;health.cornerRadius=WindowState==FormWindowState.Maximized?0:S(WindowFrame.Radius);
            health.resizeBand=S(WindowResize.Border);
            health.nativeCornersClipped=WindowState==FormWindowState.Normal&&WindowFrame.NativeCornersClipped(this);
            health.nativeBorderSuppressed=WindowFrame.NativeBorderSuppressed(Handle);
            health.cornerResizeSurfaceReady=WindowState==FormWindowState.Normal&&frame.NativeResizeSurfaceReady(this,dpiScale,smoothFrame);
            health.smoothFrameReady=smoothFrame!=null&&smoothFrame.Ready;
            health.smoothFrameVisible=smoothFrame!=null&&smoothFrame.Visible;
            health.smoothFrameError=smoothFrame==null?null:smoothFrame.LastError;
            health.interactiveResize=smoothFrame!=null&&smoothFrame.InteractiveResize;
            health.smoothFrameUploads=smoothFrame==null?0:smoothFrame.UploadCount;
            health.smoothCornersAligned=smoothFrame!=null&&smoothFrame.Aligned;
            health.guideVisible=guideOpen;health.guideAcknowledged=!GettingStarted.NeedsIntroduction;
            health.selectionVisible=selectionBar.Visible;
            health.automaticSelectionEnabled=autoSelection!=null && autoSelection.Enabled;
            health.automaticSelectionRegistered=autoSelection!=null && autoSelection.Registered;
            try { using(var memory=new MemoryStream()) { new DataContractJsonSerializer(typeof(Health)).WriteObject(memory,health); File.WriteAllText(diagnostics,Encoding.UTF8.GetString(memory.ToArray())); } }
            catch (Exception error) {if(!Preferences.IsStorageFailure(error))throw;}
        }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x2e0){ApplyDpi(WindowDpi.MessageScale(m.WParam),WindowDpi.Suggested(m.LParam));m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x7d&&m.WParam.ToInt64()==-16)EnableNativeSizing(m.HWnd);
            // Keep the entire rectangle as client area. WS_THICKFRAME enables
            // Windows' sizing loop, but our existing frame owns its visual border.
            if(m.Msg==0x83) {m.Result=IntPtr.Zero;return;}
            // With DWM decoration disabled, DefWindowProc otherwise paints a
            // classic thick frame on activation (including modal settings return).
            // Preserve activation processing, but suppress its nonclient paint.
            if(m.Msg==0x85) {m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x86&&WindowState!=FormWindowState.Minimized) {
                m.LParam=new IntPtr(-1);base.WndProc(ref m);return;
            }
            // WM_MOVING / WM_SIZING describe the system's drag rectangle.
            // Let Windows commit it (including Snap previews) before following
            // the resulting move / size events. Applying it here competes with
            // the native move loop and also lays out uncommitted proposals.
            if(m.Msg==0x10 && !quitting) { Hide(); WriteHealth(); return; }
            if(m.Msg==0x84 && WindowState==FormWindowState.Normal)
            {
                int hit=WindowResize.HitTest(PointToClient(WindowResize.ScreenPoint(m.LParam)),ClientSize,S(WindowResize.Border),false,S(WindowFrame.Radius));
                if(hit!=0) { m.Result=new IntPtr(hit); return; }
            }
            if(m.Msg==0x20&&WindowState==FormWindowState.Normal) {
                var cursor=WindowResize.CursorFor((int)(m.LParam.ToInt64()&0xffff));
                if(cursor!=null){Cursor.Current=cursor;m.Result=new IntPtr(1);return;}
            }
            if(m.Msg==0x312&&(settingsOpen||guideOpen))return;
            if(m.Msg==0x312 && m.WParam.ToInt32()==Native.HotkeyId) { ToggleWindow(); return; }
            if(m.Msg==0x312 && m.WParam.ToInt32()==Native.CaptureHotkeyId) { CaptureSelection(); return; }
            if(m.Msg==Native.OpenMessage) { ShowChat(); return; }
            base.WndProc(ref m);
        }
        private static void EnableNativeSizing(IntPtr handle) {
            int style=Native.GetWindowLong(handle,-16);
            if((style&0x40000)!=0)return;
            if(Native.SetWindowLong(handle,-16,style|0x40000)==0)throw new System.ComponentModel.Win32Exception();
            if(!Native.SetWindowPos(handle,IntPtr.Zero,0,0,0,0,0x37))throw new System.ComponentModel.Win32Exception();
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing && !resourcesDisposed) {
                resourcesDisposed=true;
                if(smoothFrame!=null)smoothFrame.Dispose();
                selectedText=null; lastInserted=null;
                if(autoSelection!=null)autoSelection.Dispose();
                healthTimer.Stop();
                healthTimer.Dispose();
                if(contextMenu!=null) contextMenu.Dispose();
                tray.Visible=false; tray.Dispose(); tips.Dispose(); browser.Dispose();
                if(trayIcon!=null)trayIcon.Dispose();
                if(windowIcon!=null)windowIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
