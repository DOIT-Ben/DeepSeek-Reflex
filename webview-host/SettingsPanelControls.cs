using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal static class PanelTheme {
        internal static readonly Color Ink=Color.FromArgb(42,49,63),Muted=Color.FromArgb(103,112,130),Accent=Color.FromArgb(67,102,238),Tint=Color.FromArgb(237,242,255),Border=Color.FromArgb(225,231,240),Surface=Color.FromArgb(247,249,252);
        internal static readonly Color Hover=Color.FromArgb(241,244,248),Pressed=Color.FromArgb(225,233,252),AccentHover=Color.FromArgb(51,82,214),AccentPressed=Color.FromArgb(42,70,190),Error=Color.FromArgb(177,48,48);
        internal const int HoverDuration=120,PressDuration=60,ReleaseDuration=90,SwitchDuration=160;
        internal static GraphicsPath Rounded(RectangleF rect,float radius) {
            float d=radius*2;var path=new GraphicsPath();
            path.AddArc(rect.X,rect.Y,d,d,180,90);path.AddArc(rect.Right-d,rect.Y,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
        internal static Color Mix(Color from,Color to,float value){return Color.FromArgb((int)Math.Round(from.R+(to.R-from.R)*value),(int)Math.Round(from.G+(to.G-from.G)*value),(int)Math.Round(from.B+(to.B-from.B)*value));}
        internal static void Feedback(Control control,UiMotion hover,UiMotion press) {
            control.MouseEnter+=delegate{hover.To(1);};
            control.MouseLeave+=delegate{hover.To(0);press.To(0,ReleaseDuration);};
            control.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left&&control.Enabled)press.To(1,PressDuration);};
            control.MouseUp+=delegate{press.To(0,ReleaseDuration);};
            control.MouseCaptureChanged+=delegate{if(!control.Capture)press.To(0,ReleaseDuration);};
            control.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Space&&control.Enabled)press.To(1,PressDuration);};
            control.KeyUp+=delegate{press.To(0,ReleaseDuration);};
            control.EnabledChanged+=delegate{hover.To(0);press.To(0);};
        }
        internal static void FocusRing(Graphics graphics,Size size,float scale,float radius,Color? color=null) {
            using(var shape=Rounded(new RectangleF(3*scale,3*scale,size.Width-6*scale,size.Height-6*scale),radius*scale))
            using(var pen=new Pen(color??Accent,1.5f*scale))graphics.DrawPath(pen,shape);
        }
    }
    internal sealed class UiMotion : IDisposable {
        private readonly Timer timer=new Timer {Interval=15};
        private readonly System.Diagnostics.Stopwatch watch=new System.Diagnostics.Stopwatch();
        private readonly Control owner;private float from,target;private int duration;private bool disposed;
        internal float Value {get;private set;}
        internal bool Running {get{return !disposed&&timer.Enabled;}}
        internal UiMotion(Control control){owner=control;timer.Tick+=delegate{Advance();};}
        internal void To(float value,int milliseconds=PanelTheme.HoverDuration) {
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
        internal bool Ghost,Segment;
        private bool chosen;
        internal bool InputFocused;
        private readonly UiMotion hoverMotion,pressMotion,choiceMotion;
        internal bool Chosen {get{return chosen;}set{chosen=value;choiceMotion.To(value?1:0);}}
        internal PanelButton(string text) {
            Text=text;AccessibleName=text;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;BackColor=Color.White;ForeColor=PanelTheme.Ink;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            hoverMotion=new UiMotion(this);pressMotion=new UiMotion(this);choiceMotion=new UiMotion(this);
            PanelTheme.Feedback(this,hoverMotion,pressMotion);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);float scale=g.DpiX/96f;
            var neutral=Segment?PanelTheme.Surface:Color.White;
            var color=Primary?PanelTheme.Mix(PanelTheme.Accent,PanelTheme.AccentHover,hoverMotion.Value):PanelTheme.Mix(PanelTheme.Mix(neutral,PanelTheme.Hover,hoverMotion.Value),Segment?Color.White:PanelTheme.Tint,choiceMotion.Value);
            color=PanelTheme.Mix(color,Primary?PanelTheme.AccentPressed:PanelTheme.Pressed,pressMotion.Value*0.7f);
            if(!Enabled)color=PanelTheme.Surface;
            float inset=scale/2,offset=pressMotion.Value*scale;
            using(var shape=PanelTheme.Rounded(new RectangleF(inset,inset,Width-scale,Height-scale),10*scale)) {
                using(var brush=new SolidBrush(color))g.FillPath(brush,shape);
                if((!Ghost&&!Segment)||InputFocused||(Segment&&chosen))using(var pen=new Pen(Primary||InputFocused?PanelTheme.Accent:Segment?PanelTheme.Border:PanelTheme.Mix(PanelTheme.Border,PanelTheme.Accent,choiceMotion.Value),scale))g.DrawPath(pen,shape);
            }
            TextRenderer.DrawText(g,Text,Font,new Rectangle(4,(int)Math.Round(offset),Width-8,Height),!Enabled?PanelTheme.Muted:Primary?Color.White:PanelTheme.Mix(ForeColor,PanelTheme.Accent,choiceMotion.Value),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)PanelTheme.FocusRing(g,Size,scale,7,Primary?Color.White:PanelTheme.Accent);
        }
        protected override void Dispose(bool disposing){if(disposing){hoverMotion.Dispose();pressMotion.Dispose();choiceMotion.Dispose();}base.Dispose(disposing);}
    }
    internal sealed class PanelSwitch : Button {
        private bool value;
        private readonly UiMotion slide,hoverMotion,pressMotion;
        internal float SlidePosition {get{return slide.Value;}}
        internal bool Checked { get{return value;}set{bool changed=this.value!=value;this.value=value;AccessibleDescription=value?"已开启":"已关闭";slide.To(value?1:0,PanelTheme.SwitchDuration);if(changed&&IsHandleCreated)AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);} }
        protected override AccessibleObject CreateAccessibilityInstance(){return new SwitchAccessibility(this);}
        private sealed class SwitchAccessibility : ControlAccessibleObject {
            private readonly PanelSwitch control;
            internal SwitchAccessibility(PanelSwitch owner):base(owner){control=owner;}
            public override AccessibleStates State {get{return base.State|(control.Checked?AccessibleStates.Checked:AccessibleStates.None);}}
            public override string DefaultAction {get{return control.Checked?"关闭":"开启";}}
            public override void DoDefaultAction(){if(control.Enabled)control.PerformClick();}
        }
        internal PanelSwitch(string text) {
            Text=text;AccessibleName=text;AccessibleRole=AccessibleRole.CheckButton;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=PanelTheme.Surface;Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            slide=new UiMotion(this);hoverMotion=new UiMotion(this);pressMotion=new UiMotion(this);PanelTheme.Feedback(this,hoverMotion,pressMotion);
        }
        protected override void OnClick(EventArgs e) { Checked=!Checked;base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);float s=g.DpiX/96f,w=36*s,h=20*s;
            using(var shape=PanelTheme.Rounded(new RectangleF(0,0,Width-1,Height-1),8*s))using(var brush=new SolidBrush(PanelTheme.Mix(PanelTheme.Mix(BackColor,PanelTheme.Hover,hoverMotion.Value),PanelTheme.Pressed,pressMotion.Value*0.45f)))g.FillPath(brush,shape);
            var rect=new RectangleF(Width-w-2*s,(Height-h)/2,w,h);
            using(var path=PanelTheme.Rounded(rect,10*s))using(var brush=new SolidBrush(PanelTheme.Mix(Color.FromArgb(194,202,216),PanelTheme.Accent,slide.Value)))g.FillPath(brush,path);
            using(var brush=new SolidBrush(Color.White))g.FillEllipse(brush,rect.X+2*s+16*s*slide.Value,rect.Y+2*s,16*s,16*s);
            TextRenderer.DrawText(g,Text,Font,new Rectangle(0,0,(int)(Width-w-12*s),Height),PanelTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)PanelTheme.FocusRing(g,Size,s,7);
        }
        protected override void Dispose(bool disposing){if(disposing){slide.Dispose();hoverMotion.Dispose();pressMotion.Dispose();}base.Dispose(disposing);}
    }
    internal sealed class PanelChoices : Panel {
        private readonly PanelButton[] choices;private int selected;
        internal event EventHandler SelectedIndexChanged;
        internal int SelectedIndex {
            get{return selected;}
            set{if(value<0||value>=choices.Length)throw new ArgumentOutOfRangeException("value");bool changed=selected!=value;selected=value;for(int i=0;i<choices.Length;i++){choices[i].Chosen=i==value;choices[i].TabStop=i==value;choices[i].AccessibleDescription=i==value?"已选择":"未选择";choices[i].Invalidate();}if(changed&&SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}
        }
        internal PanelChoices(params string[] names) {
            BackColor=Color.White;DoubleBuffered=true;TabStop=false;choices=new PanelButton[names.Length];
            for(int i=0;i<names.Length;i++) {
                int index=i;var button=new PanelButton(names[i]) {Segment=true,BackColor=PanelTheme.Surface};button.AccessibleRole=AccessibleRole.RadioButton;
                button.Click+=delegate {SelectedIndex=index;};
                button.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right||e.KeyCode==Keys.Home||e.KeyCode==Keys.End){SelectedIndex=e.KeyCode==Keys.Home?0:e.KeyCode==Keys.End?choices.Length-1:(selected+(e.KeyCode==Keys.Right?1:choices.Length-1))%choices.Length;choices[selected].Focus();e.Handled=true;e.SuppressKeyPress=true;}};
                choices[i]=button;Controls.Add(button);
            }
            SelectedIndex=0;Resize+=delegate{Arrange();};
        }
        private void Arrange() {
            float scale;using(var graphics=CreateGraphics())scale=graphics.DpiX/96f;
            int inset=(int)Math.Round(3*scale),available=Math.Max(0,Width-inset*2);
            for(int i=0;i<choices.Length;i++){int left=inset+available*i/choices.Length,right=inset+available*(i+1)/choices.Length;choices[i].SetBounds(left,inset,Math.Max(0,right-left),Math.Max(0,Height-inset*2));}
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var shape=PanelTheme.Rounded(new RectangleF(0,0,Width-1,Height-1),10*e.Graphics.DpiX/96f))using(var brush=new SolidBrush(PanelTheme.Surface))e.Graphics.FillPath(brush,shape);}
    }
    internal sealed class PanelCard : Panel {
        internal PanelCard() { BackColor=Color.White;DoubleBuffered=true; }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=PanelTheme.Rounded(new RectangleF(0,0,Width-1,Height-1),10*e.Graphics.DpiX/96f))using(var brush=new SolidBrush(PanelTheme.Surface))e.Graphics.FillPath(brush,path);
        }
    }
}
