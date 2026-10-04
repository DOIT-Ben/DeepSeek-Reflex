using System;
using System.Drawing;
namespace DeepSeekFloat
{
    internal static class WindowModes
    {
        internal static Rectangle Calculate(string mode,Rectangle current,Rectangle area,float scale)
        {
            int width=(int)Math.Round((mode=="reading"?752:410)*scale);
            int height=(int)Math.Round((mode=="reading"?720:616)*scale);
            width=Math.Min(width,area.Width);height=Math.Min(height,area.Height);
            int x=current.X+(current.Width-width)/2,y=current.Y+(current.Height-height)/2;
            return new Rectangle(Math.Max(area.Left,Math.Min(x,area.Right-width)),Math.Max(area.Top,Math.Min(y,area.Bottom-height)),width,height);
        }
    }
}
