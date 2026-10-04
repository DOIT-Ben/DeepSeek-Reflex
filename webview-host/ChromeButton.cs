using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeepSeekFloat
{
    internal sealed class ChromeButton : Button
    {
        internal readonly string Kind;
        internal bool Active;
        private readonly UiMotion hoverMotion,pressMotion;
        internal ChromeButton(string kind, string description)
        {
            Kind = kind;
            AccessibleName = description;
            Text = description;
            AccessibleRole = AccessibleRole.PushButton;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            hoverMotion=new UiMotion(this);pressMotion=new UiMotion(this);
            MouseEnter += delegate { hoverMotion.To(1); };
            MouseLeave += delegate { hoverMotion.To(0);pressMotion.To(0,80); };
            MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)pressMotion.To(1,60);};MouseUp+=delegate{pressMotion.To(0,90);};
            MouseCaptureChanged+=delegate{if(!Capture)pressMotion.To(0,90);};
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Space)pressMotion.To(1,60);};KeyUp+=delegate{pressMotion.To(0,90);};
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var background = PanelTheme.Mix(Active?PanelTheme.Tint:Color.White,Kind=="close"?Color.FromArgb(255,235,235):Color.FromArgb(241,244,248),hoverMotion.Value);
            background=PanelTheme.Mix(background,Kind=="close"?Color.FromArgb(255,216,216):Color.FromArgb(224,231,245),pressMotion.Value);
            g.Clear(Color.White);
            if(background!=Color.White)using(var shape=PanelTheme.Rounded(new RectangleF(1,1,Width-2,Height-2),8*Width/32f))using(var brush=new SolidBrush(background))g.FillPath(brush,shape);
            float scale = Width / 32f;
            g.TranslateTransform(Width / 2f, Height / 2f+pressMotion.Value*scale);
            g.ScaleTransform(scale, scale);
            using (var pen = new Pen(Active ? Color.FromArgb(57,96,222) : Color.FromArgb(71,79,95), 1.35f))
            {
                if (Kind == "pin")
                {
                    g.RotateTransform(Active ? 0 : 35);
                    var points = new[] { new PointF(-3,-7),new PointF(3,-7),new PointF(2,-1),new PointF(5,2),new PointF(-5,2),new PointF(-2,-1),new PointF(-3,-7) };
                    if (Active) using (var brush = new SolidBrush(pen.Color)) g.FillPolygon(brush, points);
                    g.DrawLines(pen, points);
                    g.DrawLine(pen, 0, 2, 0, 8);
                }
                else if (Kind == "minimize") g.DrawLine(pen, -5, 2, 5, 2);
                else if (Kind == "close") { g.DrawLine(pen,-4,-4,4,4); g.DrawLine(pen,4,-4,-4,4); }
                else if (Kind == "settings") {
                    for(int i=0;i<8;i++) {
                        double a=i*Math.PI/4;g.DrawLine(pen,(float)(5*Math.Cos(a)),(float)(5*Math.Sin(a)),(float)(7*Math.Cos(a)),(float)(7*Math.Sin(a)));
                    }
                    g.DrawEllipse(pen,-5,-5,10,10);g.DrawEllipse(pen,-2,-2,4,4);
                }
                else if (Kind == "mode") {
                    g.DrawRectangle(pen,-7,-5,14,10);
                    g.DrawLine(pen,Active?-2:2,-5,Active?-2:2,5);
                }
                else using (var brush = new SolidBrush(pen.Color)) for (int i=-4;i<=4;i+=4) g.FillEllipse(brush,i-1,-1,2,2);
            }
            g.ResetTransform();
            if (Focused && ShowFocusCues)using(var shape=PanelTheme.Rounded(new RectangleF(3*scale,3*scale,Width-6*scale,Height-6*scale),5*scale))using(var pen=new Pen(PanelTheme.Accent,scale))g.DrawPath(pen,shape);
        }
        protected override void Dispose(bool disposing){if(disposing){hoverMotion.Dispose();pressMotion.Dispose();}base.Dispose(disposing);}
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x84&&WindowResize.HitFor(this,m.LParam)!=0){m.Result=new IntPtr(-1);return;}
            base.WndProc(ref m);
        }
    }
}
