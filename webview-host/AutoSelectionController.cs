using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DeepSeekFloat
{
    internal sealed class AutoSelectionController : IDisposable
    {
        private readonly Control dispatcher;
        private readonly Native.MouseHook callback;
        private readonly SelectionGesture gesture=new SelectionGesture();
        private readonly Timer settle=new Timer { Interval=220 };
        private QuickAnswerWindow popup;
        private IntPtr hook, source;
        private Point point;
        private int revision;
        private bool disposed;
        internal bool Enabled { get; private set; }
        internal bool Registered { get { return hook!=IntPtr.Zero; } }
        internal AutoSelectionController(Control dispatcher)
        {
            this.dispatcher=dispatcher;callback=OnMouse;
            settle.Tick+=async delegate {
                settle.Stop();int version=revision;var window=source;var anchor=point;
                var result=await SelectionCapture.ReadOnlyAsync(window,anchor);
                if(disposed || !Enabled || version!=revision || result==null || result.Text==null) return;
                ShowSelection(result,anchor);
            };
        }
        internal void SetEnabled(bool value)
        {
            revision++;settle.Stop();
            if(hook!=IntPtr.Zero) { Native.UnhookWindowsHookEx(hook);hook=IntPtr.Zero; }
            Enabled=value;
            if(value) hook=Native.SetWindowsHookEx(14,callback,Native.GetModuleHandle(null),0);
            else if(popup!=null && !popup.IsDisposed) popup.Close();
        }
        private IntPtr OnMouse(int code,IntPtr message,IntPtr data)
        {
            if(code>=0 && !disposed && Enabled && (message.ToInt32()==0x201 || message.ToInt32()==0x202)) {
                try {
                    var mouse=(Native.MOUSEHOOKDATA)Marshal.PtrToStructure(data,typeof(Native.MOUSEHOOKDATA));
                    var position=new Point(mouse.point.x,mouse.point.y);
                    if(message.ToInt32()==0x201) {
                        gesture.Down(position);revision++;settle.Stop();
                        if(popup!=null && !popup.IsDisposed && !popup.Expanded && !popup.Bounds.Contains(position)) {
                            var dismiss=popup;
                            dispatcher.BeginInvoke(new Action(delegate { if(!dismiss.IsDisposed && !dismiss.Expanded) dismiss.Close(); }));
                        }
                    } else if(gesture.Up(position,mouse.time)) {
                        point=position;source=Native.GetForegroundWindow();
                        // No UI Automation, window creation or network call runs in the hook callback.
                        settle.Stop();settle.Start();
                    }
                }catch(Exception) { }
            }
            return Native.CallNextHookEx(hook,code,message,data);
        }
        internal void ShowSelection(SelectionResult result,Point anchor)
        {
            if(disposed || string.IsNullOrWhiteSpace(result.Text) || result.Text.Length>SelectionCapture.Limit) return;
            if(popup!=null && !popup.IsDisposed) {
                if(popup.Expanded && popup.Visible) return;
                popup.Close();
            }
            popup=new QuickAnswerWindow(result.Text,anchor);
            popup.Show();
        }
        public void Dispose()
        {
            if(disposed) return;disposed=true;revision++;Enabled=false;
            if(hook!=IntPtr.Zero)Native.UnhookWindowsHookEx(hook);
            hook=IntPtr.Zero;settle.Stop();settle.Dispose();
            if(popup!=null && !popup.IsDisposed)popup.Close();
        }
    }
}
