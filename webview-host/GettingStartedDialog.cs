using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal static class GettingStarted {
        internal const string Marker="getting-started-v1.json";
        internal static bool NeedsIntroduction {get{try{return !Preferences.ReadBool(Marker);}catch(UnauthorizedAccessException){return true;}}}
        internal static bool Acknowledge() {
            try {Preferences.Write(Marker,"true");return true;}
            catch(IOException){return false;}
            catch(UnauthorizedAccessException){return false;}
        }
    }
    internal sealed class GettingStartedDialog : Form {
        private readonly Label heading,body,key,hint,progress;
        private readonly PanelButton next,previous;
        private readonly string[] headings,bodies,keys,hints;
        private readonly float scale;
        private readonly WindowFrame frame=new WindowFrame();
        private SmoothFrame smoothFrame;
        internal int Step {get;private set;}
        internal GettingStartedDialog(WindowSettings settings) {
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek-Reflex 使用帮助";Font=new Font("Microsoft YaHei UI",10f);BackColor=Color.White;ForeColor=PanelTheme.Ink;
            DoubleBuffered=true;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(S(400),S(452));
            var surface=new Panel {Name="guide-surface",Dock=DockStyle.Fill,BackColor=Color.White};Controls.Add(surface);
            var brand=LabelAt(surface,"DeepSeek-Reflex",24,24,312,30,12f,true);
            var close=new ChromeButton("close","跳过引导，开始使用");Place(surface,close,344,22,32,28);
            close.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            progress=LabelAt(surface,"",24,68,352,22,9f,false);progress.ForeColor=PanelTheme.Muted;
            heading=LabelAt(surface,"",24,105,352,58,18f,true);
            body=LabelAt(surface,"",24,174,352,58,10f,false);
            var card=new PanelCard();Place(surface,card,24,248,352,96);
            key=LabelAt(card,"",16,12,320,30,14f,true);key.ForeColor=PanelTheme.Accent;key.BackColor=PanelTheme.Surface;
            hint=LabelAt(card,"",16,48,320,36,9f,false);hint.ForeColor=PanelTheme.Muted;hint.BackColor=PanelTheme.Surface;
            var help=LabelAt(surface,"以后可以在设置 → 使用帮助中重新查看。",24,356,352,22,9f,false);help.ForeColor=PanelTheme.Muted;
            var skip=new PanelButton("跳过");Place(surface,skip,24,396,76,34);skip.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            previous=new PanelButton("上一步");Place(surface,previous,180,396,88,34);previous.Click+=delegate{SetStep(Step-1);};
            next=new PanelButton("下一步") {Primary=true};Place(surface,next,280,396,96,34);
            next.Click+=delegate{if(Step==3){DialogResult=DialogResult.OK;Close();}else SetStep(Step+1);};
            headings=new[]{"先登录，就能开始聊","按一下，问题就在身边","选中文字，带入小窗","问完收起，继续工作"};
            bodies=new[]{
                "在小窗里按 DeepSeek 官网流程登录。\n已有账号、历史对话和附件都由官网提供。",
                "阅读、备课或写代码时，按快捷键唤起小窗。\n再按一次，就按你的设置收起。",
                "先在文档或网页中选中文字，再按取词键。\n选择翻译或解释，检查草稿后自己发送。",
                "左侧图钉可置顶，旁边切换小窗 / 阅读尺寸。\n右上角关闭只收进托盘，托盘菜单可彻底退出。"};
            keys=new[]{"官网登录 · 无需 API Key",HotkeyBindings.Format(settings.ToggleKeys),HotkeyBindings.Format(settings.CaptureKeys),"置顶 · 拉伸 · 收起"};
            hints=new[]{
                "DeepSeek-Reflex 是独立社区工具，需要联网。",
                settings.HideToTrayOnToggle?"当前收起方式：收进托盘。快捷键可在设置修改。":"当前收起方式：最小化到任务栏。可在设置修改。",
                "取词失败时，可复制文字，再从设置导入剪贴板。",
                "拖动标题栏移动窗口；四边和四角都可以拉伸。"};
            SetStep(0);AcceptButton=next;CancelButton=skip;
            brand.MouseDown+=Drag;progress.MouseDown+=Drag;surface.MouseDown+=Drag;
            Controls.Add(frame);smoothFrame=new SmoothFrame(this,scale,delegate{frame.UpdateShape(this,scale,smoothFrame.Ready);},false);
            frame.UpdateShape(this,scale);Resize+=delegate{smoothFrame.SyncOwner();frame.UpdateShape(this,scale,smoothFrame.Ready);};
            Shown+=delegate{var area=Screen.FromControl(this).WorkingArea;Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width));Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height));next.Focus();};
        }
        internal void SetStep(int value) {
            if(value<0||value>3)return;Step=value;heading.Text=headings[value];body.Text=bodies[value];key.Text=keys[value];hint.Text=hints[value];
            progress.Text="快速上手  "+(value+1)+" / 4";previous.Visible=value>0;next.Text=value==3?"开始使用":"下一步";next.AccessibleName=next.Text;
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        private void Place(Control parent,Control child,int x,int y,int w,int h){child.SetBounds(S(x),S(y),S(w),S(h));parent.Controls.Add(child);}
        private Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,bool bold){var label=new Label{Text=text,Font=new Font(Font.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=PanelTheme.Ink,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft};Place(parent,label,x,y,w,h);return label;}
        private void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);WindowFrame.ConfigureNativeBorder(Handle);frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);}
        protected override void Dispose(bool disposing){if(disposing&&smoothFrame!=null)smoothFrame.Dispose();base.Dispose(disposing);}
    }
}
