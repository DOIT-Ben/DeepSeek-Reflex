using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DeepSeekFloat {
    // Four cached alpha corners preserve the opaque native WebView host.
    internal sealed class SmoothFrame : IDisposable {
        internal const int ContentInset=3;
        private readonly Form host;
        private readonly float scale;
        private readonly Action stateChanged;
        private readonly CornerSurface[] corners=new CornerSurface[4];
        private bool synchronizing,ready,disposed,interactiveResize,activationQueued;
        internal bool Ready {get{return ready;}}
        internal bool Visible {get{return corners[0].Visible&&corners[1].Visible&&corners[2].Visible&&corners[3].Visible;}}
        internal bool InteractiveResize {get{return interactiveResize;}}
        internal int UploadCount {get;private set;}
        internal int MoveBatches {get;private set;}
        internal string LastError {get;private set;}
        internal CornerSurface[] Surfaces {get{return (CornerSurface[])corners.Clone();}}
        internal SmoothFrame(Form owner,float dpiScale,Action changed,bool resizable=true) {
            host=owner;scale=dpiScale;stateChanged=changed;
            for(int i=0;i<4;i++)corners[i]=new CornerSurface(host,scale,resizable);
            host.LocationChanged+=OwnerChanged;host.SizeChanged+=OwnerChanged;host.VisibleChanged+=OwnerChanged;
            host.Shown+=OwnerChanged;host.Activated+=OwnerActivated;
        }
        private void OwnerChanged(object sender,EventArgs e){SyncOwner();}
        private void OwnerActivated(object sender,EventArgs e) {
            if(activationQueued||disposed||!host.IsHandleCreated)return;
            // Activation can run inside Windows' topmost transition. Wait for
            // that transition to finish before adjusting the owned patches.
            activationQueued=true;host.BeginInvoke(new Action(delegate{activationQueued=false;SyncOwner();}));
        }
        private void SetReady(bool value){if(ready==value)return;ready=value;if(stateChanged!=null)stateChanged();}
        internal void BeginInteractiveResize(){interactiveResize=true;SyncOwner();}
        internal void EndInteractiveResize(){interactiveResize=false;SyncOwner();}
        internal static int PatchSize(float scale){return (int)Math.Ceiling(WindowFrame.Radius*scale)+2;}
        internal static Rectangle PatchBounds(Rectangle bounds,int index,int side){return new Rectangle(index%2==0?bounds.Left:bounds.Right-side,index<2?bounds.Top:bounds.Bottom-side,side,side);}
        internal bool Aligned {
            get{if(!ready||!Visible)return false;int side=PatchSize(scale);for(int i=0;i<4;i++)if(corners[i].Bounds!=PatchBounds(host.Bounds,i,side))return false;return true;}
        }
        internal bool NativeCornerReady(int index,Point ownerPoint) {
            if(!Aligned)return false;
            var screen=host.PointToScreen(ownerPoint);var packed=new IntPtr(((long)(ushort)(short)screen.Y<<16)|(ushort)(short)screen.X);
            int expected=index<2?(index%2==0?13:14):(index%2==0?16:17);
            return Native.SendMessage(corners[index].Handle,0x84,IntPtr.Zero,packed).ToInt32()==expected;
        }
        private void Hide(){foreach(var corner in corners)corner.Hide();SetReady(false);}
        internal void SyncOwner() {
            if(synchronizing||disposed||host.IsDisposed)return;
            synchronizing=true;
            try {
                if(!host.Visible||host.WindowState!=FormWindowState.Normal){Hide();return;}
                int side=PatchSize(scale);
                // Windows promotes dialogs owned by a pinned window even when
                // WinForms' TopMost property remains false. The alpha patches
                // must follow the actual HWND band rather than that property.
                bool topmost=(Native.GetWindowLong(host.Handle,-20)&0x8)!=0;
                for(int i=0;i<4;i++) {
                    var corner=corners[i];if(corner.Owner!=host)corner.Owner=host;if(corner.TopMost!=topmost)corner.TopMost=topmost;
                    if(!corner.Uploaded){using(var image=RenderCorner(i,scale))corner.Upload(image,PatchBounds(host.Bounds,i,side).Location);UploadCount++;}
                }
                MoveCorners(host.Bounds);
                const uint bandFlags=0x213; // NOMOVE | NOSIZE | NOACTIVATE | NOOWNERZORDER
                foreach(var corner in corners) {
                    if(!corner.Visible)corner.Show(host);
                    // Inherited native band changes do not update Form.TopMost.
                    // Repair a stale HWND even when the managed value matches.
                    if(((Native.GetWindowLong(corner.Handle,-20)&0x8)!=0)!=topmost
                        &&!Native.SetWindowPos(corner.Handle,new IntPtr(topmost?-1:-2),0,0,0,0,bandFlags))throw new Win32Exception();
                }
                LastError=null;SetReady(true);
            }catch(Win32Exception e){Fail(e);}catch(ExternalException e){Fail(e);}catch(OutOfMemoryException e){Fail(e);}
            finally{synchronizing=false;}
        }
        private void Fail(Exception error){LastError=error.GetType().Name;Hide();}
        // Only follow committed owner geometry; never position the host here.
        private void MoveCorners(Rectangle bounds) {
            int side=PatchSize(scale);bool moved=false;
            for(int i=0;i<4;i++) {
                var target=PatchBounds(bounds,i,side);if(corners[i].Bounds==target)continue;
                corners[i].MoveTo(target.Location);corners[i].RefreshGeometry(target);moved=true;
            }
            if(moved)MoveBatches++;
        }
        internal static Bitmap RenderCorner(int index,float scale) {
            int side=PatchSize(scale);
            using(var whole=Render(new Size(side*2,side*2),scale))return whole.Clone(new Rectangle(index%2==0?0:side,index<2?0:side,side,side),PixelFormat.Format32bppPArgb);
        }
        internal static Bitmap Render(Size size,float scale) {
            var bitmap=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
            try {
                using(var g=Graphics.FromImage(bitmap)) {
                    g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                    float radius=WindowFrame.Radius*scale,band=(WindowResize.Border+1)*scale;
                    using(var path=PanelTheme.Rounded(new RectangleF(0,0,size.Width,size.Height),radius))using(var white=new SolidBrush(Color.White))g.FillPath(white,path);
                    g.CompositingMode=CompositingMode.SourceCopy;
                    using(var path=PanelTheme.Rounded(new RectangleF(band,band,size.Width-band*2,size.Height-band*2),radius-band))using(var clear=new SolidBrush(Color.Transparent))g.FillPath(clear,path);
                    g.CompositingMode=CompositingMode.SourceOver;
                    int width=WindowFrame.OutlineWidth(scale);float inset=width/2f;
                    using(var path=PanelTheme.Rounded(new RectangleF(inset,inset,size.Width-width,size.Height-width),radius-inset))using(var pen=new Pen(WindowFrame.Outline,width))g.DrawPath(pen,path);
                    // Match the host's crisp DPI-scaled straight edges at the
                    // curve tangents, including the two-pixel patch overlap.
                    g.SmoothingMode=SmoothingMode.None;g.CompositingMode=CompositingMode.SourceCopy;
                    int tangent=(int)Math.Ceiling(radius);
                    WindowFrame.DrawStraightOutline(g,size,scale,tangent);
                }
                return bitmap;
            }catch{bitmap.Dispose();throw;}
        }
        public void Dispose() {
            if(disposed)return;disposed=true;host.LocationChanged-=OwnerChanged;host.SizeChanged-=OwnerChanged;host.VisibleChanged-=OwnerChanged;
            host.Shown-=OwnerChanged;host.Activated-=OwnerActivated;
            foreach(var corner in corners)corner.Dispose();ready=false;
        }
    }
    internal sealed class CornerSurface : Form {
        private readonly Form host;private readonly float scale;private readonly bool resizable;
        internal bool Uploaded {get;private set;}
        internal CornerSurface(Form owner,float dpiScale,bool allowResize) {
            host=owner;scale=dpiScale;resizable=allowResize;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Text="DeepSeek-Reflex 圆角绘制层";
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var value=base.CreateParams;value.ExStyle|=0x80000|0x8000000|0x80;return value;}}
        internal void RefreshGeometry(Rectangle bounds){UpdateBounds(bounds.X,bounds.Y,bounds.Width,bounds.Height,bounds.Width,bounds.Height);}
        internal void MoveTo(Point location) {
            // Reposition the cached layered bitmap without a DC, new pixels,
            // resize or synchronous repaint of the underlying WebView host.
            var destination=new Native.POINT{x=location.X,y=location.Y};
            if(!Native.MoveLayeredWindow(Handle,IntPtr.Zero,ref destination,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,0,IntPtr.Zero,0))throw new Win32Exception();
        }
        internal void Upload(Bitmap bitmap,Point position) {
            var dc=Native.CreateCompatibleDC(IntPtr.Zero);if(dc==IntPtr.Zero)throw new Win32Exception();
            IntPtr image=IntPtr.Zero,previous=IntPtr.Zero;
            try {
                image=bitmap.GetHbitmap(Color.FromArgb(0));previous=Native.SelectObject(dc,image);
                var destination=new Native.POINT{x=position.X,y=position.Y};var source=new Native.POINT();var size=new Native.SIZE{cx=bitmap.Width,cy=bitmap.Height};
                var blend=new Native.BLENDFUNCTION{SourceConstantAlpha=255,AlphaFormat=1};
                if(!Native.UpdateLayeredWindow(Handle,IntPtr.Zero,ref destination,ref size,dc,ref source,0,ref blend,2))throw new Win32Exception();
                RefreshGeometry(new Rectangle(position,bitmap.Size));Uploaded=true;
            }finally {
                if(previous!=IntPtr.Zero&&previous!=new IntPtr(-1))Native.SelectObject(dc,previous);
                if(image!=IntPtr.Zero)Native.DeleteObject(image);Native.DeleteDC(dc);
            }
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x84) {
                int hit=resizable?WindowResize.HitTest(host.PointToClient(WindowResize.ScreenPoint(m.LParam)),host.ClientSize,(int)Math.Round(WindowResize.Border*scale),host.WindowState!=FormWindowState.Normal,(int)Math.Round(WindowFrame.Radius*scale)):0;
                m.Result=new IntPtr(hit==0?-1:hit);return;
            }
            if(m.Msg==0x21){m.Result=new IntPtr(3);return;}
            if(m.Msg==0x20&&resizable){var cursor=WindowResize.CursorFor((int)(m.LParam.ToInt64()&0xffff));if(cursor!=null){Cursor.Current=cursor;m.Result=new IntPtr(1);return;}}
            if(m.Msg==0xa1&&resizable&&WindowResize.CursorFor(m.WParam.ToInt32())!=null){Native.ReleaseCapture();Native.SetForegroundWindow(host.Handle);Native.SendMessage(host.Handle,m.Msg,m.WParam,m.LParam);return;}
            base.WndProc(ref m);
        }
    }
}
