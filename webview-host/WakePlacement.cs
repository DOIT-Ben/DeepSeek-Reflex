using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class WakeTarget {
        private readonly Point point;
        private readonly string device;
        private WakeTarget(Point value){point=value;device=Screen.FromPoint(value).DeviceName;}
        internal static WakeTarget Capture(){return At(Cursor.Position);}
        internal static WakeTarget At(Point value){return new WakeTarget(value);}
        internal Screen Resolve(){return Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==device)??Screen.FromPoint(point);}
        internal bool DiffersFrom(Rectangle bounds){return Resolve().DeviceName!=Screen.FromRectangle(bounds).DeviceName;}
    }
    // One wake owns one captured target and logical size, not a history per screen.
    internal sealed class WakePlacement {
        internal readonly WakeTarget Target;
        private readonly SizeF logicalSize;
        internal WakePlacement(WakeTarget target,Size size,float scale){Target=target;logicalSize=new SizeF(size.Width/scale,size.Height/scale);}
        internal Rectangle Bounds(float scale){return Center(logicalSize,scale,Target.Resolve().WorkingArea);}
        internal static Rectangle Center(SizeF size,float scale,Rectangle area){
            int width=Math.Max(1,Math.Min(area.Width,(int)Math.Round(size.Width*scale)));
            int height=Math.Max(1,Math.Min(area.Height,(int)Math.Round(size.Height*scale)));
            return new Rectangle(area.Left+(area.Width-width)/2,area.Top+(area.Height-height)/2,width,height);
        }
        internal static Rectangle Fit(Rectangle bounds,Rectangle area){
            int width=Math.Max(1,Math.Min(bounds.Width,area.Width)),height=Math.Max(1,Math.Min(bounds.Height,area.Height));
            return new Rectangle(Math.Max(area.Left,Math.Min(bounds.Left,area.Right-width)),Math.Max(area.Top,Math.Min(bounds.Top,area.Bottom-height)),width,height);
        }
    }
}
