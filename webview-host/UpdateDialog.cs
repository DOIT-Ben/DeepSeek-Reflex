using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class UpdateDialog:Form {
        private readonly UpdateCoordinator coordinator;
        private readonly Panel surface=new Panel {Dock=DockStyle.Fill,AutoScroll=true,BackColor=PanelTheme.Back};
        private readonly Panel content=new Panel {BackColor=PanelTheme.Back};
        private readonly Label heading=new Label {Text="版本与更新",BackColor=Color.Transparent};
        private readonly Label versions=new Label {BackColor=Color.Transparent};
        private readonly AnnouncingLabel status=new AnnouncingLabel();
        private readonly Label notesHeading=new Label {Text="更新说明",BackColor=Color.Transparent};
        private readonly Label privacy=new Label {Text="仅更新程序文件，保留设置和登录状态。",BackColor=Color.Transparent};
        private readonly TextBox notes=new EditorTextBox {Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,ScrollBars=ScrollBars.Vertical,TabStop=true};
        private readonly InputPanel notesFrame=new InputPanel {TabStop=false,BackColor=PanelTheme.Back};
        private readonly PanelSwitch auto=new PanelSwitch("自动检查正式版本");
        private readonly ProgressBar progress=new ProgressBar {Minimum=0,Maximum=100,Style=ProgressBarStyle.Continuous};
        private readonly PanelButton action=new PanelButton("检查更新") {Primary=true};
        private readonly PanelButton cancel=new PanelButton("取消") {Ghost=true};
        private readonly LinkLabel releaseLink=new LinkLabel {Text="GitHub 更新公告",LinkBehavior=LinkBehavior.HoverUnderline};
        private readonly ChromeButton close=new ChromeButton("close","关闭更新");
        private readonly WindowFrame frame=new WindowFrame();
        private SmoothFrame smoothFrame;private float scale;private bool rendering,arranging;
        internal UpdateDialog(UpdateCoordinator coordinator) {
            this.coordinator=coordinator;using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96f;
            Text="DeepSeek-Reflex 更新";BackColor=PanelTheme.Back;ForeColor=PanelTheme.Ink;Font=PanelTheme.CompactFont();DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;ShowInTaskbar=false;MaximizeBox=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.None;
            ClientSize=new Size(S(440),S(448));Controls.Add(surface);surface.Controls.Add(content);
            foreach(var c in new Control[]{heading,versions,status,notesHeading,privacy,notesFrame,auto,progress,action,cancel,releaseLink,close})content.Controls.Add(c);
            heading.Font=PanelTheme.CompactFont(PanelTheme.HeadingSize,true);versions.Font=PanelTheme.CompactFont();versions.ForeColor=PanelTheme.Muted;
            notesHeading.Font=PanelTheme.CompactFont(PanelTheme.BodySize,true);notes.Font=PanelTheme.CompactFont();notes.BackColor=PanelTheme.Back;notes.ForeColor=PanelTheme.Muted;notes.AccessibleName="更新说明（只读）";
            notesFrame.Controls.Add(notes);notes.Enter+=delegate{notesFrame.Accent=true;};notes.Leave+=delegate{notesFrame.Accent=false;};
            privacy.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);privacy.ForeColor=PanelTheme.Muted;
            status.Font=PanelTheme.CompactFont();status.ForeColor=PanelTheme.Ink;
            releaseLink.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);releaseLink.LinkColor=PanelTheme.Accent;releaseLink.ActiveLinkColor=PanelTheme.AccentPressed;
            releaseLink.Click+=delegate{try{
                string target=coordinator.Release==null||coordinator.Release.Asset==null?null:coordinator.Release.Asset.browser_download_url;
                if(string.IsNullOrEmpty(target))target="https://github.com/DOIT-Ben/DeepSeek-Reflex/releases";
                UpdateFeed.ValidateUri(new Uri(target));System.Diagnostics.Process.Start(target);
            }catch(Exception){}};
            auto.Checked=coordinator.Auto;auto.Click+=delegate{if(!rendering)coordinator.SetAuto(auto.Checked);};
            action.Click+=async delegate{if(coordinator.State==UpdateStatus.Ready)await coordinator.InstallAsync();else if(coordinator.State==UpdateStatus.Available||coordinator.State==UpdateStatus.DownloadFailed)await coordinator.DownloadAsync();else await coordinator.CheckAsync();};
            cancel.Click+=delegate{coordinator.Cancel();};close.Click+=delegate{Close();};
            heading.MouseDown+=Drag;content.MouseDown+=Drag;
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(coordinator.State==UpdateStatus.Preparing&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;return;}coordinator.Cancel();};
            KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){e.Handled=true;Close();}};
            Shown+=delegate{Arrange();close.Focus();};Resize+=delegate{Arrange();if(smoothFrame!=null)smoothFrame.SyncOwner();frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);};
            Controls.Add(frame);smoothFrame=new SmoothFrame(this,scale,delegate{frame.UpdateShape(this,scale,smoothFrame.Ready);},false);frame.UpdateShape(this,scale);
            OwnedWindowPlacement.Attach(this,delegate{return scale;},ApplyDpi);coordinator.Changed+=Render;Render();
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        private void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}
        private void Render() {
            if(IsDisposed)return;rendering=true;
            try {
                var state=coordinator.State;
                releaseLink.Text=coordinator.Release==null?"项目发布页":"手动下载安装包";
                versions.Text="当前 v"+coordinator.Current+(coordinator.Release==null?"":"    最新正式版 v"+coordinator.Release.Version);
                status.Text=coordinator.Message;status.ForeColor=state==UpdateStatus.CheckFailed||state==UpdateStatus.DownloadFailed?PanelTheme.Error:PanelTheme.Ink;
                string text=coordinator.Release==null?"检查更新后，可在这里查看新版的更新说明。":string.IsNullOrWhiteSpace(coordinator.Release.body)?"此版本未提供更新说明。":coordinator.Release.body;
                if(notes.Text!=text)notes.Text=text;
                action.Text=state==UpdateStatus.Ready?"安装并重启":state==UpdateStatus.Preparing?"准备中…":state==UpdateStatus.Available?"下载更新":state==UpdateStatus.DownloadFailed?"重试下载":state==UpdateStatus.Checking?"检查中…":state==UpdateStatus.Downloading?"下载中…":state==UpdateStatus.CheckFailed?"重新检查":"检查更新";
                action.Enabled=!coordinator.Busy;auto.Enabled=!coordinator.Busy;auto.Checked=coordinator.Auto;close.Enabled=state!=UpdateStatus.Preparing;
                cancel.Visible=state==UpdateStatus.Downloading||state==UpdateStatus.Checking;cancel.Enabled=cancel.Visible;
                progress.Visible=state==UpdateStatus.Downloading;progress.Value=coordinator.Release==null?0:(int)Math.Min(100,coordinator.Downloaded*100/Math.Max(1,coordinator.Release.Asset.size));
                if(progress.Visible)status.Text="正在下载更新… "+progress.Value+"%";
            }finally{rendering=false;}Arrange();
        }
        private void Arrange() {
            if(arranging)return;arranging=true;try {
                surface.AutoScrollMinSize=new Size(0,S(448));content.SetBounds(surface.AutoScrollPosition.X,surface.AutoScrollPosition.Y,Math.Max(1,surface.ClientSize.Width),S(448));int w=content.Width-S(40);
                heading.SetBounds(S(20),S(16),Math.Max(1,w-S(40)),S(28));close.SetBounds(content.Width-S(52),S(16),S(32),S(28));
                versions.SetBounds(S(20),S(60),w,S(22));status.SetBounds(S(20),S(96),w,S(42));progress.SetBounds(S(20),S(142),w,S(5));
                notesHeading.SetBounds(S(20),S(166),w,S(22));notesFrame.SetBounds(S(20),S(196),w,S(120));notes.SetBounds(S(10),S(8),Math.Max(1,w-S(20)),S(104));
                auto.SetBounds(S(20),S(332),w,S(30));privacy.SetBounds(S(20),S(368),w,S(18));
                releaseLink.SetBounds(S(20),S(402),Math.Max(1,w-S(216)),S(26));cancel.SetBounds(content.Width-S(222),S(400),S(80),S(30));action.SetBounds(content.Width-S(132),S(400),S(112),S(30));
            }finally{arranging=false;}
        }
        internal void ApplyDpi(float next,Rectangle suggested){if(next<=0)return;float ratio=next/scale;scale=next;surface.Scale(new SizeF(ratio,ratio));Bounds=SettingsDialog.FitBounds(suggested,Screen.FromRectangle(suggested).WorkingArea,scale);Arrange();if(smoothFrame!=null)smoothFrame.UpdateScale(scale);frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);}
        protected override void WndProc(ref Message message){if(message.Msg==0x2e0){ApplyDpi(WindowDpi.MessageScale(message.WParam),WindowDpi.Suggested(message.LParam));message.Result=IntPtr.Zero;return;}base.WndProc(ref message);}
        protected override void Dispose(bool disposing){if(disposing){coordinator.Changed-=Render;if(smoothFrame!=null)smoothFrame.Dispose();}base.Dispose(disposing);}
    }
}
