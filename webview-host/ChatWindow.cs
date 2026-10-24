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
        [DataMember] public string chatMode;
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
        [DataMember] public bool edgeDocked;
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
        private readonly Label status = new AnnouncingLabel { ActAsButton=true };
        private readonly ChromeButton pin = new ChromeButton("pin","置顶窗口");
        private readonly ChromeButton more = new ChromeButton("settings","设置");
        private readonly ChromeButton minimize = new ChromeButton("minimize","最小化");
        private readonly ChromeButton close = new ChromeButton("close","收起到托盘");
        private readonly ChromeButton mode = new ChromeButton("mode","切换小窗 / 阅读尺寸");
        private readonly ToolTip tips = new ToolTip();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly Icon windowIcon, trayIcon;
        private readonly Timer healthTimer = new Timer();
        private readonly PanelButton chatModeButton = new PanelButton("普通对话") {Ghost=true,DropDown=true};
        private string chatModeError;
        private bool updatingChatMode,chatModeUpdatePending;
        private readonly Panel selectionBar = new Panel();
        private readonly PanelChoices selectionAction = new PanelChoices("翻译","解释","提问");
        private readonly Label selectionStatus = new AnnouncingLabel();
        private readonly PanelButton moreSelection = new PanelButton("更多指令") {Ghost=true,Outline=true,DropDown=true};
        private readonly PanelButton insertSelection = new PanelButton("填入") {Chosen=true};
        private readonly PanelButton copySelection = new PanelButton("复制请求") {Ghost=true};
        private readonly ChromeButton dismissSelection = new ChromeButton("close","收起取词栏");
        private PromptCommand[] customCommands=new PromptCommand[0];
        private PromptCommand customChoice;
        private string commandsError;
        private bool updatingChoice;
        private bool capturing, inserting, settingsOpen, pendingComposerFocus,layingOut,guideOpen,guideQueued;
        private int focusVersion;
        private WindowSettings settings;
        private HotkeyBindings hotkeys;
        private int selectionVersion;
        private string selectedText, lastInserted;
        private ContextMenuStrip contextMenu;
        private AutoSelectionController autoSelection;
        private bool resourcesDisposed;
        private ReflexBridge bridge;
        private bool bridgeInFlight;
        private readonly string diagnostics;
        private readonly bool pageDiagnostics;
        private readonly bool background;
        private float dpiScale;
        private Func<WakeTarget> captureWakeTarget=WakeTarget.Capture;
        private WakePlacement pendingWakePlacement;
        private int wakeVersion;
        private bool showWithoutActivation;
        private int lastTrayToggle=unchecked((int)0x80000000);
        private readonly WakeTarget startupTarget;
        private bool startupPositioned;
        private EdgeDockController edgeDock;
        private FormWindowState restoreState=FormWindowState.Normal;
        private CoreWebView2Environment environment;
        private bool allowShow, quitting, initializing;
        private readonly Health health = new Health();

        internal ChatWindow(string[] args)
        {
            startupTarget=captureWakeTarget();
            background = args.Contains("--background");
            diagnostics = args.FirstOrDefault(a => a.StartsWith("--diagnostics=",StringComparison.Ordinal)||a.StartsWith("--window-diagnostics=",StringComparison.Ordinal));
            pageDiagnostics=diagnostics!=null&&diagnostics.StartsWith("--diagnostics=",StringComparison.Ordinal);
            if (diagnostics != null) diagnostics = Path.GetFullPath(diagnostics.Substring(diagnostics.IndexOf('=')+1));
            Text = "DeepSeek-Reflex";
            AutoScaleMode = AutoScaleMode.None;
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) dpiScale = graphics.DpiX / 96f;
            // Resolve the theme before any control captures a color.
            PanelTheme.SetMode(ResolveTheme(WindowSettings.Load().Theme));
            BackColor = PanelTheme.Back;
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
            if(settings.Theme!="light"&&settings.Theme!="dark")Microsoft.Win32.SystemEvents.UserPreferenceChanged+=OnSystemPreferenceChanged;
            browser.DefaultBackgroundColor=PanelTheme.Back;
            if(settings.Mode=="compact") {
                Bounds=WindowModes.Calculate("compact",Bounds,Screen.FromRectangle(Bounds).WorkingArea,dpiScale);
                SaveBounds();
            }

            chrome.BackColor = PanelTheme.Back;
            chrome.Controls.AddRange(new Control[]{mode,pin,more,minimize,close});
            chrome.Paint += delegate(object sender,PaintEventArgs e) { using(var pen = new Pen(Color.FromArgb(233,237,243))) e.Graphics.DrawLine(pen,0,chrome.Height-1,chrome.Width,chrome.Height-1); };
            chrome.MouseDown += Drag;
            chrome.DoubleClick += delegate { ToggleMaximize(); };
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
            Font=PanelTheme.TextFont();
            status.Font = PanelTheme.TextFont();
            status.ForeColor = PanelTheme.Muted;
            status.BackColor = PanelTheme.Back;
            status.Click += async delegate { await InitializeBrowser(); };
            // WebView defaults to opaque white. Avoid requesting an optional
            // background-color controller interface just to set the default.
            browser.AccessibleName = "DeepSeek 官网聊天";
            chatModeButton.Font=PanelTheme.TextFont();
            chatModeButton.AccessibleName="选择持续生效的对话模式";
            chatModeButton.Click+=delegate{ShowChatModeMenu();};
            chrome.Controls.Add(chatModeButton);
            RefreshChatMode();
            selectionBar.BackColor=PanelTheme.Back;
            selectionBar.Font=PanelTheme.TextFont();
            selectionBar.Paint+=delegate(object sender,PaintEventArgs e){using(var pen=new Pen(PanelTheme.Border))e.Graphics.DrawLine(pen,S(10),selectionBar.Height-1,selectionBar.Width-S(10),selectionBar.Height-1);};
            selectionBar.Visible=false;
            selectionAction.AccessibleName="文字处理方式";
            selectionAction.Controls[1].AccessibleName="学生解释";selectionAction.Controls[2].AccessibleName="理解提问";
            selectionAction.SelectedIndex=0;
            selectionAction.SelectedIndexChanged += async delegate {
                if(updatingChoice)return;
                if(selectionAction.SelectedIndex>=0&&!updatingChoice) { customChoice=null;updatingChoice=true;RefreshSelectionChoice();updatingChoice=false; }
                selectionVersion++; if(selectedText!=null) await FillSelection();
            };
            moreSelection.AccessibleName="更多取词指令";
            moreSelection.Click+=delegate{ShowMoreCommandsMenu();};
            tips.SetToolTip(moreSelection,"选择自定义取词指令");
            selectionStatus.ForeColor=PanelTheme.Muted;
            selectionStatus.Font=PanelTheme.TextFont(PanelTheme.CaptionSize);
            selectionStatus.AutoEllipsis=true;
            selectionStatus.TextAlign=ContentAlignment.MiddleLeft;
            insertSelection.Click += async delegate { await FillSelection(); };
            copySelection.Click += async delegate {
                if(selectedText==null) return;
                int version=selectionVersion;
                try {
                    string prompt=ComposeSelection();Clipboard.SetText(prompt);
                    var core=browser.CoreWebView2;
                    if(core!=null)try{await core.ExecuteScriptAsync(ChatModeInjector.ExpectOneShotScript(prompt));}catch(Exception){}
                    if(!resourcesDisposed&&!quitting&&version==selectionVersion)selectionStatus.Text="请求已复制，可粘贴到对话框";
                }
                catch(System.Runtime.InteropServices.ExternalException) { selectionStatus.Text="剪贴板正忙，请重试"; }
            };
            dismissSelection.Click += delegate { selectionVersion++; selectedText=null; selectionBar.Hide(); LayoutContent(); };
            tips.SetToolTip(selectionAction,"选择文字处理方式，填入后由你确认发送");
            tips.SetToolTip(dismissSelection,"收起取词栏");
            selectionBar.Controls.AddRange(new Control[]{selectionAction,selectionStatus,moreSelection,insertSelection,copySelection,dismissSelection});
            Controls.AddRange(new Control[]{browser,status,selectionBar,chrome,frame});
            selectionBar.BringToFront();
            chrome.BringToFront();
            Resize += delegate { if(WindowState!=FormWindowState.Minimized)restoreState=WindowState;LayoutContent(); };
            ResizeBegin += delegate {wakeVersion++;pendingWakePlacement=null;if(smoothFrame!=null)smoothFrame.BeginInteractiveResize();};
            ResizeEnd += delegate {
                if(smoothFrame!=null)smoothFrame.EndInteractiveResize();
                SaveBounds();
                if(settings.Mode!="custom") {
                    var next=settings.Clone();next.Mode="custom";
                    try { next.Save();settings=next;RefreshMode(); } catch(Exception error) {if(!Preferences.IsStorageFailure(error))throw;}
                }
                WriteHealth();
            };
            Shown += delegate { if(!background){if(!startupPositioned){startupPositioned=true;PositionForWake(startupTarget);}RequestComposerFocus();}QueueIntroduction(); };
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
                if (e.Button != MouseButtons.Left) return;
                // Tray icons stay single-click toggles; absorb the second click of
                // a habitual double-click so the window does not flash open and closed.
                int gap=unchecked(Environment.TickCount-lastTrayToggle);
                if(gap>=0&&gap<SystemInformation.DoubleClickTime) return;
                lastTrayToggle=Environment.TickCount;
                ToggleWindow();
            };
            healthTimer.Interval = 500;
            healthTimer.Tick += delegate { WriteHealth(); };
            if (diagnostics != null) healthTimer.Start();
            smoothFrame=new SmoothFrame(this,dpiScale,delegate {if(!resourcesDisposed)LayoutContent();});
            edgeDock=new EdgeDockController(this);
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=OnDisplaySettingsChanged;
            LayoutContent();
        }
        private void OnDisplaySettingsChanged(object sender,EventArgs e) {
            if(edgeDock!=null&&!resourcesDisposed&&!quitting&&IsHandleCreated)BeginInvoke(new Action(delegate { edgeDock.OnDisplaysChanged(); WriteHealth(); }));
        }
        // Edge-dock controller hooks; kept internal for the controller and tests.
        internal WindowSettings Settings {get{return settings;}}
        internal bool ModalOpen {get{return settingsOpen||guideOpen;}}
        internal float DockScale {get{return dpiScale;}}
        internal Icon AppIcon {get{return Icon;}}
        internal void WakeFromDock() {
            if(edgeDock!=null&&edgeDock.Docked)edgeDock.Restore();
            var center=edgeDock!=null&&edgeDock.SavedRect!=Rectangle.Empty
                ? new Point(edgeDock.SavedRect.Left+edgeDock.SavedRect.Width/2,edgeDock.SavedRect.Top+edgeDock.SavedRect.Height/2)
                : Cursor.Position;
            ShowChatAt(WakeTarget.At(center));
        }
        private int S(int value) { return Math.Max(1,(int)Math.Round(value*dpiScale)); }
        internal void ApplyDpi(float value,Rectangle bounds) {
            if(value<=0)return;
            dpiScale=value;
            // Pixel-unit font for the mode pill keeps its measured width equal
            // to its painted width after any resolution/DPI change.
            chatModeButton.Font=new Font(PanelTheme.FontFamily,Math.Max(9f,(float)Math.Round(12f*Math.Max(1f,value)*2f)/2f),GraphicsUnit.Pixel);
            if(pendingWakePlacement!=null)bounds=pendingWakePlacement.Bounds(value);
            var area=Screen.FromRectangle(bounds).WorkingArea;
            MinimumSize=new Size(Math.Min(S(360),area.Width),Math.Min(S(480),area.Height));
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
            var leftButtons = new[]{minimize,pin,mode};
            for(int i=0;i<leftButtons.Length;i++)leftButtons[i].SetBounds(S(8)+width*i,0,width,S(28));
            var rightButtons = new[]{close,more};
            for(int i=0;i<rightButtons.Length;i++)rightButtons[i].SetBounds(ClientSize.Width-S(8)-width*(i+1),0,width,S(28));
            int available=Math.Max(0,ClientSize.Width-S(192));
            // Hug the mode text plus chevron instead of a fixed pill width.
            int selectorWidth=Math.Min(Math.Max(S(92),TextRenderer.MeasureText(chatModeButton.Text,chatModeButton.Font).Width+S(32)),Math.Max(S(92),available));
            // Center on the whole title bar rather than the left/right button
            // clusters, so the pill reads as the window's title.
            chatModeButton.SetBounds(Math.Max(S(112),(ClientSize.Width-selectorWidth)/2),0,selectorWidth,S(28));
            if(selectionBar.Visible) {
                // Two fixed rows per the dock/capture contract: choices, more
                // commands and dismiss on top; status, fill and copy below.
                selectionBar.SetBounds(edge,height,Math.Max(0,ClientSize.Width-edge*2),S(82));
                int choicesWidth=Math.Max(1,selectionBar.Width-S(136)-S(16));
                selectionAction.SetBounds(S(10),S(8),choicesWidth,S(32));
                moreSelection.SetBounds(selectionBar.Width-S(126),S(8),S(84),S(32));
                dismissSelection.SetBounds(selectionBar.Width-S(38),S(10),S(28),S(28));
                selectionStatus.SetBounds(S(10),S(46),Math.Max(1,selectionBar.Width-S(176)),S(30));
                insertSelection.SetBounds(selectionBar.Width-S(162),S(46),S(56),S(30));
                copySelection.SetBounds(selectionBar.Width-S(100),S(46),S(92),S(30));
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
            var target=captureWakeTarget();
            bool failed=false;
            capturing=true;
            try {
                var result=await SelectionCapture.CaptureAsync(source);
                if(resourcesDisposed || quitting || IsDisposed) return;
                await PresentSelectionAt(result,target);
            } catch(Exception) { failed=!resourcesDisposed; }
            finally { capturing=false; }
            // Unexpected capture errors must still surface somewhere visible,
            // not inside the hidden selection bar.
            if(failed && !resourcesDisposed && !quitting && !IsDisposed)
                await PresentSelectionAt(SelectionResult.Fail("取词未完成，请复制后从设置导入"),target);
        }
        private async Task PresentSelection(SelectionResult result)
        {
            await PresentSelectionAt(result,captureWakeTarget());
        }
        private async Task PresentSelectionAt(SelectionResult result,WakeTarget target)
        {
            selectionVersion++;
            selectedText=result.Text;
            // Failed captures keep the user's foreground: the window appears
            // without activation so a mistyped hotkey does not interrupt reading.
            selectionBar.Show(); LayoutContent(); WakeChatAt(target,selectedText!=null); selectionBar.BringToFront(); chrome.BringToFront(); frame.BringToFront();
            insertSelection.Enabled=copySelection.Enabled=selectedText!=null;
            selectionStatus.Text=result.Error ?? ("已取词 "+selectedText.Length+" 字，正在填入…");
            if(selectedText!=null) await FillSelection();
        }
        private string ComposeSelection() {
            return customChoice==null
                ? PromptInserter.Compose(selectedText,Math.Max(0,selectionAction.SelectedIndex))
                : PromptInserter.ComposeInstruction(customChoice.Instruction,selectedText);
        }
        private void RefreshSelectionChoice() {
            if(customChoice!=null) {
                updatingChoice=true;
                if(selectionAction.SelectedIndex!=-1)selectionAction.SelectedIndex=-1;
                updatingChoice=false;
                moreSelection.Text=customChoice.Name;moreSelection.Chosen=true;
                // Overflow is ellipsized by the painter; the full name lives in
                // the accessible name and tooltip instead.
                moreSelection.AccessibleName="自定义指令 "+customChoice.Name;
                tips.SetToolTip(moreSelection,"已选择："+customChoice.Name+"，点击更换");
                if(selectedText==null)selectionStatus.Text="已选「"+customChoice.Name+"」，选中文字后点“填入”";
            } else {
                updatingChoice=true;
                if(selectionAction.SelectedIndex<0)selectionAction.SelectedIndex=0;
                updatingChoice=false;
                moreSelection.Text="更多指令";moreSelection.Chosen=false;
                moreSelection.AccessibleName="更多取词指令";
                tips.SetToolTip(moreSelection,"选择自定义取词指令");
                if(selectedText==null)selectionStatus.Text="";
            }
        }
        private void ShowMoreCommandsMenu() {
            var menu=new TrayMenu(dpiScale);
            if(commandsError!=null){menu.AddAction(commandsError,delegate{}).Enabled=false;menu.AddDivider();}
            foreach(var command in customCommands) {
                var choice=command;
                menu.AddAction((customChoice==choice?"✓ ":"")+choice.Name,delegate {
                    customChoice=choice;RefreshSelectionChoice();
                    selectionVersion++;if(selectedText!=null){var pending=FillSelection();}
                });
            }
            if(customCommands.Length==0&&commandsError==null) {
                var empty=menu.AddAction("还没有自定义指令",delegate{});empty.Enabled=false;
            }
            menu.AddDivider();
            menu.AddAction("去设置添加",delegate { ShowSettingsPage("commands"); });
            menu.Closed+=delegate {
                if(!resourcesDisposed&&!quitting&&IsHandleCreated){moreSelection.Focus();BeginInvoke(new Action(menu.Dispose));}
                else menu.Dispose();
            };
            menu.Show(moreSelection,new Point(0,moreSelection.Height));
        }
        private void ReloadCommands() {
            var catalog=PromptCatalog.Load();
            customCommands=catalog.Commands.Where(c=>c.Custom).ToArray();
            commandsError=catalog.Error;
            if(customChoice!=null&&Array.IndexOf(customCommands,customChoice)<0){customChoice=null;RefreshSelectionChoice();}
        }
        private async Task FillSelection()
        {
            if(selectedText==null || inserting || resourcesDisposed) return;
            inserting=true;
            int version=selectionVersion, length=selectedText.Length;
            string prompt=ComposeSelection();
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
            wakeVersion++;pendingWakePlacement=null;
            var before=NormalWakeBounds();
            Native.ReleaseCapture();
            // Blocks until the native move loop ends, which is the only
            // reliable "release on an edge" signal for the dock contract.
            Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);
            if(edgeDock!=null&&!resourcesDisposed&&!quitting)edgeDock.OnDragEnded(before);
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
            ShowChatAt(captureWakeTarget());
        }
        private void ShowChatAt(WakeTarget target)
        {
            WakeChatAt(target,true);
        }
        // A distinct name keeps ShowChatAt unambiguous for the reflection-based tests.
        private void WakeChatAt(WakeTarget target,bool activate)
        {
            if(edgeDock!=null&&edgeDock.Docked)edgeDock.Restore();
            wakeVersion++;
            startupPositioned=true;
            if(!settingsOpen&&!guideOpen)PositionForWake(target);
            allowShow = true;
            showWithoutActivation=!activate;
            Show();
            showWithoutActivation=false;
            if(!activate)Native.SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x53); // top of z-order, no activation
            if (WindowState == FormWindowState.Minimized) WindowState=restoreState;
            if(settingsOpen||guideOpen) {
                var dialog=OwnedForms.FirstOrDefault(f=>f is SettingsDialog||f is GettingStartedDialog);
                if(dialog!=null){if(!dialog.Visible)dialog.Show(this);if(dialog.WindowState==FormWindowState.Minimized)dialog.WindowState=FormWindowState.Normal;dialog.Activate();}
                return;
            }
            if(activate){Activate();Native.SetForegroundWindow(Handle);browser.Focus();RequestComposerFocus();}
            QueueIntroduction();
            WriteHealth();
        }
        private void ToggleWindow()
        {
            var target=captureWakeTarget();
            if(settingsOpen||guideOpen){ShowChat();return;}
            if (Visible && WindowState != FormWindowState.Minimized&&!target.DiffersFrom(NormalWakeBounds())) {
                wakeVersion++;pendingWakePlacement=null;
                pendingComposerFocus=false;focusVersion++;
                if(settings.HideToTrayOnToggle)Hide();else WindowState=FormWindowState.Minimized;
            }
            else ShowChatAt(target);
            WriteHealth();
        }
        private Rectangle NormalWakeBounds(){return WindowState==FormWindowState.Normal?Bounds:RestoreBounds;}
        private void PositionForWake(WakeTarget target) {
            var before=NormalWakeBounds();var area=target.Resolve().WorkingArea;
            bool changed=target.DiffersFrom(before);
            var fitted=WakePlacement.Fit(before,area);
            if(!changed&&fitted==before)return;
            int version=wakeVersion;
            var state=WindowState==FormWindowState.Minimized?restoreState:WindowState;
            float scale=IsHandleCreated?WindowDpi.Scale(this):dpiScale;
            var placement=changed?new WakePlacement(target,before.Size,scale):null;
            pendingWakePlacement=placement;
            try{
                if(WindowState!=FormWindowState.Normal)WindowState=FormWindowState.Normal;
                MinimumSize=new Size(Math.Min(S(360),area.Width),Math.Min(S(480),area.Height));
                Bounds=placement==null?fitted:placement.Bounds(scale);
                if(placement!=null)ApplyDpi(WindowDpi.Scale(this),placement.Bounds(WindowDpi.Scale(this)));
            }finally{pendingWakePlacement=null;}
            if(state==FormWindowState.Maximized)WindowState=state;
            restoreState=state;SaveBounds();
            if(placement!=null&&IsHandleCreated)BeginInvoke(new Action(delegate{
                if(resourcesDisposed||quitting||version!=wakeVersion||!Visible||settingsOpen||guideOpen||WindowState!=FormWindowState.Normal)return;
                // Recenter once after queued native DPI changes, using the real DPI.
                float actual=WindowDpi.Scale(this);pendingWakePlacement=placement;
                try{ApplyDpi(actual,placement.Bounds(actual));}finally{pendingWakePlacement=null;}
                SaveBounds();
            }));
        }
        protected override bool ShowWithoutActivation { get { return showWithoutActivation; } }
        protected override void SetVisibleCore(bool value)
        {
            if (background && !allowShow && value) { if (!IsHandleCreated) CreateHandle(); base.SetVisibleCore(false); return; }
            base.SetVisibleCore(value);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if(bridge==null)bridge=new ReflexBridge(HandleBridge);
            EnableNativeSizing(Handle);
            WindowFrame.ConfigureNativeBorder(Handle);
            frame.UpdateShape(this,dpiScale);
            if(smoothFrame!=null)smoothFrame.SyncOwner();
            hotkeys=new HotkeyBindings(Handle,settings.ToggleKeys,settings.CaptureKeys);
            string shortcutError=hotkeys.Apply(settings.ToggleKeys,settings.CaptureKeys);
            tips.SetToolTip(more,shortcutError ?? "设置 · 快捷键与窗口");
            if(autoSelection==null)autoSelection=new AutoSelectionController(this);
            autoSelection.SetEnabled(Preferences.AutomaticSelectionEnabled());
            ReloadCommands();
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
        private static BridgeResult R(string status){return new BridgeResult(status);}
        private Task<BridgeResult> HandleBridge(BridgeRequest request,System.Threading.CancellationToken cancel) {
            if(request!=null&&request.IsReply)return ReadReplyFromBridge(cancel);
            return StageFromBridge(request,cancel);
        }
        private Task<BridgeResult> StageFromBridge(BridgeRequest request,System.Threading.CancellationToken cancel) {
            var completion=new TaskCompletionSource<BridgeResult>();
            if(resourcesDisposed||!IsHandleCreated){completion.SetResult(R("loading"));return completion.Task;}
            try{BeginInvoke(new Action(async delegate{
                if(cancel.IsCancellationRequested||resourcesDisposed||quitting){completion.TrySetResult(R("outcome-unknown"));return;}
                if(bridgeInFlight){completion.TrySetResult(R("busy"));return;}
                var target=captureWakeTarget();
                if(settingsOpen||guideOpen){ShowChatAt(target);completion.TrySetResult(R("busy"));return;}
                var core=browser.CoreWebView2;
                ShowChatAt(target);
                if(core==null||initializing||updatingChatMode){completion.TrySetResult(R("loading"));return;}
                bridgeInFlight=true;
                try{
                    if(!request.Valid){completion.TrySetResult(R("invalid"));return;}
                    string prompt=ChatModes.Prefix(ChatModes.Find(request.mode))+request.text;
                    completion.TrySetResult(R(await PromptInserter.InsertAsync(core,prompt,null)));
                }catch(Exception){completion.TrySetResult(R("outcome-unknown"));}
                finally{bridgeInFlight=false;}
            }));}catch(InvalidOperationException){completion.TrySetResult(R("loading"));}
            return completion.Task;
        }
        // Passive: the window stays docked, hidden or behind panels; nothing is focused or sent.
        private Task<BridgeResult> ReadReplyFromBridge(System.Threading.CancellationToken cancel) {
            var completion=new TaskCompletionSource<BridgeResult>();
            if(resourcesDisposed||!IsHandleCreated){completion.SetResult(R("loading"));return completion.Task;}
            try{BeginInvoke(new Action(async delegate{
                if(cancel.IsCancellationRequested||resourcesDisposed||quitting){completion.TrySetResult(R("outcome-unknown"));return;}
                if(bridgeInFlight){completion.TrySetResult(R("busy"));return;}
                var core=browser.CoreWebView2;
                if(core==null||initializing){completion.TrySetResult(R("loading"));return;}
                bridgeInFlight=true;
                try{
                    var reply=ReplyReader.Parse((await core.ExecuteScriptAsync(ReplyReader.Script)).Trim());
                    completion.TrySetResult(reply.Status=="ok"&&reply.Text!=null?new BridgeResult("ok",reply.Text):R(reply.Status));
                }catch(Exception){completion.TrySetResult(R("unsupported"));}
                finally{bridgeInFlight=false;}
            }));}catch(InvalidOperationException){completion.TrySetResult(R("loading"));}
            return completion.Task;
        }
        private void RefreshChatMode() {
            var current=ChatModes.Find(settings.ChatMode);
            chatModeError=null;
            chatModeButton.Text=current.Name;chatModeButton.Chosen=current.Id!="normal";
            if(IsHandleCreated)LayoutContent();
            chatModeButton.AccessibleDescription=current.Name+(current.Id=="normal"?"，按原文发送":"，每次输入自动应用；检查后发送");
            tips.SetToolTip(chatModeButton,current.Id=="normal"?"按原文发送，不添加提示词":current.Instruction+"\n每条消息都会在输入框中自动填入提示词，可检查后发送。取词请求单独处理一次。");
        }
        private void SetChatModeError(string message){chatModeError=message;chatModeButton.AccessibleDescription=message;tips.SetToolTip(chatModeButton,message);}
        private void ShowChatModeMenu() {
            var menu=CreateChatModeMenu();
            // Left edges stay flush: the popup grows right/down from the anchor
            // point, so a DPI rescale on show cannot shove it sideways.
            menu.Show(chatModeButton,new Point(0,chatModeButton.Height));
        }
        private TrayMenu CreateChatModeMenu() {
            var menu=new TrayMenu(dpiScale);
            if(chatModeError!=null){menu.AddAction(chatModeError,delegate{}).Enabled=false;menu.AddDivider();}
            foreach(var definition in ChatModes.All) {
                var choice=definition;
                menu.AddAction((choice.Id==settings.ChatMode?"✓ ":"")+choice.Name,async delegate{await SwitchChatMode(choice.Id);});
            }
            menu.Closed+=delegate{
                if(!resourcesDisposed&&!quitting&&IsHandleCreated){chatModeButton.Focus();BeginInvoke(new Action(menu.Dispose));}
                else menu.Dispose();
            };
            return menu;
        }
        private async Task SwitchChatMode(string value) {
            if(ChatModes.Find(value).Id==settings.ChatMode)return;
            var next=settings.Clone();next.ChatMode=ChatModes.Find(value).Id;
            try{next.Save();}
            catch(Exception error){if(!Preferences.IsStorageFailure(error))throw;SetChatModeError("模式未保存，请重试");return;}
            settings=next;RefreshChatMode();await UpdateChatMode();WriteHealth();
        }
        private async Task UpdateChatMode() {
            chatModeUpdatePending=true;if(updatingChatMode)return;
            updatingChatMode=true;
            try{
                while(chatModeUpdatePending&&!resourcesDisposed&&!quitting){
                    chatModeUpdatePending=false;var core=browser.CoreWebView2;if(core==null)return;
                    string chosen=settings.ChatMode;
                    try{
                        string result=(await core.ExecuteScriptAsync(ChatModeInjector.Script(chosen))).Trim('"');
                        if(resourcesDisposed||quitting||chosen!=settings.ChatMode||!Object.ReferenceEquals(core,browser.CoreWebView2))continue;
                        RefreshChatMode();
                        if(chosen!="normal"&&result=="unsupported-editor")SetChatModeError("此输入框暂不支持");
                        else if(chosen!="normal"&&result=="no-editor")SetChatModeError("打开对话后自动应用");
                        else if(result=="wrong-host")SetChatModeError("仅在聊天官网生效");
                        else if(result=="failed")SetChatModeError("填入失败，请重试");
                        else if(result=="edited-prefix")SetChatModeError("已保留修改的提示词");
                    }catch(Exception){if(!resourcesDisposed&&!quitting)SetChatModeError("页面就绪后自动应用");}
                }
            }finally{updatingChatMode=false;}
        }
        private void ApplyMode(string value)
        {
            if(value=="custom")return;
            wakeVersion++;pendingWakePlacement=null;
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
            ApplyThemeFromSettings();
            RefreshMode();WriteHealth();return null;
        }
        private void ShowFeedback() {
            if(resourcesDisposed||quitting)return;
            if(edgeDock!=null&&edgeDock.Docked){edgeDock.Restore();ShowChatAt(captureWakeTarget());}
            using(var dialog=new FeedbackDialog()){dialog.Icon=Icon;dialog.ShowDialog(this);}
            WriteHealth();
        }
        private void ShowSettings(){ShowSettingsPage(null);}
        internal static UiTheme ResolveTheme(string preference) {
            if(preference=="dark")return UiTheme.Dark;
            if(preference=="light")return UiTheme.Light;
            return PanelTheme.SystemPrefersDark()?UiTheme.Dark:UiTheme.Light;
        }
        private void ApplyThemeFromSettings() {
            var mode=ResolveTheme(settings.Theme);
            if(PanelTheme.Mode==mode)return;
            PanelTheme.SetMode(mode);
            ApplyTheme();
        }
        // Captured colors track the theme that was active at construction, so
        // a runtime switch re-assigns them from the palette and repaints.
        internal void ApplyTheme() {
            BackColor=PanelTheme.Back;
            if(chrome!=null){chrome.BackColor=PanelTheme.Back;foreach(Control child in chrome.Controls){child.BackColor=PanelTheme.Back;if(child is PanelButton||child is ChromeButton)child.ForeColor=PanelTheme.Ink;}}
            if(selectionBar!=null){selectionBar.BackColor=PanelTheme.Back;foreach(Control child in selectionBar.Controls){child.BackColor=PanelTheme.Back;if(child is PanelButton||child is ChromeButton)child.ForeColor=PanelTheme.Ink;}}
            if(status!=null){status.BackColor=PanelTheme.Back;status.ForeColor=PanelTheme.Muted;}
            if(edgeDock!=null)edgeDock.ApplyTheme();
            browser.DefaultBackgroundColor=PanelTheme.Back;
            Invalidate(true);
        }
        private void OnSystemPreferenceChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e) {
            if(e.Category!=Microsoft.Win32.UserPreferenceCategory.General||resourcesDisposed||quitting||!IsHandleCreated)return;
            BeginInvoke(new Action(delegate{if(!resourcesDisposed&&!quitting)ApplyThemeFromSettings();}));
        }
        private void ShowSettingsPage(string openPage)
        {
            if(settingsOpen||guideOpen)return;
            if(edgeDock!=null&&edgeDock.Docked) { edgeDock.Restore(); ShowChatAt(captureWakeTarget()); }
            focusVersion++;pendingComposerFocus=false;settingsOpen=true;hotkeys.Suspend();
            try {
                using(var dialog=new SettingsDialog(settings,ApplySettings,delegate(string action) {
                    BeginInvoke(new Action(async delegate {
                        if(action=="feedback")ShowFeedback();
                        if(action=="refresh")await InitializeBrowser();
                        if(action=="import")await PresentSelection(SelectionCapture.FromClipboard());
                        if(action=="help")ShowHelp();
                        if(action=="commands")ReloadCommands();
                    }));
                },openPage)) { dialog.Icon=Icon;dialog.ShowDialog(this); }
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
            if(edgeDock!=null&&edgeDock.Docked) { edgeDock.Restore(); ShowChatAt(captureWakeTarget()); }
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
            menu.AddAction("打开 DeepSeek-Reflex",delegate { var target=captureWakeTarget();BeginInvoke(new Action(delegate{ShowChatAt(target);})); });
            var pinItem=menu.AddAction(TopMost?"取消置顶":"置顶窗口",delegate { BeginInvoke(new Action(delegate { SetPinned(!TopMost); })); });
            menu.AddDivider();
            menu.AddAction("设置",delegate { BeginInvoke(new Action(ShowSettings)); });
            menu.AddAction("反馈",delegate { BeginInvoke(new Action(ShowFeedback)); });
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
                browser=new WebView2 {AccessibleName="DeepSeek 官网聊天",DefaultBackgroundColor=PanelTheme.Back};
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
                    // WebView2 persists zoom per origin in the profile, so the
                    // pre-navigation default does not survive a real navigation.
                    // Reapply it once the document has committed.
                    if (e.IsSuccess && Math.Abs(browser.ZoomFactor-WindowFrame.DefaultZoom)>0.0005) browser.ZoomFactor=WindowFrame.DefaultZoom;
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
                    if(e.IsSuccess)await UpdateChatMode();
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
        internal void SaveBounds()
        {
            var bounds=WindowState==FormWindowState.Normal ? Bounds : RestoreBounds;
            // The edge-dock tab rectangle is a separate window and never reaches here.
            try { if(bounds.Width>0 && bounds.Height>0) Preferences.Write("webview-bounds.txt",string.Join(",",bounds.X,bounds.Y,bounds.Width,bounds.Height)); }
            catch(Exception error){if(!Preferences.IsStorageFailure(error))throw;}
        }
        internal void WriteHealth()
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
            health.edgeDocked=edgeDock!=null&&edgeDock.Docked;
            health.chatMode=settings.ChatMode;
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
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=OnDisplaySettingsChanged;
                Microsoft.Win32.SystemEvents.UserPreferenceChanged-=OnSystemPreferenceChanged;
                if(edgeDock!=null)edgeDock.Shutdown();
                if(bridge!=null)bridge.Dispose();
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
