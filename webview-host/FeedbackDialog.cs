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
        private readonly TextBox body=new EditorTextBox {BorderStyle=BorderStyle.None,Multiline=true,BackColor=PanelTheme.Back,ForeColor=PanelTheme.Ink};
        private readonly Label bodyHint=new Label {AutoSize=false,BackColor=Color.Transparent,Cursor=Cursors.IBeam};
        private readonly TextBox contact=new EditorTextBox {BorderStyle=BorderStyle.None,BackColor=PanelTheme.Back,ForeColor=PanelTheme.Ink};
        private readonly Label contactHint=new Label {AutoSize=false,BackColor=Color.Transparent,Cursor=Cursors.IBeam};
        private readonly InputPanel bodyFrame=new InputPanel {TabStop=false,BackColor=PanelTheme.Back};
        private readonly InputPanel contactFrame=new InputPanel {TabStop=false,BackColor=PanelTheme.Back};
        private readonly PanelButton send=new PanelButton("发送反馈") {Primary=true};
        private readonly PanelButton[] chips;
        private readonly AnnouncingLabel status=new AnnouncingLabel();
        private readonly LinkLabel issueLink=new LinkLabel {AutoSize=false,BackColor=PanelTheme.Back,LinkBehavior=LinkBehavior.HoverUnderline,Cursor=Cursors.Hand};
        private readonly Label counter=new Label {AutoSize=false,TextAlign=ContentAlignment.MiddleRight,BackColor=PanelTheme.Back};
        private readonly Label heading=new Label {AutoSize=false,BackColor=PanelTheme.Back,TextAlign=ContentAlignment.MiddleLeft};
        private readonly Label privacy=new Label {AutoSize=false,BackColor=PanelTheme.Back};
        private readonly Label categoryLabel=new Label {Text="反馈类型",BackColor=PanelTheme.Back,TextAlign=ContentAlignment.MiddleLeft};
        private readonly Label bodyLabel=new Label {Text="反馈内容",BackColor=PanelTheme.Back};
        private readonly Label contactLabel=new Label {Text="联系方式（选填）",BackColor=PanelTheme.Back};
        private readonly Label keyboardHint=new Label {Text="Ctrl+Enter 发送",BackColor=PanelTheme.Back,TextAlign=ContentAlignment.MiddleLeft};
        private readonly Panel surface=new Panel {Dock=DockStyle.Fill,AutoScroll=true,BackColor=PanelTheme.Back};
        private readonly Panel content=new Panel {BackColor=PanelTheme.Back};
        private readonly Label thanksMark=new Label {AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,BackColor=PanelTheme.Back};
        private readonly Label thanksText=new Label {AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,BackColor=PanelTheme.Back};
        private readonly ChromeButton close=new ChromeButton("close","关闭反馈");
        private readonly Timer thanksTimer=new Timer {Interval=1400};
        private readonly Func<string,string> poster;
        private float scale;
        private bool sending,showFailure,arranging,thanking;
        private readonly WindowFrame frameShape=new WindowFrame();
        private SmoothFrame smoothFrame;
        internal FeedbackDialog(Func<string,string> testPoster=null) {
            poster=testPoster;
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek-Reflex 反馈";Font=PanelTheme.CompactFont();BackColor=PanelTheme.Back;ForeColor=PanelTheme.Ink;DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(S(420),S(408));Padding=Padding.Empty;
            heading.Text="反馈";heading.Font=PanelTheme.CompactFont(PanelTheme.HeadingSize,true);
            privacy.Text="仅发送本页填写的内容与应用版本。\n不读取聊天记录或设备信息。";
            privacy.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);privacy.ForeColor=PanelTheme.Muted;
            foreach(var label in new[]{categoryLabel,bodyLabel,contactLabel})label.Font=PanelTheme.CompactFont();
            keyboardHint.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);keyboardHint.ForeColor=PanelTheme.Muted;
            body.Font=PanelTheme.CompactFont();body.MaxLength=MaxChars;body.AcceptsReturn=true;body.AccessibleName="反馈内容";
            bodyHint.Text="请描述遇到的情况、操作步骤与预期结果…";bodyHint.Font=PanelTheme.CompactFont();bodyHint.ForeColor=PanelTheme.Muted;bodyHint.BackColor=PanelTheme.Back;bodyHint.AccessibleName="反馈内容提示";
            bodyHint.Click+=delegate{body.Focus();};
            contact.Font=PanelTheme.CompactFont();contact.MaxLength=ContactMaxChars;contact.AccessibleName="联系方式（选填）";
            contactHint.Text="QQ 或邮箱，方便我们回复你";contactHint.Font=PanelTheme.CompactFont();contactHint.ForeColor=PanelTheme.Muted;contactHint.BackColor=PanelTheme.Back;
            contactHint.Click+=delegate{contact.Focus();};
            bodyFrame.Controls.Add(body);bodyFrame.Controls.Add(bodyHint);
            contactFrame.Controls.Add(contact);contactFrame.Controls.Add(contactHint);
            // Native TextBox HWNDs otherwise cover these labels even though
            // DrawToBitmap still appears to show the placeholders.
            bodyHint.BringToFront();contactHint.BringToFront();
            body.Enter+=delegate{bodyFrame.Accent=true;};body.Leave+=delegate{bodyFrame.Accent=false;};
            contact.Enter+=delegate{contactFrame.Accent=true;};contact.Leave+=delegate{contactFrame.Accent=false;};
            bodyFrame.Click+=delegate{body.Focus();};contactFrame.Click+=delegate{contact.Focus();};
            chips=new PanelButton[Categories.Length];
            for(int i=0;i<chips.Length;i++) {
                var chip=new PanelButton(Categories[i]) {Ghost=true,Segment=true,Chosen=i==0};var index=i;
                chip.AccessibleName=Categories[i]+"分类";
                chip.Click+=delegate{
                    if(sending)return;
                    for(int k=0;k<chips.Length;k++)chips[k].Chosen=k==index;
                    bodyHint.Text=index==0?"请描述遇到的情况、操作步骤与预期结果…":index==1?"希望增加什么功能，或者怎样用起来更顺手？":"有什么想告诉我们？";
                };
                chips[i]=chip;
            }
            counter.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);counter.ForeColor=PanelTheme.Muted;counter.Text="0 / "+MaxChars;
            status.ForeColor=PanelTheme.Muted;
            issueLink.Text="到 GitHub 仓库提 Issue";
            issueLink.Font=PanelTheme.CompactFont();issueLink.LinkColor=PanelTheme.Accent;issueLink.ActiveLinkColor=PanelTheme.AccentPressed;issueLink.Visible=false;
            issueLink.Click+=delegate{try{System.Diagnostics.Process.Start("https://github.com/DOIT-Ben/DeepSeek-Reflex/issues");}catch(Exception){}};
            thanksMark.Text="✓";thanksMark.Font=PanelTheme.CompactFont(PanelTheme.HeadingSize,true);thanksMark.ForeColor=PanelTheme.Accent;thanksMark.Visible=false;
            thanksText.Text="已收到，感谢你的反馈！";thanksText.Font=PanelTheme.CompactFont(PanelTheme.TitleSize);thanksText.Visible=false;
            thanksTimer.Tick+=delegate{thanksTimer.Stop();DialogResult=DialogResult.OK;Close();};
            Controls.Add(surface);
            surface.Controls.Add(content);
            content.Controls.Add(heading);content.Controls.Add(close);
            content.Controls.Add(categoryLabel);content.Controls.Add(bodyLabel);content.Controls.Add(contactLabel);content.Controls.Add(keyboardHint);
            foreach(var chip in chips)content.Controls.Add(chip);
            content.Controls.Add(bodyFrame);content.Controls.Add(counter);content.Controls.Add(contactFrame);
            content.Controls.Add(privacy);content.Controls.Add(status);content.Controls.Add(issueLink);
            content.Controls.Add(send);
            content.Controls.Add(thanksMark);content.Controls.Add(thanksText);
            heading.MouseDown+=Drag;surface.MouseDown+=Drag;content.MouseDown+=Drag;privacy.MouseDown+=Drag;
            close.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            send.Enabled=false;
            body.TextChanged+=delegate{
                send.Enabled=!sending&&body.Text.Trim().Length>0;
                counter.Text=body.Text.Length+" / "+MaxChars;
                bodyHint.Visible=body.Text.Length==0;
                if(!sending){status.Text="";issueLink.Visible=false;showFailure=false;Arrange();}
            };
            contact.TextChanged+=delegate{contactHint.Visible=contact.Text.Length==0;};
            body.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Send();}};
            contact.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Send();}};
            send.Click+=delegate{Send();};
            // The X chrome button and Escape both close; no extra close button.
            KeyPreview=true;
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){e.Handled=true;DialogResult=DialogResult.Cancel;Close();}};
            var remembered=Preferences.ReadText("feedback-contact");
            if(remembered.Length>0)contact.Text=remembered;
            Shown+=delegate{Bounds=SettingsDialog.FitBounds(Bounds,Screen.FromControl(this).WorkingArea,scale);Resize+=delegate{Arrange();};Arrange();body.Focus();};
            Controls.Add(frameShape);smoothFrame=new SmoothFrame(this,scale,delegate{frameShape.UpdateShape(this,scale,smoothFrame.Ready);},false);
            Resize+=delegate{if(smoothFrame!=null)smoothFrame.SyncOwner();frameShape.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);};
            frameShape.UpdateShape(this,scale);
            OwnedWindowPlacement.Attach(this,delegate{return scale;},ApplyDpi);
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        private void Arrange() {
            if(arranging)return;arranging=true;
            try {
            int desiredHeight=S(showFailure?472:408);
            var area=(IsHandleCreated?Screen.FromControl(this):Screen.FromRectangle(Bounds)).WorkingArea;
            var fitted=SettingsDialog.FitBounds(new Rectangle(Location,new Size(Width,desiredHeight)),area,scale);
            if(Bounds!=fitted)Bounds=fitted;
            // Keep the logical form intact on short work areas. The scroll
            // surface exposes every field and action without undoing FitBounds.
            surface.AutoScrollMinSize=new Size(0,desiredHeight);
            content.SetBounds(surface.AutoScrollPosition.X,surface.AutoScrollPosition.Y,Math.Max(1,surface.ClientSize.Width),desiredHeight);
            int layoutWidth=content.ClientSize.Width;
            int width=Math.Max(1,layoutWidth-S(40));
            heading.SetBounds(S(20),S(14),S(292),S(26));
            close.SetBounds(layoutWidth-S(52),S(14),S(32),S(28));
            categoryLabel.SetBounds(S(20),S(60),S(96),S(28));
            int chipWidth=Math.Max(1,(width-S(104)-S(16))/3);
            for(int i=0;i<chips.Length;i++)chips[i].SetBounds(S(124)+i*(chipWidth+S(8)),S(60),chipWidth,S(28));
            bodyLabel.SetBounds(S(20),S(104),S(160),S(18));
            counter.SetBounds(S(180),S(104),Math.Max(1,width-S(160)),S(18));
            bodyFrame.SetBounds(S(20),S(128),width,S(112));
            body.SetBounds(S(8),S(6),Math.Max(1,bodyFrame.Width-S(16)),Math.Max(1,bodyFrame.Height-S(12)));
            bodyHint.SetBounds(S(8),S(8),Math.Max(1,bodyFrame.Width-S(16)),S(14));
            contactLabel.SetBounds(S(20),S(256),width,S(18));
            contactFrame.SetBounds(S(20),S(280),width,S(32));
            contact.SetBounds(S(8),Math.Max(0,(contactFrame.Height-contact.Height)/2),Math.Max(1,contactFrame.Width-S(16)),contact.Height);
            contactHint.SetBounds(S(8),Math.Max(0,(contactFrame.Height-S(14))/2),Math.Max(1,contactFrame.Width-S(16)),S(14));
            privacy.SetBounds(S(20),S(324),width,S(28));
            // Failure reasons can be long; two wrapped lines keep the whole
            // message readable instead of clipping mid-sentence, and the
            // issue link sits right beneath as the alternate channel.
            int footerTop=desiredHeight-S(42);
            status.Visible=!thanking&&(sending||showFailure);keyboardHint.Visible=!thanking&&!sending;
            status.SetBounds(S(20),showFailure?S(364):footerTop,showFailure?width:Math.Max(1,width-S(108)),showFailure?S(32):S(30));
            issueLink.SetBounds(S(20),S(400),width,S(18));
            keyboardHint.SetBounds(S(20),footerTop,Math.Max(1,width-S(108)),S(30));
            send.SetBounds(layoutWidth-S(20)-S(96),footerTop,S(96),S(30));
            thanksMark.SetBounds(S(20),S(120),width,S(44));
            thanksText.SetBounds(S(20),S(172),width,S(24));
            }finally{arranging=false;}
        }
        private void Drag(object sender,MouseEventArgs e) {
            if(e.Button!=MouseButtons.Left)return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);
        }
        internal void ApplyDpi(float value,Rectangle bounds) {
            if(value<=0)return;
            float ratio=value/scale;scale=value;surface.Scale(new SizeF(ratio,ratio));
            Bounds=SettingsDialog.FitBounds(bounds,Screen.FromRectangle(bounds).WorkingArea,scale);Arrange();
            if(smoothFrame!=null)smoothFrame.UpdateScale(value);
            frameShape.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);
        }
        protected override void WndProc(ref Message message) {
            if(message.Msg==0x2e0){ApplyDpi(WindowDpi.MessageScale(message.WParam),WindowDpi.Suggested(message.LParam));message.Result=IntPtr.Zero;return;}
            base.WndProc(ref message);
        }
        internal static string VersionText() {
            var version=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version!=null&&version.Major>0?"v"+version.ToString(3):"dev";
        }
        private string BuildJson(string submittedContact) {
            var json="{\"app\":\"deepseek-reflex\",\"version\":"+PromptInserter.Json(VersionText())+",\"text\":"+PromptInserter.Json(body.Text.Trim());
            for(int i=0;i<chips.Length;i++)if(chips[i].Chosen)json+=",\"category\":"+PromptInserter.Json(Categories[i]);
            if(submittedContact.Length>0)json+=",\"contact\":"+PromptInserter.Json(submittedContact);
            return json+"}";
        }
        private async void Send() {
            if(sending||body.Text.Trim().Length==0||!send.Enabled)return;
            var submittedContact=contact.Text.Trim();
            var json=BuildJson(submittedContact);
            SetSending(true);showFailure=false;
            status.ForeColor=PanelTheme.Muted;status.Text="正在发送…";issueLink.Visible=false;
            Arrange();
            var self=this;
            try {
                string failure=null;
                await System.Threading.Tasks.Task.Run(delegate{failure=poster!=null?poster(json):HttpPost(json);});
                if(self.IsDisposed||!self.IsHandleCreated)return;
                if(failure==null){
                    try{Preferences.Write("feedback-contact",submittedContact);}catch(Exception){}
                    self.ShowThanks();return;
                }
                self.SetSending(false);self.showFailure=true;
                self.status.ForeColor=PanelTheme.Error;
                self.status.Text="发送失败："+failure.TrimEnd('。','.')+"。";
                self.issueLink.Visible=true;
                self.Arrange();
            }catch(Exception){if(!self.IsDisposed){self.SetSending(false);self.showFailure=true;self.status.ForeColor=PanelTheme.Error;self.status.Text="发送失败，请稍后重试。";self.issueLink.Visible=true;self.Arrange();}}
        }
        private void SetSending(bool value) {
            // ReadOnly recreates native edit handles and may raise TextChanged.
            // Keep the busy guard until both handles have finished changing.
            if(value)sending=true;
            body.ReadOnly=value;contact.ReadOnly=value;
            foreach(var chip in chips)chip.Enabled=!value;
            sending=value;
            send.Enabled=!value&&body.Text.Trim().Length>0;send.Text=value?"发送中…":"发送反馈";
        }
        private void ShowThanks() {
            thanking=true;
            heading.Visible=false;close.Visible=false;counter.Visible=false;privacy.Visible=false;
            categoryLabel.Visible=false;bodyLabel.Visible=false;contactLabel.Visible=false;keyboardHint.Visible=false;
            status.Visible=false;issueLink.Visible=false;send.Visible=false;
            foreach(var chip in chips)chip.Visible=false;
            bodyFrame.Visible=false;contactFrame.Visible=false;
            thanksMark.Visible=true;thanksText.Visible=true;thanksText.AccessibleName=thanksText.Text;
            Arrange();thanksTimer.Start();
        }
        internal static string HttpPost(string json) {
            try {
                // The 4.0-targeted build negotiates legacy TLS by default and
                // the endpoint only accepts TLS 1.2+, so opt in explicitly
                // (3072 = SecurityProtocolType.Tls12, absent from the 4.0
                // reference assembly).
                try{System.Net.ServicePointManager.SecurityProtocol|=(System.Net.SecurityProtocolType)3072;}catch(Exception){}
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
            if(disposing){thanksTimer.Dispose();if(smoothFrame!=null)smoothFrame.Dispose();}
            base.Dispose(disposing);
        }
    }
}
