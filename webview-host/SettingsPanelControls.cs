using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal static class PanelTheme {
        internal static readonly Color Ink=Color.FromArgb(42,49,63),Muted=Color.FromArgb(116,125,141),Accent=Color.FromArgb(67,102,238),Tint=Color.FromArgb(237,242,255),Border=Color.FromArgb(225,231,240),Surface=Color.FromArgb(247,249,252);
        internal static GraphicsPath Rounded(RectangleF rect,float radius) {
            float d=radius*2;var path=new GraphicsPath();
            path.AddArc(rect.X,rect.Y,d,d,180,90);path.AddArc(rect.Right-d,rect.Y,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
        internal static Color Mix(Color from,Color to,float value){return Color.FromArgb((int)Math.Round(from.R+(to.R-from.R)*value),(int)Math.Round(from.G+(to.G-from.G)*value),(int)Math.Round(from.B+(to.B-from.B)*value));}
    }
    internal sealed class UiMotion : IDisposable {
        private readonly Timer timer=new Timer {Interval=15};
        private readonly System.Diagnostics.Stopwatch watch=new System.Diagnostics.Stopwatch();
        private readonly Control owner;private float from,target;private int duration;private bool disposed;
        internal float Value {get;private set;}
        internal bool Running {get{return !disposed&&timer.Enabled;}}
        internal UiMotion(Control control){owner=control;timer.Tick+=delegate{Advance();};}
        internal void To(float value,int milliseconds=120) {
            if(disposed)return;
            if(timer.Enabled)Advance();
            bool allowed=true;Native.SystemParametersInfo(0x1042,0,ref allowed,0);
            if(!owner.IsHandleCreated||!owner.Visible||!SystemInformation.UIEffectsEnabled||!allowed){timer.Stop();Value=value;target=value;owner.Invalidate();return;}
            if(target==value&&Value==value)return;
            from=Value;target=value;duration=milliseconds;watch.Restart();timer.Start();
        }
        private void Advance(){if(disposed)return;float progress=Math.Min(1,watch.ElapsedMilliseconds/(float)duration);float eased=1-(1-progress)*(1-progress)*(1-progress);Value=from+(target-from)*eased;if(progress>=1){Value=target;timer.Stop();watch.Stop();}if(!owner.IsDisposed)owner.Invalidate();}
        public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Dispose();}
    }
    internal sealed class PanelButton : Button {
        internal bool Primary;
        private bool chosen;
        internal bool InputFocused;
        private readonly UiMotion hoverMotion,pressMotion,choiceMotion;
        internal bool Chosen {get{return chosen;}set{chosen=value;choiceMotion.To(value?1:0);}}
        internal PanelButton(string text) {
            Text=text;AccessibleName=text;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;BackColor=Color.White;ForeColor=PanelTheme.Ink;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            hoverMotion=new UiMotion(this);pressMotion=new UiMotion(this);choiceMotion=new UiMotion(this);
            MouseEnter+=delegate{hoverMotion.To(1);};MouseLeave+=delegate{hoverMotion.To(0);pressMotion.To(0,70);};
            MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)pressMotion.To(1,60);};MouseUp+=delegate{pressMotion.To(0,90);};
            MouseCaptureChanged+=delegate{if(!Capture)pressMotion.To(0,90);};
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Space)pressMotion.To(1,60);};KeyUp+=delegate{pressMotion.To(0,90);};
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);float scale=g.DpiX/96f;
            var color=Primary?PanelTheme.Mix(PanelTheme.Accent,Color.FromArgb(51,82,214),hoverMotion.Value):PanelTheme.Mix(PanelTheme.Mix(Color.White,PanelTheme.Surface,hoverMotion.Value),PanelTheme.Tint,choiceMotion.Value);
            color=PanelTheme.Mix(color,Primary?Color.FromArgb(42,70,190):Color.FromArgb(225,233,252),pressMotion.Value*0.7f);
            float inset=scale/2,offset=pressMotion.Value*scale;
            using(var shape=PanelTheme.Rounded(new RectangleF(inset,inset,Width-scale,Height-scale),10*scale)) {
                using(var brush=new SolidBrush(color))g.FillPath(brush,shape);
                using(var pen=new Pen(Primary||InputFocused?PanelTheme.Accent:PanelTheme.Mix(PanelTheme.Border,PanelTheme.Accent,choiceMotion.Value),scale))g.DrawPath(pen,shape);
            }
            TextRenderer.DrawText(g,Text,Font,new Rectangle(4,(int)Math.Round(offset),Width-8,Height),!Enabled?PanelTheme.Muted:Primary?Color.White:PanelTheme.Mix(ForeColor,PanelTheme.Accent,choiceMotion.Value),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)using(var shape=PanelTheme.Rounded(new RectangleF(3*scale,3*scale,Width-6*scale,Height-6*scale),7*scale))using(var pen=new Pen(PanelTheme.Accent,scale))g.DrawPath(pen,shape);
        }
        protected override void Dispose(bool disposing){if(disposing){hoverMotion.Dispose();pressMotion.Dispose();choiceMotion.Dispose();}base.Dispose(disposing);}
    }
    internal sealed class PanelSwitch : Button {
        private bool value;
        private readonly UiMotion slide;
        internal float SlidePosition {get{return slide.Value;}}
        internal bool Checked { get{return value;}set{this.value=value;AccessibleDescription=value?"已开启":"已关闭";slide.To(value?1:0,160);} }
        internal PanelSwitch(string text) {
            Text=text;AccessibleName=text;AccessibleRole=AccessibleRole.CheckButton;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=PanelTheme.Surface;Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            slide=new UiMotion(this);
        }
        protected override void OnClick(EventArgs e) { Checked=!Checked;base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);float s=g.DpiX/96f,w=36*s,h=20*s;
            var rect=new RectangleF(Width-w-2*s,(Height-h)/2,w,h);
            using(var path=PanelTheme.Rounded(rect,10*s))using(var brush=new SolidBrush(PanelTheme.Mix(Color.FromArgb(194,202,216),PanelTheme.Accent,slide.Value)))g.FillPath(brush,path);
            using(var brush=new SolidBrush(Color.White))g.FillEllipse(brush,rect.X+2*s+16*s*slide.Value,rect.Y+2*s,16*s,16*s);
            TextRenderer.DrawText(g,Text,Font,new Rectangle(0,0,(int)(Width-w-12*s),Height),PanelTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(2,2,Width-4,Height-4));
        }
        protected override void Dispose(bool disposing){if(disposing)slide.Dispose();base.Dispose(disposing);}
    }
    internal sealed class PanelChoices : Panel {
        private readonly PanelButton[] choices;private int selected;
        internal int SelectedIndex {
            get{return selected;}
            set{if(value<0||value>=choices.Length)throw new ArgumentOutOfRangeException("value");selected=value;for(int i=0;i<choices.Length;i++){choices[i].Chosen=i==value;choices[i].TabStop=i==value;choices[i].AccessibleDescription=i==value?"已选择":"未选择";choices[i].Invalidate();}}
        }
        internal PanelChoices(params string[] names) {
            BackColor=Color.White;TabStop=false;choices=new PanelButton[names.Length];
            for(int i=0;i<names.Length;i++) {
                int index=i;var button=new PanelButton(names[i]);button.AccessibleRole=AccessibleRole.RadioButton;
                button.Click+=delegate {SelectedIndex=index;};
                button.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){SelectedIndex=(selected+(e.KeyCode==Keys.Right?1:choices.Length-1))%choices.Length;choices[selected].Focus();e.Handled=true;}};
                choices[i]=button;Controls.Add(button);
            }
            SelectedIndex=0;Resize+=delegate{Arrange();};
        }
        private void Arrange() { int gap=Math.Max(4,Height/6),width=(Width-gap*(choices.Length-1))/choices.Length;for(int i=0;i<choices.Length;i++)choices[i].SetBounds(i*(width+gap),0,width,Height); }
    }
    internal sealed class PanelCard : Panel {
        internal PanelCard() { BackColor=Color.White;DoubleBuffered=true; }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=PanelTheme.Rounded(new RectangleF(0,0,Width-1,Height-1),10*e.Graphics.DpiX/96f))using(var brush=new SolidBrush(PanelTheme.Surface))e.Graphics.FillPath(brush,path);
        }
    }
}
