using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class WindowFrame : Control {
        internal const int Radius=22;
        internal const double DefaultZoom=0.9;
        private float scale=1;
        private bool square,smooth;
        private Size shapedSize;
        private Region hostShape,interactionShape;
        internal int ShapeUpdates {get;private set;}
        internal WindowFrame() {TabStop=false;BackColor=Color.White;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        internal void UpdateShape(Form form,float dpiScale,bool smoothOutline=false) {
            if(form.WindowState==FormWindowState.Minimized||form.ClientSize.Width<1||form.ClientSize.Height<1)return;
            bool maximized=form.WindowState==FormWindowState.Maximized;
            if(shapedSize==form.ClientSize&&scale==dpiScale&&square==maximized&&smooth==smoothOutline
                &&Object.ReferenceEquals(form.Region,hostShape)&&Object.ReferenceEquals(Region,interactionShape)) {
                KeepForemost();return;
            }
            scale=dpiScale;square=form.WindowState==FormWindowState.Maximized;smooth=smoothOutline;
            SetStyle(ControlStyles.OptimizedDoubleBuffer,!smooth);
            Bounds=new Rectangle(Point.Empty,form.ClientSize);
            var rect=new RectangleF(0,0,Width,Height);
            var old=form.Region;
            if(square)form.Region=null;
            else {
                float clip=smooth?SmoothFrame.ContentInset:0;
                var region=NativeRounded(new Rectangle((int)clip,(int)clip,Width-(int)clip*2,Height-(int)clip*2),Radius*scale-clip);
                if(smooth) {
                    float radius=Radius*scale;
                    region.Union(new RectangleF(radius,0,Width-radius*2,Height));
                    region.Union(new RectangleF(0,radius,Width,Height-radius*2));
                }
                form.Region=region;
            }
            if(old!=null)old.Dispose();
            // Cached alpha corners own the curved hit zones when ready. Keep
            // the straight ring cheap, and a full curve ring for the fallback.
            float inset=(WindowResize.Border+1)*scale;var inner=new RectangleF(inset,inset,Width-2*inset,Height-2*inset);
            Region ring;
            if(square||smooth){ring=new Region(rect);ring.Exclude(inner);}
            else {
                ring=NativeRounded(new Rectangle(0,0,Width,Height),Radius*scale);
                using(var inside=NativeRounded(Rectangle.Round(inner),Radius*scale-inset))ring.Exclude(inside);
            }
            old=Region;Region=ring;if(old!=null)old.Dispose();
            shapedSize=form.ClientSize;hostShape=form.Region;interactionShape=Region;ShapeUpdates++;
            KeepForemost();Invalidate();
        }
        private static Region NativeRounded(Rectangle bounds,float radius) {
            int diameter=(int)Math.Round(radius*2);
            var native=Native.CreateRoundRectRgn(bounds.Left,bounds.Top,bounds.Right+1,bounds.Bottom+1,diameter,diameter);
            if(native==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
            try{return Region.FromHrgn(native);}finally{Native.DeleteObject(native);}
        }
        private void KeepForemost() {if(Parent!=null&&Parent.Controls.GetChildIndex(this)!=0)BringToFront();}
        internal static void ConfigureNativeBorder(IntPtr handle) {
            // A custom region supplies our radius. DWM's default corner and
            // border would introduce a second, differently shaped outline.
            int corner=1,border=unchecked((int)0xfffffffe),rendering=1;
            Native.DwmSetWindowAttribute(handle,33,ref corner,4);
            Native.DwmSetWindowAttribute(handle,34,ref border,4);
            // Disable nonclient decoration as well: border color is set-only on
            // some Windows versions, and a custom region owns the whole frame.
            Native.DwmSetWindowAttribute(handle,2,ref rendering,4);
        }
        internal static bool NativeCornersClipped(Form form) {
            var region=Native.CreateRectRgn(0,0,0,0);
            if(region==IntPtr.Zero)return false;
            try {
                return Native.GetWindowRgn(form.Handle,region)==3&&!Native.PtInRegion(region,1,1)
                    &&Native.PtInRegion(region,form.Width/2,SmoothFrame.ContentInset+1)
                    &&!Native.PtInRegion(region,form.Width-2,form.Height-2);
            }finally {Native.DeleteObject(region);}
        }
        internal static bool NativeBorderSuppressed(IntPtr handle) {
            int rendering=1;
            return Native.DwmGetWindowAttribute(handle,1,ref rendering,4)==0&&rendering==0;
        }
        internal bool NativeResizeSurfaceReady(Form form,float dpiScale,SmoothFrame corners=null) {
            if(!IsHandleCreated||Native.GetWindow(Handle,3)!=IntPtr.Zero)return false;
            var region=Native.CreateRectRgn(0,0,0,0);
            if(region==IntPtr.Zero)return false;
            try {
                if(Native.GetWindowRgn(Handle,region)!=3)return false;
                int d=(int)Math.Round((Radius*(1-1/Math.Sqrt(2.0))+WindowResize.Border/(2*Math.Sqrt(2.0)))*dpiScale);
                for(int corner=0;corner<4;corner++) {
                    int x=corner%2==0?d:Width-1-d,y=corner<2?d:Height-1-d;
                    if(!Native.PtInRegion(region,x,y)&&(corners==null||!corners.NativeCornerReady(corner,new Point(x,y))))return false;
                    var point=form.PointToScreen(new Point(x,y));
                    var packed=new IntPtr(((long)(ushort)(short)point.Y<<16)|(ushort)(short)point.X);
                    int hit=corner<2?(corner%2==0?13:14):(corner%2==0?16:17);
                    if(Native.SendMessage(form.Handle,0x84,IntPtr.Zero,packed).ToInt32()!=hit)return false;
                }
                return true;
            }finally {Native.DeleteObject(region);}
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e) {
            if(smooth) {
                int side=SmoothFrame.PatchSize(scale);
                using(var pen=new Pen(Color.FromArgb(226,231,238),1)) {
                    e.Graphics.DrawLine(pen,side,0,Width-side-1,0);e.Graphics.DrawLine(pen,side,Height-1,Width-side-1,Height-1);
                    e.Graphics.DrawLine(pen,0,side,0,Height-side-1);e.Graphics.DrawLine(pen,Width-1,side,Width-1,Height-side-1);
                }
                return;
            }
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float inset=scale/2;var rect=new RectangleF(inset,inset,Width-scale,Height-scale);
            using(var pen=new Pen(Color.FromArgb(226,231,238),scale)) {
                if(square)e.Graphics.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);
                else using(var path=PanelTheme.Rounded(rect,Radius*scale-inset))e.Graphics.DrawPath(pen,path);
            }
        }
    }
}
