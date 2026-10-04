using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace DeepSeekFloat
{
    // This is an independent tool window; the main chat form is never shown by these actions.
    internal sealed class QuickAnswerWindow : Form
    {
        private readonly Panel header=new Panel();
        private readonly Label caption=new Label();
        private readonly Label selectionPreview=new Label();
        private readonly Label message=new Label();
        private readonly Button translate=new Button(), explain=new Button(), question=new Button(), retry=new Button();
        private readonly ChromeButton close=new ChromeButton("close","关闭划词浮窗");
        private readonly Timer expiry=new Timer { Interval=20000 };
        private readonly float scale;
        private readonly Point anchor;
        private string text,ownDraft;
        private int pendingAction;
        private WebView2 view;
        private Task<bool> loading;
        private bool busy, disposed, submitted;
        internal bool Expanded { get; private set; }
        internal QuickAnswerWindow(string text,Point anchor)
        {
            this.text=text;this.anchor=anchor;
            using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96f;
            Text="DeepSeek-Reflex 划词浮窗";AutoScaleMode=AutoScaleMode.None;
            KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) { e.Handled=true;Close(); } };
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.White;
            Font=new Font("Microsoft YaHei UI",9f);
            header.BackColor=Color.White;
            foreach(var b in new[]{translate,explain,question,retry}) {
                b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;
                b.BackColor=Color.FromArgb(242,245,253);b.ForeColor=Color.FromArgb(52,82,180);
            }
            translate.Text="翻译";explain.Text="解释";question.Text="问一问";retry.Text="登录后继续";
            translate.Click+=async delegate { await Request(0); };
            explain.Click+=async delegate { await Request(1); };
            question.Click+=async delegate { await Request(2); };
            retry.Click+=async delegate { await Request(pendingAction); };
            close.Click+=delegate { Close(); };
            caption.Text="划词 · DeepSeek-Reflex";caption.ForeColor=Color.FromArgb(78,87,107);caption.TextAlign=ContentAlignment.MiddleLeft;
            caption.MouseDown+=delegate(object s,MouseEventArgs e) { if(Expanded && e.Button==MouseButtons.Left) { Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero); } };
            selectionPreview.Text=text.Replace('\r',' ').Replace('\n',' ');selectionPreview.AutoEllipsis=true;selectionPreview.ForeColor=Color.FromArgb(100,110,130);
            selectionPreview.TextAlign=ContentAlignment.MiddleLeft;
            message.TextAlign=ContentAlignment.MiddleLeft;message.ForeColor=Color.FromArgb(85,95,115);
            header.Controls.AddRange(new Control[]{caption,translate,explain,question,close});
            Controls.AddRange(new Control[]{header,selectionPreview,message,retry});
            expiry.Tick+=delegate { if(!Expanded)Close(); };expiry.Start();
            Resize+=delegate { LayoutPopup(); };
            Size=new Size(S(256),S(44));Place();LayoutPopup();
        }
        private int S(int value) { return (int)Math.Round(value*scale); }
        protected override bool ShowWithoutActivation { get { return !Expanded; } }
        protected override CreateParams CreateParams { get { var p=base.CreateParams;p.ExStyle|=0x80;return p; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);int corner=2;Native.DwmSetWindowAttribute(Handle,33,ref corner,4); }
        private void Place()
        {
            var area=Screen.FromPoint(anchor).WorkingArea;
            int x=Math.Max(area.Left,Math.Min(anchor.X+S(12),area.Right-Width));
            int y=anchor.Y+S(18);if(y+Height>area.Bottom)y=anchor.Y-Height-S(12);
            Location=new Point(x,Math.Max(area.Top,Math.Min(y,area.Bottom-Height)));
        }
        private void LayoutPopup()
        {
            header.SetBounds(0,0,ClientSize.Width,S(44));
            caption.Visible=Expanded;
            if(Expanded)caption.SetBounds(S(12),0,Math.Max(0,ClientSize.Width-S(244)),S(44));
            int start=Expanded?ClientSize.Width-S(230):S(8);
            translate.SetBounds(start,S(7),S(56),S(30));explain.SetBounds(start+S(62),S(7),S(56),S(30));question.SetBounds(start+S(124),S(7),S(64),S(30));
            close.SetBounds(ClientSize.Width-S(36),S(7),S(28),S(30));
            selectionPreview.Visible=message.Visible=retry.Visible=Expanded;
            selectionPreview.SetBounds(S(12),S(44),ClientSize.Width-S(24),S(30));
            message.SetBounds(S(12),S(74),Math.Max(0,ClientSize.Width-S(152)),S(32));retry.SetBounds(ClientSize.Width-S(132),S(75),S(120),S(28));
            if(view!=null)view.SetBounds(S(4),S(110),ClientSize.Width-S(8),Math.Max(0,ClientSize.Height-S(114)));
        }
        private void Expand()
        {
            if(Expanded)return;Expanded=true;expiry.Stop();
            var area=Screen.FromPoint(anchor).WorkingArea;
            Size=new Size(Math.Min(S(440),area.Width),Math.Min(S(540),area.Height));
            view=new WebView2 { DefaultBackgroundColor=Color.White,AccessibleName="独立划词问答" };
            Controls.Add(view);LayoutPopup();Place();
        }
        private async Task<bool> LoadWebsite()
        {
            try {
                var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Preferences.Root,"webview2-profile"));
                if(disposed)return false;
                await view.EnsureCoreWebView2Async(environment);
                if(disposed)return false;
                var core=view.CoreWebView2;core.Settings.AreHostObjectsAllowed=false;core.Settings.IsWebMessageEnabled=false;core.Settings.AreDevToolsEnabled=false;
                core.NavigationStarting+=delegate(object s,CoreWebView2NavigationStartingEventArgs e) { Uri uri;if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)||uri.Scheme!="https")e.Cancel=true; };
                core.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs e) { e.Handled=true;Uri uri;if(Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)&&uri.Scheme=="https")core.Navigate(e.Uri); };
                var loaded=new TaskCompletionSource<bool>();
                core.NavigationCompleted+=delegate(object s,CoreWebView2NavigationCompletedEventArgs e) { loaded.TrySetResult(e.IsSuccess); };
                core.Navigate("https://chat.deepseek.com/");
                if(await Task.WhenAny(loaded.Task,Task.Delay(20000))!=loaded.Task)return false;
                if(!await loaded.Task)return false;
                await Task.Delay(1000);return !disposed;
            }catch(Exception) { return false; }
        }
        private async Task Request(int action)
        {
            if(busy||disposed||text==null)return;
            // Another request needs an explicitly created new selection window. Never repeat a send.
            if(submitted) { message.Text="继续提问可直接使用下方对话框";return; }
            busy=true;pendingAction=action;Expand();retry.Enabled=false;
            message.Text="正在准备独立问答…";
            try {
                if(loading==null)loading=LoadWebsite();
                if(!await loading) { if(!disposed) { message.Text="页面未打开，点击继续重试";loading=null; }return; }
                if(disposed)return;
                string prompt=PromptInserter.Compose(text,action);
                var result=await PromptInserter.InsertAsync(view.CoreWebView2,prompt,ownDraft);
                if(disposed)return;
                if(result=="no-editor" || result=="wrong-host") { message.Text="请在下方登录，再点继续";return; }
                if(result=="draft") { message.Text="下方已有草稿，请处理后继续";return; }
                if(result!="filled") { message.Text="填入未完成，请重试";return; }
                ownDraft=prompt;
                await Task.Delay(200);
                if(disposed)return;
                Activate();Native.SetForegroundWindow(Handle);view.Focus();
                // Only target this independent official website window, never another application's input.
                Uri current;
                if(Native.GetForegroundWindow()!=Handle || !Uri.TryCreate(view.CoreWebView2.Source,UriKind.Absolute,out current) || current.Scheme!="https" || current.Host!="chat.deepseek.com") {
                    message.Text="请求已填入，请点击下方发送";return;
                }
                if(Native.GetAsyncKeyState(0x11)<0 || Native.GetAsyncKeyState(0x10)<0 || Native.GetAsyncKeyState(0x12)<0) { message.Text="请松开按键，在下方点击发送";return; }
                var input=new[]{Native.Key(0x0D,false),Native.Key(0x0D,true)};
                uint sent=Native.SendInput(2,input,System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.INPUT)));
                submitted=true;
                message.Text=sent==2 ? "查看下方回答，可继续追问" : "请点击下方发送";
                retry.Text="在下方继续";retry.Enabled=false;
            }catch(Exception) { if(!disposed)message.Text="问答未完成，可在下方继续"; }
            finally { busy=false;if(!disposed)retry.Enabled=!submitted; }
        }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x21&&!Expanded) { m.Result=new IntPtr(3);return; }
            if(m.Msg==0x84&&Expanded) {
                var p=PointToClient(new Point((short)(m.LParam.ToInt64()&0xffff),(short)(m.LParam.ToInt64()>>16)));int edge=S(5);
                bool l=p.X<edge,r=p.X>=ClientSize.Width-edge,t=p.Y<edge,b=p.Y>=ClientSize.Height-edge;
                int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0;
                if(hit!=0) { m.Result=new IntPtr(hit);return; }
            }
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!disposed) { disposed=true;text=null;ownDraft=null;expiry.Stop();expiry.Dispose();if(view!=null)view.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
