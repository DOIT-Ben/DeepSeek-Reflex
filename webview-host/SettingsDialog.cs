using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeepSeekFloat
{
    internal sealed class HotkeyBox : TextBox
    {
        internal int Combination { get; private set; }
        internal HotkeyBox(int value)
        {
            ReadOnly=true;Combination=value;Text=HotkeyBindings.Format(value);BackColor=Color.White;BorderStyle=BorderStyle.None;TextAlign=HorizontalAlignment.Center;
            KeyDown+=delegate(object sender,KeyEventArgs e) {
                e.Handled=true;e.SuppressKeyPress=true;
                if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return;
                int next=(int)e.KeyData;
                if(!HotkeyBindings.ValidKey(next))return;
                Combination=next;Text=HotkeyBindings.Format(next);
            };
        }
    }
    internal sealed class SettingsDialog : Form
    {
        private readonly HotkeyBox toggle,capture;
        private readonly PanelSwitch focus=new PanelSwitch("唤出后直接在输入框打字");
        private readonly PanelChoices hideMode=new PanelChoices("最小化到任务栏","收进托盘");
        private readonly PanelChoices windowMode=new PanelChoices("当前尺寸","小窗模式","阅读模式");
        private readonly Label error=new Label { AutoSize=false,ForeColor=Color.FromArgb(185,55,55) };
        private readonly WindowSettings original;
        private readonly float scale;
        private readonly WindowFrame frame=new WindowFrame();
        private SmoothFrame smoothFrame;
        internal SettingsDialog(WindowSettings settings,Func<WindowSettings,string> apply,Action<string> quickAction=null)
        {
            original=settings.Clone();toggle=new HotkeyBox(settings.ToggleKeys);capture=new HotkeyBox(settings.CaptureKeys);
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek 小窗设置";Font=new Font("Microsoft YaHei UI",10f);BackColor=Color.White;ForeColor=PanelTheme.Ink;DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(S(440),S(634));Padding=Padding.Empty;
            var surface=new Panel {Name="settings-surface",Dock=DockStyle.Fill,BackColor=Color.White};Controls.Add(surface);
            var heading=LabelAt(surface,"小窗设置",24,22,340,34,16f,true);
            var subtitle=LabelAt(surface,"顺手唤起，随时问答",24,60,350,24,9f,false);subtitle.ForeColor=PanelTheme.Muted;
            var close=new ChromeButton("close","关闭设置");Place(surface,close,384,22,32,28);close.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            heading.MouseDown+=Drag;subtitle.MouseDown+=Drag;surface.MouseDown+=Drag;
            LabelAt(surface,"快捷键",24,96,350,24,10f,true);
            var card=new PanelCard();Place(surface,card,24,124,392,120);
            KeyRow(card,"唤出 / 收起",toggle,12);KeyRow(card,"选中文字取词",capture,64);
            focus.Checked=settings.FocusOnOpen;var focusCard=new PanelCard();Place(surface,focusCard,24,258,392,44);Place(focusCard,focus,16,7,360,30);
            LabelAt(surface,"快捷键收起方式",24,320,392,24,10f,true);
            hideMode.SelectedIndex=settings.HideToTrayOnToggle?1:0;Place(surface,hideMode,24,348,392,38);
            LabelAt(surface,"窗口尺寸",24,404,392,24,10f,true);
            windowMode.SelectedIndex=settings.Mode=="reading"?2:settings.Mode=="compact"?1:0;Place(surface,windowMode,24,432,392,38);
            var hint=LabelAt(surface,"点击按键框，再按下你想用的组合键。",24,476,392,24,9f,false);hint.ForeColor=PanelTheme.Muted;
            var refresh=new PanelButton("刷新页面");var import=new PanelButton("导入剪贴板");Place(surface,refresh,24,512,190,34);Place(surface,import,226,512,190,34);
            refresh.Enabled=import.Enabled=quickAction!=null;
            refresh.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("refresh");};
            import.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("import");};
            Place(surface,error,24,554,392,28);error.Font=new Font(Font.FontFamily,9f);
            var save=new PanelButton("保存") {Primary=true};var cancel=new PanelButton("取消") {DialogResult=DialogResult.Cancel};
            Place(surface,cancel,232,588,86,34);Place(surface,save,330,588,86,34);
            save.Click+=delegate {
                var next=original.Clone();next.ToggleKeys=toggle.Combination;next.CaptureKeys=capture.Combination;next.FocusOnOpen=focus.Checked;next.HideToTrayOnToggle=hideMode.SelectedIndex==1;
                next.Mode=windowMode.SelectedIndex==2?"reading":windowMode.SelectedIndex==1?"compact":"custom";
                string failure=apply(next);if(failure!=null) { error.Text=failure;return; }
                DialogResult=DialogResult.OK;Close();
            };
            CancelButton=cancel;
            KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();e.Handled=true;}};
            Shown+=delegate{var area=Screen.FromControl(this).WorkingArea;Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width));Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height));};
            Controls.Add(frame);smoothFrame=new SmoothFrame(this,scale,delegate{frame.UpdateShape(this,scale,smoothFrame.Ready);},false);
            frame.UpdateShape(this,scale);
            Resize+=delegate{if(smoothFrame!=null)smoothFrame.SyncOwner();frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);};
            ResizeBegin+=delegate{smoothFrame.BeginInteractiveResize();};ResizeEnd+=delegate{smoothFrame.EndInteractiveResize();};
        }
        private int S(int value) {return Math.Max(1,(int)Math.Round(value*scale));}
        private void Place(Control parent,Control child,int x,int y,int w,int h) {child.SetBounds(S(x),S(y),S(w),S(h));parent.Controls.Add(child);}
        private Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,bool bold) {
            var label=new Label {Text=text,Font=new Font(Font.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=PanelTheme.Ink,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft};Place(parent,label,x,y,w,h);return label;
        }
        private void KeyRow(PanelCard card,string label,HotkeyBox box,int y)
        {
            var text=LabelAt(card,label,16,y,142,40,10f,false);text.BackColor=PanelTheme.Surface;
            var frame=new PanelButton("") {TabStop=false,BackColor=PanelTheme.Surface};Place(card,frame,166,y,210,40);frame.Cursor=Cursors.IBeam;
            box.Font=new Font("Segoe UI",10f);Place(frame,box,8,10,194,24);frame.Click+=delegate{box.Focus();};
            box.Enter+=delegate{frame.InputFocused=true;frame.Invalidate();};box.Leave+=delegate{frame.InputFocused=false;frame.Invalidate();};
        }
        private void Drag(object sender,MouseEventArgs e) {if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);WindowFrame.ConfigureNativeBorder(Handle);frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x216&&m.LParam!=IntPtr.Zero&&smoothFrame!=null){var rect=(Native.RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam,typeof(Native.RECT));if(smoothFrame.TrySetBounds(Rectangle.FromLTRB(rect.left,rect.top,rect.right,rect.bottom))){m.Result=new IntPtr(1);return;}}
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing){if(disposing&&smoothFrame!=null)smoothFrame.Dispose();base.Dispose(disposing);}
    }
}
