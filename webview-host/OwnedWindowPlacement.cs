using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal static class OwnedWindowPlacement {
        internal static Rectangle FitBounds(Rectangle bounds,Rectangle area,float scale) {
            int margin=Math.Min((int)Math.Round(12*scale),Math.Min(area.Width,area.Height)/4);
            int width=Math.Min(bounds.Width,area.Width-margin*2),height=Math.Min(bounds.Height,area.Height-margin*2);
            return new Rectangle(Math.Max(area.Left+margin,Math.Min(bounds.Left,area.Right-margin-width)),Math.Max(area.Top+margin,Math.Min(bounds.Top,area.Bottom-margin-height)),width,height);
        }
        internal static void Center(Form window,float scale) {
            var owner=window.Owner;
            var anchor=owner==null?window.Bounds:owner.WindowState==FormWindowState.Minimized?owner.RestoreBounds:owner.Bounds;
            var area=Screen.FromRectangle(anchor).WorkingArea;
            var requested=owner==null?window.Bounds:new Rectangle(anchor.Left+(anchor.Width-window.Width)/2,anchor.Top+(anchor.Height-window.Height)/2,window.Width,window.Height);
            var fitted=FitBounds(requested,area,scale);
            // A short work area must win over a resizable popup's minimum.
            window.MinimumSize=new Size(Math.Min(window.MinimumSize.Width,fitted.Width),Math.Min(window.MinimumSize.Height,fitted.Height));
            window.Bounds=fitted;
        }
        internal static void Attach(Form window,Func<float> scale,Action<float,Rectangle> applyDpi) {
            window.StartPosition=FormStartPosition.Manual;
            bool placed=false;int version=0;
            window.VisibleChanged+=delegate{version++;};
            window.Shown+=delegate {
                if(placed)return;placed=true;
                // Constructor dimensions may use the owner's DPI while the
                // first HWND is created on another monitor. Normalize before
                // moving so WM_DPICHANGED does not scale them a second time.
                float actual=WindowDpi.Scale(window),currentScale=scale();
                if(window.Owner!=null&&currentScale>0&&Math.Abs(actual-currentScale)>0.01f) {
                    float ratio=actual/currentScale;
                    applyDpi(actual,new Rectangle(window.Location,new Size(Math.Max(1,(int)Math.Round(window.Width*ratio)),Math.Max(1,(int)Math.Round(window.Height*ratio)))));
                }
                Center(window,scale());
                int current=version;var owner=window.Owner;
                if(owner!=null)window.BeginInvoke(new Action(delegate {
                    // Settle DPI sizing without re-centering a restored session.
                    if(!window.IsDisposed&&window.Visible&&current==version&&window.Owner==owner)Center(window,scale());
                }));
            };
        }
    }
}
