using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace DeepSeekFloat {
    // Shared opaque WebView host and cached alpha frame; the origin is always visible.
    internal sealed class PopupWindow : Form {
        internal readonly WebView2 Browser=new WebView2 {DefaultBackgroundColor=Color.White};
        private readonly Panel chrome=new Panel {BackColor=Color.White};
        private readonly Label origin=new Label {AutoEllipsis=true,TextAlign=ContentAlignment.MiddleCenter,ForeColor=PanelTheme.Ink};
        private readonly Label error=new Label {TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.White,ForeColor=PanelTheme.Error,Visible=false};
        private readonly ChromeButton close=new ChromeButton("close","关闭登录或链接窗口");
        private readonly WindowFrame frame=new WindowFrame();
        private SmoothFrame smoothFrame;
        private float scale;
        private bool closing;
        internal PopupWindow(Uri uri,float dpiScale) {
            scale=dpiScale;Text="DeepSeek-Reflex · 登录或链接";BackColor=Color.White;AutoScaleMode=AutoScaleMode.None;
            FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            Size=new Size(S(440),S(640));MinimumSize=new Size(S(320),S(360));
            origin.Text=uri.Host;origin.AccessibleName="当前页面域名";origin.Font=new Font("Segoe UI",9f);
            close.Click+=delegate{Close();};chrome.Controls.AddRange(new Control[]{origin,close});
            chrome.MouseDown+=Drag;origin.MouseDown+=Drag;
            Browser.CoreWebView2InitializationCompleted+=delegate {
                if(IsDisposed||Browser.IsDisposed||Browser.CoreWebView2==null)return;
                var core=Browser.CoreWebView2;
                core.NavigationStarting+=delegate(object sender,Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args){
                    Uri target;if(!Uri.TryCreate(args.Uri,UriKind.Absolute,out target)||target.Scheme!="https"){args.Cancel=true;return;}origin.Text=target.Host;
                };
                core.SourceChanged+=delegate{Uri target;if(Uri.TryCreate(core.Source,UriKind.Absolute,out target))origin.Text=target.Host;};
            };
            Controls.AddRange(new Control[]{Browser,error,chrome,frame});
            smoothFrame=new SmoothFrame(this,scale,Arrange);
            Resize+=delegate{Arrange();};Shown+=delegate{Bounds=SettingsDialog.FitBounds(Bounds,Screen.FromControl(this).WorkingArea,scale);Arrange();};
            FormClosed+=delegate{Browser.Dispose();};Arrange();
        }
        internal void ShowError(string text){error.Text=text;error.Show();error.BringToFront();chrome.BringToFront();frame.BringToFront();}
        internal async Task<bool> InitializeAsync(CoreWebView2Environment environment) {
            if(closing||IsDisposed||Browser.IsDisposed)return false;
            try{await Browser.EnsureCoreWebView2Async(environment);return !closing&&!IsDisposed&&!Browser.IsDisposed&&Browser.CoreWebView2!=null;}
            catch(Exception){if(closing||IsDisposed||Browser.IsDisposed)return false;throw;}
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        private void Arrange(){
            chrome.SetBounds(0,S(WindowResize.Border),ClientSize.Width,S(44)-S(WindowResize.Border));
            origin.SetBounds(S(32),0,Math.Max(1,ClientSize.Width-S(88)),S(32));close.SetBounds(ClientSize.Width-S(40),0,S(32),S(28));
            Browser.SetBounds(S(4),S(44),Math.Max(1,ClientSize.Width-S(8)),Math.Max(1,ClientSize.Height-S(48)));error.Bounds=Browser.Bounds;
            if(smoothFrame!=null)smoothFrame.SyncOwner();frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);
            chrome.BringToFront();frame.BringToFront();
        }
        internal void ApplyDpi(float value,Rectangle bounds){if(value<=0)return;scale=value;MinimumSize=new Size(S(320),S(360));Bounds=bounds;smoothFrame.UpdateScale(value);Arrange();}
        private void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);WindowFrame.ConfigureNativeBorder(Handle);int style=Native.GetWindowLong(Handle,-16);Native.SetWindowLong(Handle,-16,style|0x40000);Native.SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x37);}
        protected override void WndProc(ref Message m){
            if(m.Msg==0x2e0){ApplyDpi(WindowDpi.MessageScale(m.WParam),WindowDpi.Suggested(m.LParam));m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x83||m.Msg==0x85){m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x86&&WindowState!=FormWindowState.Minimized){m.LParam=new IntPtr(-1);base.WndProc(ref m);return;}
            if(m.Msg==0x84&&WindowState==FormWindowState.Normal){int hit=WindowResize.HitTest(PointToClient(WindowResize.ScreenPoint(m.LParam)),ClientSize,S(WindowResize.Border),false,S(WindowFrame.Radius));if(hit!=0){m.Result=new IntPtr(hit);return;}}
            if(m.Msg==0x20){var cursor=WindowResize.CursorFor((int)(m.LParam.ToInt64()&0xffff));if(cursor!=null){Cursor.Current=cursor;m.Result=new IntPtr(1);return;}}
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e){base.OnFormClosing(e);if(!e.Cancel)closing=true;}
        protected override void Dispose(bool disposing){if(disposing){closing=true;if(smoothFrame!=null)smoothFrame.Dispose();}base.Dispose(disposing);}
    }
}
