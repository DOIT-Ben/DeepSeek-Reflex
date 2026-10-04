using System;
using System.Drawing;
namespace DeepSeekFloat
{
    internal sealed class SelectionGesture
    {
        private Point down, lastUp;
        private uint lastTime;
        private bool held;
        internal void Down(Point point) { down=point;held=true; }
        internal bool Up(Point point,uint time)
        {
            if(!held) return false;
            held=false;
            bool drag=Math.Abs(point.X-down.X)>=5 || Math.Abs(point.Y-down.Y)>=5;
            bool multiple=lastTime!=0 && unchecked(time-lastTime)<=System.Windows.Forms.SystemInformation.DoubleClickTime && Math.Abs(point.X-lastUp.X)<8 && Math.Abs(point.Y-lastUp.Y)<8;
            lastUp=point;lastTime=time;
            return drag || multiple;
        }
    }
}
