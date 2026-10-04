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
    }

    internal sealed class ChatWindow : Form
    {
        private readonly WebView2 browser = new WebView2();
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
        private readonly Timer healthTimer = new Timer();
        private readonly Panel selectionBar = new Panel();
        private readonly ComboBox selectionAction = new ComboBox();
        private readonly Label selectionStatus = new Label();
        private readonly Button insertSelection = new Button();
        private readonly Button copySelection = new Button();
        private readonly Button dismissSelection = new Button();
        private bool capturing, inserting, settingsOpen, pendingComposerFocus,layingOut;
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
        private readonly float dpiScale;
        private CoreWebView2Environment environment;
        private bool allowShow, quitting, initializing;
        private readonly Health health = new Health();

        internal ChatWindow(string[] args)
        {
            background = args.Contains("--background");
            diagnostics = args.FirstOrDefault(a => a.StartsWith("--diagnostics=",StringComparison.Ordinal)||a.StartsWith("--window-diagnostics=",StringComparison.Ordinal));
            pageDiagnostics=diagnostics!=null&&diagnostics.StartsWith("--diagnostics=",StringComparison.Ordinal);
            if (diagnostics != null) diagnostics = Path.GetFullPath(diagnostics.Substring(diagnostics.IndexOf('=')+1));
            Text = "DeepSeek 小窗";
            AutoScaleMode = AutoScaleMode.None;
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) dpiScale = graphics.DpiX / 96f;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(S(360),S(480));
            Bounds = Preferences.Bounds(dpiScale);
            try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"icon.ico")); } catch (IOException) { }
            TopMost = Preferences.ReadBool("pin.json");
            settings = WindowSettings.Load();
            if(settings.Mode=="compact") {
                Bounds=WindowModes.Calculate("compact",Bounds,Screen.FromRectangle(Bounds).WorkingArea,dpiScale);
                SaveBounds();
            }

            chrome.BackColor = Color.White;
            title.Text = "DeepSeek";
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
            selectionBar.BackColor=Color.FromArgb(246,248,253);
            selectionBar.Visible=false;
            selectionAction.DropDownStyle=ComboBoxStyle.DropDownList;
            selectionAction.Items.AddRange(new object[]{"翻译","学生解释","理解提问"});
            selectionAction.SelectedIndex=0;
            selectionAction.SelectedIndexChanged += async delegate { selectionVersion++; if(selectedText!=null) await FillSelection(); };
            selectionStatus.ForeColor=Color.FromArgb(69,77,92);
            selectionStatus.AutoEllipsis=true;
            selectionStatus.TextAlign=ContentAlignment.MiddleLeft;
            insertSelection.Text="填入"; copySelection.Text="复制请求"; dismissSelection.Text="×";
            foreach(var button in new[]{insertSelection,copySelection,dismissSelection}) {
                button.FlatStyle=FlatStyle.Flat; button.FlatAppearance.BorderSize=0;
                button.BackColor=Color.FromArgb(234,239,252); button.ForeColor=Color.FromArgb(61,92,187);
            }
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
            Resize += delegate { LayoutContent(); };
            ResizeBegin += delegate {if(smoothFrame!=null)smoothFrame.BeginInteractiveResize();};
            ResizeEnd += delegate {
                if(smoothFrame!=null)smoothFrame.EndInteractiveResize();
                SaveBounds();
                if(settings.Mode!="custom") {
                    var next=settings.Clone();next.Mode="custom";
                    try { next.Save();settings=next;RefreshMode(); } catch(IOException) { }
                }
                WriteHealth();
            };
            Shown += delegate { if(!background) RequestComposerFocus(); };
            FormClosing += delegate(object sender,FormClosingEventArgs e) {
                if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel=true; Hide(); WriteHealth(); }
                else SaveBounds();
            };
            tray.Icon = Icon;
            tray.Text = "DeepSeek 小窗";
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
                selectionAction.SetBounds(S(10),S(8),S(108),S(28));
                insertSelection.SetBounds(S(128),S(8),S(48),S(28));
                copySelection.SetBounds(S(182),S(8),S(84),S(28));
                dismissSelection.SetBounds(selectionBar.Width-S(36),S(8),S(26),S(28));
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
            TopMost = value;
            if(smoothFrame!=null)smoothFrame.SyncOwner();
            Preferences.Write("pin.json",value ? "true" : "false");
            RefreshPin();
            WriteHealth();
        }
        private void ShowChat()
        {
            allowShow = true;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState=FormWindowState.Normal;
            Activate();
            Native.SetForegroundWindow(Handle);
            browser.Focus();
            RequestComposerFocus();
            WriteHealth();
        }
        private void ToggleWindow()
        {
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
            catch(IOException) { MessageBox.Show(this,"尺寸设置未能保存，请重试。","DeepSeek 小窗"); }
        }
        private string ApplySettings(WindowSettings next)
        {
            string error=hotkeys.Apply(next.ToggleKeys,next.CaptureKeys);if(error!=null)return error;
            try { next.Save(); }
            catch(IOException) {
                hotkeys.Apply(settings.ToggleKeys,settings.CaptureKeys);
                return "设置未能保存，请重试。";
            }
            string previousMode=settings.Mode;settings=next;
            if(previousMode!=settings.Mode)ApplyMode(settings.Mode);
            RefreshMode();WriteHealth();return null;
        }
        private void ShowSettings()
        {
            if(settingsOpen)return;
            focusVersion++;pendingComposerFocus=false;settingsOpen=true;hotkeys.Suspend();
            try {
                using(var dialog=new SettingsDialog(settings,ApplySettings,delegate(string action) {
                    BeginInvoke(new Action(async delegate {
                        if(action=="refresh"&&browser.CoreWebView2!=null)browser.Reload();
                        if(action=="import")await PresentSelection(SelectionCapture.FromClipboard());
                    }));
                }))dialog.ShowDialog(this);
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
        private void RequestComposerFocus()
        {
            focusVersion++;pendingComposerFocus=settings.FocusOnOpen;
            if(pendingComposerFocus)FocusComposer(focusVersion);
        }
        private async void FocusComposer(int version)
        {
            if(browser.CoreWebView2==null) { health.composerFocus="loading";return; }
            health.composerFocus=await ComposerFocus.TryFocusAsync(browser.CoreWebView2,delegate {
                return !resourcesDisposed&&!quitting&&!settingsOpen&&settings.FocusOnOpen&&version==focusVersion&&Visible&&WindowState!=FormWindowState.Minimized&&Native.GetForegroundWindow()==Handle;
            });
            if(version==focusVersion)pendingComposerFocus=false;
            WriteHealth();
        }
        private ContextMenuStrip BuildMenu()
        {
            if(contextMenu!=null)return contextMenu;
            var menu=new TrayMenu(dpiScale);
            // Finish the dropdown click before opening a modal window or disposing the host.
            menu.AddAction("打开 DeepSeek 小窗",delegate { BeginInvoke(new Action(ShowChat)); });
            var pinItem=menu.AddAction(TopMost?"取消置顶":"置顶窗口",delegate { BeginInvoke(new Action(delegate { SetPinned(!TopMost); })); });
            menu.AddDivider();
            menu.AddAction("设置",delegate { BeginInvoke(new Action(ShowSettings)); });
            menu.AddAction("退出",delegate { BeginInvoke(new Action(delegate { quitting=true;Close(); })); });
            menu.Opening+=delegate { pinItem.Text=TopMost?"取消置顶":"置顶窗口"; };
            contextMenu=menu;return menu;
        }
        private async Task InitializeBrowser()
        {
            if (initializing) return;
            if (browser.CoreWebView2 != null) { browser.Reload(); return; }
            initializing=true;
            status.Text="正在打开 DeepSeek…";
            try
            {
                // A dedicated, persistent profile; the existing Edge profile is never copied or modified.
                environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Preferences.Root,"webview2-profile"));
                await browser.EnsureCoreWebView2Async(environment);
                browser.ZoomFactor=WindowFrame.DefaultZoom;
                var core=browser.CoreWebView2;
                core.Settings.AreHostObjectsAllowed=false;
                core.Settings.IsWebMessageEnabled=false;
                core.Settings.AreDevToolsEnabled=pageDiagnostics;
                core.NavigationStarting += delegate(object sender,CoreWebView2NavigationStartingEventArgs e) {
                    Uri uri;
                    if (!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="about")) e.Cancel=true;
                };
                core.NavigationCompleted += async delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) {
                    health.navigationSucceeded=e.IsSuccess;
                    health.navigationError=e.IsSuccess ? null : e.WebErrorStatus.ToString();
                    Uri uri;
                    if (Uri.TryCreate(core.Source,UriKind.Absolute,out uri)) {
                        health.sourceHost=uri.Host;
                        if (e.IsSuccess && uri.Scheme=="https" && uri.Host=="chat.deepseek.com") Preferences.Write("webview-last-page.txt",uri.GetLeftPart(UriPartial.Path));
                    }
                    status.Visible=!e.IsSuccess;
                    if (!e.IsSuccess) { status.Text="页面暂时无法打开，点此重试"; status.BringToFront(); chrome.BringToFront(); frame.BringToFront(); }
                    if (pageDiagnostics && e.IsSuccess) await CollectPageFacts();
                    if(e.IsSuccess&&pendingComposerFocus)FocusComposer(focusVersion);
                    WriteHealth();
                };
                core.NewWindowRequested += OpenPopup;
                core.ProcessFailed += delegate { status.Text="页面已停止响应，请在设置中刷新"; status.Show(); status.BringToFront(); chrome.BringToFront(); frame.BringToFront(); };
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
        private async void OpenPopup(object sender,CoreWebView2NewWindowRequestedEventArgs e)
        {
            var deferral=e.GetDeferral();
            Form popup=null;
            try
            {
                Uri uri;
                if (!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri) || uri.Scheme!="https") { e.Handled=true; return; }
                popup=new Form { Text="DeepSeek · 登录或链接",Size=new Size(S(440),S(640)),StartPosition=FormStartPosition.CenterParent,Icon=Icon };
                var child=new WebView2 { Dock=DockStyle.Fill,DefaultBackgroundColor=Color.White };
                popup.Controls.Add(child);
                popup.FormClosed += delegate { child.Dispose(); };
                popup.Show(this);
                await child.EnsureCoreWebView2Async(environment);
                child.CoreWebView2.Settings.AreHostObjectsAllowed=false;
                child.CoreWebView2.Settings.IsWebMessageEnabled=false;
                child.CoreWebView2.WindowCloseRequested += delegate { popup.Close(); };
                e.NewWindow=child.CoreWebView2;
                e.Handled=true;
            }
            catch (Exception) { e.Handled=true; if (popup!=null) popup.Close(); MessageBox.Show("此窗口未能打开，请重试。","DeepSeek 小窗"); }
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
            if(bounds.Width>0 && bounds.Height>0) Preferences.Write("webview-bounds.txt",string.Join(",",bounds.X,bounds.Y,bounds.Width,bounds.Height));
        }
        private void WriteHealth()
        {
            if (diagnostics==null || !IsHandleCreated || IsDisposed) return;
            health.pinned=TopMost; health.minimized=WindowState==FormWindowState.Minimized; health.visible=Visible;
            health.pid=System.Diagnostics.Process.GetCurrentProcess().Id; health.hwnd=Handle.ToInt64();
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
            health.selectionVisible=selectionBar.Visible;
            health.automaticSelectionEnabled=autoSelection!=null && autoSelection.Enabled;
            health.automaticSelectionRegistered=autoSelection!=null && autoSelection.Registered;
            try { using(var memory=new MemoryStream()) { new DataContractJsonSerializer(typeof(Health)).WriteObject(memory,health); File.WriteAllText(diagnostics,Encoding.UTF8.GetString(memory.ToArray())); } }
            catch (IOException) { }
        }
        protected override void WndProc(ref Message m)
        {
            if((m.Msg==0x214||m.Msg==0x216)&&m.LParam!=IntPtr.Zero&&smoothFrame!=null) {
                var rect=(Native.RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam,typeof(Native.RECT));
                if(smoothFrame.TrySetBounds(Rectangle.FromLTRB(rect.left,rect.top,rect.right,rect.bottom))){m.Result=new IntPtr(1);return;}
            }
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
            if(m.Msg==0x312&&settingsOpen)return;
            if(m.Msg==0x312 && m.WParam.ToInt32()==Native.HotkeyId) { ToggleWindow(); return; }
            if(m.Msg==0x312 && m.WParam.ToInt32()==Native.CaptureHotkeyId) { CaptureSelection(); return; }
            if(m.Msg==Native.OpenMessage) { ShowChat(); return; }
            base.WndProc(ref m);
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
            }
            base.Dispose(disposing);
        }
    }
}
