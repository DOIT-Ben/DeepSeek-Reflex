using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal static class WindowDpi {
        internal static float Scale(Form form) {
            uint dpi=Native.GetDpiForWindow(form.Handle);
            return dpi==0?1:dpi/96f;
        }
        internal static Rectangle Suggested(IntPtr value) {
            var rect=(Native.RECT)Marshal.PtrToStructure(value,typeof(Native.RECT));
            return Rectangle.FromLTRB(rect.left,rect.top,rect.right,rect.bottom);
        }
        internal static float MessageScale(IntPtr value){return (value.ToInt64()&0xffff)/96f;}
    }
}
