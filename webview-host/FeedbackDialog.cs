using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
namespace DeepSeekFloat {
    // User-initiated feedback: only the typed text, an optional category and
    // an optional contact leave the machine, and only after an explicit send
    // click. Nothing is collected automatically; failures stay inline with
    // the text preserved, and success shows a short thank-you state instead
    // of silently dropping the window.
    internal sealed class FeedbackDialog : Form {
        internal const string EndpointUrl="https://test.doitbenai.cloud/reflex/feedback";
        internal const int MaxChars=1000;
        internal const int ContactMaxChars=100;
        internal static readonly string[] Categories={"问题","建议","其他"};
        private readonly TextBox body=new TextBox {BorderStyle=BorderStyle.None,Multiline=true,BackColor=PanelTheme.Card,ForeColor=PanelTheme.Ink};
        private readonly Label bodyHint=new Label {AutoSize=false,BackColor=Color.Transparent,Cursor=Cursors.IBeam};
        private readonly TextBox contact=new TextBox {BorderStyle=BorderStyle.None,BackColor=PanelTheme.Card,ForeColor=PanelTheme.Ink};
        private readonly Label contactHint=new Label {AutoSize=false,BackColor=Color.Transparent,Cursor=Cursors.IBeam};
        private readonly PanelButton bodyFrame=new PanelButton("") {TabStop=false,InputFrame=true,BackColor=PanelTheme.Surface};
        private readonly PanelButton contactFrame=new PanelButton("") {TabStop=false,InputFrame=true,BackColor=PanelTheme.Surface};
        private readonly PanelButton send=new PanelButton("发送反馈") {Primary=true};
        private readonly PanelButton cancel=new PanelButton("关闭") {Ghost=true,Outline=true};
        private readonly PanelButton[] chips;
        private readonly AnnouncingLabel status=new AnnouncingLabel();
        private readonly Label counter=new Label {AutoSize=false,TextAlign=ContentAlignment.MiddleRight,BackColor=PanelTheme.Back};
        private readonly Label intro=new Label {AutoSize=false,BackColor=PanelTheme.Back};
        private readonly Label heading=new Label {AutoSize=false,BackColor=PanelTheme.Back,TextAlign=ContentAlignment.MiddleLeft};
        private readonly Label privacy=new Label {AutoSize=false,BackColor=PanelTheme.Back};
        private readonly Label thanksMark=new Label {AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,BackColor=PanelTheme.Back};
        private readonly Label thanksText=new Label {AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,BackColor=PanelTheme.Back};
        private readonly ChromeButton close=new ChromeButton("close","关闭反馈");
        private readonly Timer thanksTimer=new Timer {Interval=1400};
        private readonly Func<string,string> poster;
        private float scale;
        private readonly WindowFrame frameShape=new WindowFrame();
        private SmoothFrame smoothFrame;
        internal FeedbackDialog(Func<string,string> testPoster=null) {
            poster=testPoster;
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek-Reflex 反馈";Font=PanelTheme.CompactFont();BackColor=PanelTheme.Back;ForeColor=PanelTheme.Ink;DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(S(420),S(372));Padding=Padding.Empty;
            heading.Text="反馈";heading.Font=PanelTheme.CompactFont(PanelTheme.HeadingSize,true);
            intro.Text="说说遇到的问题、想要的功能，或者任何想法。";
            intro.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);intro.ForeColor=PanelTheme.Muted;
            privacy.Text="只提交你输入的内容和应用版本，不含聊天内容和机器信息。";
            privacy.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);privacy.ForeColor=PanelTheme.Muted;
            body.Font=PanelTheme.CompactFont();body.MaxLength=MaxChars;body.AcceptsReturn=true;body.AccessibleName="反馈内容";
            bodyHint.Text="遇到的问题、想要的功能，或者任何想法…";bodyHint.Font=PanelTheme.CompactFont();bodyHint.ForeColor=PanelTheme.Muted;bodyHint.BackColor=PanelTheme.Card;bodyHint.AccessibleName="反馈内容";
            bodyHint.Click+=delegate{body.Focus();};
            contact.Font=PanelTheme.CompactFont();contact.MaxLength=ContactMaxChars;contact.AccessibleName="联系方式（选填）";
            contactHint.Text="联系方式（选填）：QQ 或邮箱，方便回复你";contactHint.Font=PanelTheme.CompactFont();contactHint.ForeColor=PanelTheme.Muted;contactHint.BackColor=PanelTheme.Card;
            contactHint.Click+=delegate{contact.Focus();};
            bodyFrame.Controls.Add(body);bodyFrame.Controls.Add(bodyHint);
            contactFrame.Controls.Add(contact);contactFrame.Controls.Add(contactHint);
            chips=new PanelButton[Categories.Length];
            for(int i=0;i<chips.Length;i++) {
                var chip=new PanelButton(Categories[i]) {Ghost=true,Segment=true};var index=i;
                chip.AccessibleName=Categories[i]+"分类";
                chip.Click+=delegate{
                    bool on=!chips[index].Chosen;
                    for(int k=0;k<chips.Length;k++)chips[k].Chosen=k==index&&on;
                    if(on)body.Focus();
                };
                chips[i]=chip;
            }
            counter.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);counter.ForeColor=PanelTheme.Muted;
            status.ForeColor=PanelTheme.Muted;
            thanksMark.Text="✓";thanksMark.Font=PanelTheme.CompactFont(PanelTheme.HeadingSize,true);thanksMark.ForeColor=PanelTheme.Accent;thanksMark.Visible=false;
            thanksText.Text="已收到，感谢你的反馈！";thanksText.Font=PanelTheme.CompactFont(PanelTheme.TitleSize);thanksText.Visible=false;
            thanksTimer.Tick+=delegate{thanksTimer.Stop();DialogResult=DialogResult.OK;Close();};
            var surface=new Panel {Dock=DockStyle.Fill,BackColor=PanelTheme.Back};Controls.Add(surface);
            surface.Controls.Add(heading);surface.Controls.Add(close);surface.Controls.Add(intro);
            foreach(var chip in chips)surface.Controls.Add(chip);
            surface.Controls.Add(bodyFrame);surface.Controls.Add(counter);surface.Controls.Add(contactFrame);
            surface.Controls.Add(privacy);surface.Controls.Add(status);
            surface.Controls.Add(cancel);surface.Controls.Add(send);
            surface.Controls.Add(thanksMark);surface.Controls.Add(thanksText);
            heading.MouseDown+=Drag;intro.MouseDown+=Drag;surface.MouseDown+=Drag;privacy.MouseDown+=Drag;
            close.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            send.Enabled=false;
            body.TextChanged+=delegate{
                send.Enabled=body.Text.Trim().Length>0;
                counter.Text=body.Text.Length>=MaxChars*3/4?body.Text.Length+" / "+MaxChars:"";
                bodyHint.Visible=body.Text.Length==0;
                status.Text="";
            };
            contact.TextChanged+=delegate{contactHint.Visible=contact.Text.Length==0;};
            body.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Send();}};
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
            intro.SetBounds(S(20),S(48),width,S(16));
            for(int i=0;i<chips.Length;i++)chips[i].SetBounds(S(20+i*94),S(72),S(88),S(28));
            bodyFrame.SetBounds(S(20),S(108),width,S(118));
            body.SetBounds(S(8),S(6),Math.Max(1,bodyFrame.Width-S(16)),Math.Max(1,bodyFrame.Height-S(12)));
            bodyHint.SetBounds(S(8),S(8),Math.Max(1,bodyFrame.Width-S(16)),S(14));
            counter.SetBounds(S(20),S(230),width,S(14));
            contactFrame.SetBounds(S(20),S(250),width,S(28));
            contact.SetBounds(S(8),Math.Max(0,(contactFrame.Height-contact.Height)/2),Math.Max(1,contactFrame.Width-S(16)),contact.Height);
            contactHint.SetBounds(S(8),Math.Max(0,(contactFrame.Height-S(14))/2),Math.Max(1,contactFrame.Width-S(16)),S(14));
            privacy.SetBounds(S(20),S(286),width,S(14));
            status.SetBounds(S(20),ClientSize.Height-S(60),width,S(14));
            send.SetBounds(ClientSize.Width-S(20)-S(96),ClientSize.Height-S(42),S(96),S(30));
            cancel.SetBounds(ClientSize.Width-S(20)-S(96)-S(6)-S(70),ClientSize.Height-S(42),S(70),S(30));
            thanksMark.SetBounds(S(20),S(120),width,S(44));
            thanksText.SetBounds(S(20),S(172),width,S(24));
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
        private string BuildJson() {
            var json="{\"app\":\"deepseek-reflex\",\"version\":"+PromptInserter.Json(VersionText())+",\"text\":"+PromptInserter.Json(body.Text.Trim());
            for(int i=0;i<chips.Length;i++)if(chips[i].Chosen)json+=",\"category\":"+PromptInserter.Json(Categories[i]);
            if(contact.Text.Trim().Length>0)json+=",\"contact\":"+PromptInserter.Json(contact.Text.Trim());
            return json+"}";
        }
        private async void Send() {
            if(body.Text.Trim().Length==0||!send.Enabled)return;
            var json=BuildJson();
            send.Enabled=false;send.Text="发送中…";cancel.Enabled=false;
            status.ForeColor=PanelTheme.Muted;status.Text="正在发送…";
            var self=this;
            try {
                string failure=null;
                await System.Threading.Tasks.Task.Run(delegate{failure=poster!=null?poster(json):HttpPost(json);});
                if(self.IsDisposed||!self.IsHandleCreated)return;
                if(failure==null){self.ShowThanks();return;}
                self.send.Enabled=true;self.send.Text="发送反馈";self.cancel.Enabled=true;
                self.status.ForeColor=PanelTheme.Error;
                self.status.Text="发送失败："+failure+"。也可到 GitHub 仓库提 Issue。";
            }catch(Exception){if(!self.IsDisposed){self.send.Enabled=true;self.send.Text="发送反馈";self.cancel.Enabled=true;self.status.ForeColor=PanelTheme.Error;self.status.Text="发送失败，请稍后重试。";}}
        }
        private void ShowThanks() {
            heading.Visible=false;close.Visible=false;intro.Visible=false;counter.Visible=false;privacy.Visible=false;
            status.Visible=false;cancel.Visible=false;send.Visible=false;
            foreach(var chip in chips)chip.Visible=false;
            bodyFrame.Visible=false;contactFrame.Visible=false;
            thanksMark.Visible=true;thanksText.Visible=true;thanksText.AccessibleName=thanksText.Text;
            Arrange();thanksTimer.Start();
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
        protected override void Dispose(bool disposing) {
            if(disposing)thanksTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
