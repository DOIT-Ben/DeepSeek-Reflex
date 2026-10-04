using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal static class WindowResize {
        internal const int Border=8;
        internal static int HitTest(Point p,Size size,int border,bool maximized,int radius=0) {
            if(maximized||p.X<0||p.Y<0||p.X>=size.Width||p.Y>=size.Height)return 0;
            if(radius>border) {
                bool west=p.X<radius,east=p.X>=size.Width-radius,north=p.Y<radius,south=p.Y>=size.Height-radius;
                if((west||east)&&(north||south)) {
                    int dx=p.X-(west?radius:size.Width-radius),dy=p.Y-(north?radius:size.Height-radius);
                    if(dx*dx+dy*dy>=(radius-border)*(radius-border))return north?(west?13:14):(west?16:17);
                }
            }
            bool left=p.X<border,right=p.X>=size.Width-border,top=p.Y<border,bottom=p.Y>=size.Height-border;
            return top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:0;
        }
        internal static Point ScreenPoint(IntPtr value) { return new Point((short)(value.ToInt64()&0xffff),(short)((value.ToInt64()>>16)&0xffff)); }
        internal static int HitFor(Control control,IntPtr position) {
            var form=control.FindForm();if(form==null)return 0;
            int border,radius;using(var graphics=form.CreateGraphics()) {
                float scale=graphics.DpiX/96f;
                border=Math.Max(1,(int)Math.Round(Border*scale));
                radius=Math.Max(1,(int)Math.Round(WindowFrame.Radius*scale));
            }
            return HitTest(form.PointToClient(ScreenPoint(position)),form.ClientSize,border,form.WindowState!=FormWindowState.Normal,form.Region!=null?radius:0);
        }
        internal static Cursor CursorFor(int hit) {
            switch(hit){case 10:case 11:return Cursors.SizeWE;case 12:case 15:return Cursors.SizeNS;case 13:case 17:return Cursors.SizeNWSE;case 14:case 16:return Cursors.SizeNESW;default:return null;}
        }
    }
    internal sealed class ResizeChrome : Panel {
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x84&&WindowResize.HitFor(this,m.LParam)!=0){m.Result=new IntPtr(-1);return;}
            base.WndProc(ref m);
        }
    }
}
