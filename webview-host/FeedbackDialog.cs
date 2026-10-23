using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
namespace DeepSeekFloat {
    // User-initiated feedback: only the typed text and app version leave the
    // machine, and only after an explicit send click. Nothing is collected
    // automatically; failures stay inline with the text preserved.
    internal sealed class FeedbackDialog : Form {
        internal const string EndpointUrl="https://test.doitbenai.cloud/reflex/feedback";
        internal const int MaxChars=1000;
        private readonly TextBox body=new TextBox {BorderStyle=BorderStyle.None,Multiline=true};
        private readonly PanelButton send=new PanelButton("发送") {Primary=true};
        private readonly PanelButton cancel=new PanelButton("取消");
        private readonly AnnouncingLabel status=new AnnouncingLabel();
        private readonly Label counter=new Label {AutoSize=false,TextAlign=ContentAlignment.MiddleRight};
        private readonly Label intro=new Label {AutoSize=false,BackColor=Color.White};
        private readonly Label heading=new Label {AutoSize=false,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft};
        private readonly ChromeButton close=new ChromeButton("close","关闭反馈");
        private readonly PanelButton frame=new PanelButton("") {TabStop=false,InputFrame=true,BackColor=PanelTheme.Surface};
        private readonly Func<string,string> poster;
        private float scale;
        private readonly WindowFrame frameShape=new WindowFrame();
        private SmoothFrame smoothFrame;
        internal FeedbackDialog(Func<string,string> testPoster=null) {
            poster=testPoster;
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek-Reflex 反馈";Font=PanelTheme.CompactFont();BackColor=Color.White;ForeColor=PanelTheme.Ink;DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(S(380),S(268));Padding=Padding.Empty;
            heading.Text="反馈";heading.Font=PanelTheme.CompactFont(PanelTheme.HeadingSize,true);
            intro.Text="说说哪里不顺手，或想要什么功能。\n只提交这段文字和应用版本，不含聊天内容和机器信息。";
            intro.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);intro.ForeColor=PanelTheme.Muted;
            body.Font=PanelTheme.CompactFont();body.MaxLength=MaxChars;body.AcceptsReturn=true;body.AccessibleName="反馈内容";
            frame.Controls.Add(body);
            counter.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);counter.ForeColor=PanelTheme.Muted;counter.Text="0 / "+MaxChars;
            status.ForeColor=PanelTheme.Muted;
            var surface=new Panel {Dock=DockStyle.Fill,BackColor=Color.White};Controls.Add(surface);
            surface.Controls.Add(heading);surface.Controls.Add(close);surface.Controls.Add(intro);
            surface.Controls.Add(frame);surface.Controls.Add(counter);surface.Controls.Add(status);
            surface.Controls.Add(cancel);surface.Controls.Add(send);
            heading.MouseDown+=Drag;intro.MouseDown+=Drag;surface.MouseDown+=Drag;
            close.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            send.Enabled=false;
            body.TextChanged+=delegate{
                send.Enabled=body.Text.Trim().Length>0;
                counter.Text=body.Text.Length+" / "+MaxChars;
                status.Text="";
            };
            send.Click+=delegate{Send();};
            CancelButton=cancel;
            Shown+=delegate{Bounds=SettingsDialog.FitBounds(Bounds,Screen.FromControl(this).WorkingArea,scale);Resize+=delegate{Arrange();};Arrange();};
            Controls.Add(frameShape);smoothFrame=new SmoothFrame(this,scale,delegate{frameShape.UpdateShape(this,scale,smoothFrame.Ready);},false);
            Resize+=delegate{if(smoothFrame!=null)smoothFrame.SyncOwner();frameShape.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);};
            frameShape.UpdateShape(this,scale);
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        private void Arrange() {
            int width=Math.Max(1,ClientSize.Width-S(40));
            heading.SetBounds(S(20),S(14),S(292),S(26));
            close.SetBounds(ClientSize.Width-S(52),S(14),S(32),S(28));
            intro.SetBounds(S(20),S(48),width,S(30));
            frame.SetBounds(S(20),S(86),width,S(96));
            body.SetBounds(S(8),S(6),Math.Max(1,frame.Width-S(16)),Math.Max(1,frame.Height-S(12)));
            counter.SetBounds(S(20),S(186),width,S(14));
            status.SetBounds(S(20),ClientSize.Height-S(64),width,S(16));
            send.SetBounds(ClientSize.Width-S(20)-S(86),ClientSize.Height-S(42),S(86),S(30));
            cancel.SetBounds(ClientSize.Width-S(20)-S(86)-S(6)-S(70),ClientSize.Height-S(42),S(70),S(30));
        }
        private void Drag(object sender,MouseEventArgs e) {
            if(e.Button!=MouseButtons.Left)return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);
        }
        internal static string VersionText() {
            var version=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version!=null&&version.Major>0?"v"+version.ToString(3):"dev";
        }
        private async void Send() {
            var text=body.Text.Trim();
            if(text.Length==0||!send.Enabled)return;
            send.Enabled=false;status.ForeColor=PanelTheme.Muted;status.Text="正在发送…";
            var json="{\"app\":\"deepseek-reflex\",\"version\":"+PromptInserter.Json(VersionText())+",\"text\":"+PromptInserter.Json(text)+"}";
            var self=this;
            try {
                string failure=null;
                await System.Threading.Tasks.Task.Run(delegate{failure=poster!=null?poster(json):HttpPost(json);});
                if(self.IsDisposed||!self.IsHandleCreated)return;
                if(failure==null){self.DialogResult=DialogResult.OK;self.Close();return;}
                self.send.Enabled=true;
                self.status.ForeColor=PanelTheme.Error;
                self.status.Text="发送失败："+failure+"。也可到 GitHub 仓库提 Issue。";
            }catch(Exception){if(!self.IsDisposed){self.send.Enabled=true;self.status.ForeColor=PanelTheme.Error;self.status.Text="发送失败，请稍后重试。";}}
        }
        internal static string HttpPost(string json) {
            try {
                var request=(System.Net.HttpWebRequest)System.Net.WebRequest.Create(EndpointUrl);
                request.Method="POST";request.ContentType="application/json; charset=utf-8";
                request.Timeout=8000;request.ReadWriteTimeout=8000;request.UserAgent="DeepSeek-Reflex";
                var payload=Encoding.UTF8.GetBytes(json);request.ContentLength=payload.Length;
                using(var stream=request.GetRequestStream())stream.Write(payload,0,payload.Length);
                using(var response=(System.Net.HttpWebResponse)request.GetResponse()) {
                    int code=(int)response.StatusCode;
                    return code>=200&&code<300?null:"服务器返回 "+code;
                }
            }
            catch(System.Net.WebException error) {
                var response=error.Response as System.Net.HttpWebResponse;
                if(response!=null)return "服务器返回 "+(int)response.StatusCode;
                return error.Message;
            }
            catch(Exception error){return error.Message;}
        }
    }
}
