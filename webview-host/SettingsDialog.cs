using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class SettingsDialog : Form {
        private readonly HotkeyBox toggle,capture;
        private readonly PanelSwitch focus=new PanelSwitch("唤出后直接在输入框打字");
        private readonly PanelChoices hideMode=new PanelChoices("最小化到任务栏","收进托盘");
        private readonly PanelChoices windowMode=new PanelChoices("当前尺寸","小窗模式","阅读模式");
        private readonly Label error=new Label {AutoSize=false,ForeColor=PanelTheme.Error};
        private readonly Label hint=new Label {AutoSize=false,ForeColor=PanelTheme.Muted};
        private readonly Panel surface=new Panel {Name="settings-surface",Dock=DockStyle.Fill,BackColor=Color.White};
        private readonly Panel body=new Panel {Name="settings-body",AutoScroll=true,BackColor=Color.White,TabStop=false};
        private readonly Panel content=new Panel {BackColor=Color.White,TabStop=false};
        private readonly Panel footer=new Panel {Name="settings-footer",BackColor=Color.White};
        private readonly PanelButton save=new PanelButton("保存") {Primary=true};
        private readonly PanelButton cancel=new PanelButton("取消") {DialogResult=DialogResult.Cancel};
        private readonly ChromeButton close=new ChromeButton("close","关闭设置");
        private readonly Label heading,subtitle;
        private readonly WindowSettings original;
        private readonly float scale;
        private readonly WindowFrame frame=new WindowFrame();
        private SmoothFrame smoothFrame;
        private bool arranging;
        internal SettingsDialog(WindowSettings settings,Func<WindowSettings,string> apply,Action<string> quickAction=null) {
            original=settings.Clone();toggle=new HotkeyBox(settings.ToggleKeys);capture=new HotkeyBox(settings.CaptureKeys);
            toggle.AccessibleName="唤出 / 收起快捷键";capture.AccessibleName="选中文字取词快捷键";
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek-Reflex 设置";Font=new Font("Microsoft YaHei UI",10f);BackColor=Color.White;ForeColor=PanelTheme.Ink;DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(S(440),S(644));Padding=Padding.Empty;
            Controls.Add(surface);
            heading=LabelAt(surface,"小窗设置",24,22,340,34,16f,true);
            subtitle=LabelAt(surface,"顺手唤起，随时问答",24,60,350,24,9f,false);subtitle.ForeColor=PanelTheme.Muted;
            surface.Controls.Add(close);close.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            heading.MouseDown+=Drag;subtitle.MouseDown+=Drag;surface.MouseDown+=Drag;
            surface.Controls.Add(body);body.Controls.Add(content);surface.Controls.Add(footer);
            content.Size=new Size(S(392),S(452));
            LabelAt(content,"快捷键",0,0,392,20,10f,true);
            var card=new PanelCard();Place(content,card,0,28,392,104);card.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            KeyRow(card,"唤出 / 收起",toggle,8);KeyRow(card,"选中文字取词",capture,56);
            hint.Font=new Font(Font.FontFamily,9f);hint.Text="点击按键框开始录入，Esc 取消，保存后生效。";Place(content,hint,0,140,392,36);hint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            toggle.FeedbackChanged+=RecordingFeedback;capture.FeedbackChanged+=RecordingFeedback;
            focus.Checked=settings.FocusOnOpen;var focusCard=new PanelCard();Place(content,focusCard,0,184,392,44);focusCard.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Place(focusCard,focus,12,7,368,30);focus.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            LabelAt(content,"快捷键收起方式",0,244,392,20,10f,true);
            hideMode.SelectedIndex=settings.HideToTrayOnToggle?1:0;Place(content,hideMode,0,272,392,36);hideMode.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            LabelAt(content,"窗口尺寸",0,328,392,20,10f,true);
            windowMode.SelectedIndex=settings.Mode=="reading"?2:settings.Mode=="compact"?1:0;Place(content,windowMode,0,356,392,36);windowMode.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var actions=new Panel {Margin=Padding.Empty,Padding=Padding.Empty,BackColor=Color.White};
            Place(content,actions,0,416,392,32);actions.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var refresh=new PanelButton("刷新页面") {Ghost=true};var import=new PanelButton("导入剪贴板") {Ghost=true};var help=new PanelButton("使用帮助") {Ghost=true};
            var actionButtons=new[]{refresh,import,help};
            foreach(var button in actionButtons){button.Margin=Padding.Empty;button.Enabled=quickAction!=null;actions.Controls.Add(button);}
            Action arrangeActions=delegate{for(int i=0;i<actionButtons.Length;i++){int left=actions.Width*i/actionButtons.Length,right=actions.Width*(i+1)/actionButtons.Length;actionButtons[i].SetBounds(left,0,right-left,actions.Height);}};
            actions.Resize+=delegate{arrangeActions();};arrangeActions();
            refresh.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("refresh");};
            import.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("import");};
            help.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("help");};
            error.Font=new Font(Font.FontFamily,9f);footer.Controls.Add(error);footer.Controls.Add(cancel);footer.Controls.Add(save);
            save.Click+=delegate {
                if(toggle.Recording||capture.Recording){hint.Text="请先完成或取消快捷键录入。";return;}
                var next=original.Clone();next.ToggleKeys=toggle.Combination;next.CaptureKeys=capture.Combination;next.FocusOnOpen=focus.Checked;next.HideToTrayOnToggle=hideMode.SelectedIndex==1;
                next.Mode=windowMode.SelectedIndex==2?"reading":windowMode.SelectedIndex==1?"compact":"custom";
                string failure=apply(next);if(failure!=null){error.Text=failure;return;}
                DialogResult=DialogResult.OK;Close();
            };
            CancelButton=cancel;
            cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            Shown+=delegate {
                var area=Screen.FromControl(this).WorkingArea;
                Bounds=FitBounds(Bounds,area,scale);Arrange();
            };
            body.Layout+=delegate{ArrangeContent();};surface.Resize+=delegate{Arrange();};
            Controls.Add(frame);smoothFrame=new SmoothFrame(this,scale,delegate{frame.UpdateShape(this,scale,smoothFrame.Ready);},false);
            Arrange();frame.UpdateShape(this,scale);
            Resize+=delegate{if(smoothFrame!=null)smoothFrame.SyncOwner();frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);};
            ResizeBegin+=delegate{smoothFrame.BeginInteractiveResize();};ResizeEnd+=delegate{smoothFrame.EndInteractiveResize();};
        }
        internal static Rectangle FitBounds(Rectangle bounds,Rectangle area,float dpiScale) {
            int margin=Math.Min((int)Math.Round(12*dpiScale),Math.Min(area.Width,area.Height)/4);
            int width=Math.Min(bounds.Width,area.Width-margin*2),height=Math.Min(bounds.Height,area.Height-margin*2);
            return new Rectangle(Math.Max(area.Left+margin,Math.Min(bounds.Left,area.Right-margin-width)),Math.Max(area.Top+margin,Math.Min(bounds.Top,area.Bottom-margin-height)),width,height);
        }
        private void RecordingFeedback(object sender,EventArgs e) {
            var box=(HotkeyBox)sender;string validation=HotkeyBindings.Validate(toggle.Combination,capture.Combination);
            hint.Text=validation??box.Feedback;hint.ForeColor=validation!=null||box.Invalid?PanelTheme.Error:PanelTheme.Muted;
            error.Text="";save.Enabled=validation==null;
        }
        private void Arrange() {
            if(arranging)return;arranging=true;
            try {
                int width=Math.Max(1,surface.ClientSize.Width-S(48));
                heading.Width=Math.Max(1,surface.Width-S(88));subtitle.Width=width;
                close.SetBounds(surface.Width-S(56),S(22),S(32),S(28));
                body.SetBounds(S(24),S(100),width,Math.Max(1,surface.Height-S(192)));
                footer.SetBounds(S(24),surface.Height-S(88),width,S(78));
                error.SetBounds(0,0,width,S(36));cancel.SetBounds(width-S(184),S(44),S(86),S(34));save.SetBounds(width-S(86),S(44),S(86),S(34));
            }finally{arranging=false;}
            ArrangeContent();
        }
        private void ArrangeContent() {
            if(arranging)return;arranging=true;
            try {
                body.AutoScrollMinSize=new Size(0,S(452));
                content.SetBounds(0,body.AutoScrollPosition.Y,Math.Max(1,body.ClientSize.Width),S(452));
                // Native anchor rounding can leave the last column one pixel outside
                // a scrollbar-reduced viewport. Keep each full-width row inside it.
                foreach(Control row in content.Controls)row.Width=Math.Max(1,content.ClientSize.Width-row.Left);
            }finally{arranging=false;}
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        private void Place(Control parent,Control child,int x,int y,int w,int h){child.SetBounds(S(x),S(y),S(w),S(h));parent.Controls.Add(child);}
        private Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,bool bold) {
            var label=new Label {Text=text,Font=new Font(Font.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=PanelTheme.Ink,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft};Place(parent,label,x,y,w,h);if(parent==content)label.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;return label;
        }
        private void KeyRow(PanelCard card,string label,HotkeyBox box,int y) {
            var text=LabelAt(card,label,16,y,142,40,10f,false);text.BackColor=PanelTheme.Surface;
            var keyFrame=new PanelButton("") {TabStop=false,BackColor=PanelTheme.Surface};card.Controls.Add(keyFrame);keyFrame.Cursor=Cursors.Hand;
            box.Font=new Font("Segoe UI",10f);keyFrame.Controls.Add(box);keyFrame.Click+=delegate{box.Focus();box.BeginCapture();};
            Action arrange=delegate {
                int labelWidth=Math.Min(S(142),(int)(card.Width*0.39));text.Width=labelWidth;
                keyFrame.SetBounds(labelWidth+S(24),S(y),Math.Max(1,card.Width-labelWidth-S(40)),S(40));
                box.SetBounds(S(8),Math.Max(0,(keyFrame.Height-box.PreferredHeight)/2),Math.Max(1,keyFrame.Width-S(16)),box.PreferredHeight);
            };
            card.Resize+=delegate{arrange();};arrange();
            box.Enter+=delegate{keyFrame.InputFocused=true;keyFrame.Invalidate();};box.Leave+=delegate{keyFrame.InputFocused=false;keyFrame.Invalidate();};
        }
        private void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);WindowFrame.ConfigureNativeBorder(Handle);frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);}
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x216&&m.LParam!=IntPtr.Zero&&smoothFrame!=null){var rect=(Native.RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam,typeof(Native.RECT));if(smoothFrame.TrySetBounds(Rectangle.FromLTRB(rect.left,rect.top,rect.right,rect.bottom))){m.Result=new IntPtr(1);return;}}
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing){if(disposing&&smoothFrame!=null)smoothFrame.Dispose();base.Dispose(disposing);}
    }
}
