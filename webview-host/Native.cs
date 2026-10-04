using System;
using System.Runtime.InteropServices;

namespace DeepSeekFloat
{
    internal static class Native
    {
        internal const int HotkeyId = 1;
        internal const int CaptureHotkeyId = 2;
        internal delegate IntPtr MouseHook(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr SetWindowsHookEx(int id,MouseHook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { internal int x,y; }
        [StructLayout(LayoutKind.Sequential)] internal struct MOUSEHOOKDATA { internal POINT point; internal uint mouseData,flags,time; internal UIntPtr extra; }
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll", SetLastError=true)] internal static extern uint SendInput(uint count, INPUT[] input, int size);
        [StructLayout(LayoutKind.Sequential)] internal struct INPUT { internal uint type; internal INPUTUNION data; }
        [StructLayout(LayoutKind.Explicit)] internal struct INPUTUNION {
            [FieldOffset(0)] internal KEYBDINPUT keyboard;
            [FieldOffset(0)] internal MOUSEINPUT mouse;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { internal ushort key, scan; internal uint flags, time; internal UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { internal int x,y; internal uint mouseData,flags,time; internal UIntPtr extra; }
        internal static INPUT Key(ushort key, bool up) { return new INPUT { type=1, data=new INPUTUNION { keyboard=new KEYBDINPUT { key=key, flags=up ? 2u : 0u } } }; }
        internal const int OpenMessage = 0x8001;
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls, string title);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        [DllImport("user32.dll")] internal static extern int GetWindowRgn(IntPtr hwnd,IntPtr region);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd,uint command);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
        [DllImport("gdi32.dll")] internal static extern bool PtInRegion(IntPtr region,int x,int y);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
        [StructLayout(LayoutKind.Sequential)] internal struct SIZE {internal int cx,cy;}
        [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct BLENDFUNCTION {internal byte BlendOp,BlendFlags,SourceConstantAlpha,AlphaFormat;}
        [DllImport("gdi32.dll",SetLastError=true)] internal static extern IntPtr CreateCompatibleDC(IntPtr value);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr destinationDc,ref POINT destination,ref SIZE size,IntPtr sourceDc,ref POINT source,uint color,ref BLENDFUNCTION blend,uint flags);
        [StructLayout(LayoutKind.Sequential)] internal struct RECT {internal int left,top,right,bottom;}
        [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr BeginDeferWindowPos(int count);
        [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr DeferWindowPos(IntPtr batch,IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool EndDeferWindowPos(IntPtr batch);
        [DllImport("user32.dll")] internal static extern bool SystemParametersInfo(uint action,uint parameter,ref bool value,uint flags);
    }
}
