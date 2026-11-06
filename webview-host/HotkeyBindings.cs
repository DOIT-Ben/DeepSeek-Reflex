using System;
using System.Windows.Forms;
namespace DeepSeekFloat
{
    internal enum HotkeyAvailability { All, ToggleOnly, None }
    internal sealed class HotkeyBindings : IDisposable
    {
        private readonly IntPtr window;
        internal HotkeyAvailability Availability {get;private set;}
        internal int ToggleKeys { get; private set; }
        internal int CaptureKeys { get; private set; }
        internal bool ToggleRegistered { get; private set; }
        internal bool CaptureRegistered { get; private set; }
        internal HotkeyBindings(IntPtr window,int toggle,int capture) { this.window=window;ToggleKeys=toggle;CaptureKeys=capture; }
        internal static string Validate(int toggle,int capture)
        {
            if(!ValidKey(toggle)||!ValidKey(capture))return "快捷键需要 Ctrl 或 Alt，再配合字母、数字或功能键。";
            if(toggle==capture)return "唤出键和取词键不能相同。";
            return null;
        }
        internal static bool ValidKey(int value)
        {
            var keys=(Keys)value;int key=(int)(keys&Keys.KeyCode);
            if((value&~((int)Keys.Control|(int)Keys.Alt|(int)Keys.Shift|0xffff))!=0 || (keys&(Keys.Control|Keys.Alt))==0)return false;
            bool allowed=(key>=(int)Keys.A&&key<=(int)Keys.Z)||(key>=(int)Keys.D0&&key<=(int)Keys.D9)||(key>=(int)Keys.F1&&key<=(int)Keys.F24)||key==(int)Keys.Space;
            return allowed && keys!=(Keys.Alt|Keys.F4);
        }
        internal static string Format(int value)
        {
            var keys=(Keys)value;string result="";
            if((keys&Keys.Control)!=0)result+="Ctrl+";
            if((keys&Keys.Alt)!=0)result+="Alt+";
            if((keys&Keys.Shift)!=0)result+="Shift+";
            var key=keys&Keys.KeyCode;string name=key.ToString();
            if(key>=Keys.D0&&key<=Keys.D9)name=((int)key-(int)Keys.D0).ToString();
            return result+name;
        }
        private bool Register(int id,int value)
        {
            var keys=(Keys)value;uint modifiers=0x4000;
            if((keys&Keys.Control)!=0)modifiers|=2;
            if((keys&Keys.Alt)!=0)modifiers|=1;
            if((keys&Keys.Shift)!=0)modifiers|=4;
            return Native.RegisterHotKey(window,id,modifiers,(uint)(keys&Keys.KeyCode));
        }
        private void Release()
        {
            Native.UnregisterHotKey(window,Native.HotkeyId);Native.UnregisterHotKey(window,Native.CaptureHotkeyId);
            ToggleRegistered=CaptureRegistered=false;
        }
        internal string Apply(int toggle,int capture)
        {
            string error=Validate(toggle,capture);if(error!=null)return error;
            int oldToggle=ToggleKeys,oldCapture=CaptureKeys;bool toggleActive=ToggleRegistered,captureActive=CaptureRegistered;
            Release();
            bool first=Register(Native.HotkeyId,toggle),second=first && Register(Native.CaptureHotkeyId,capture);
            if(!second) {
                Release();ToggleRegistered=toggleActive&&Register(Native.HotkeyId,oldToggle);CaptureRegistered=captureActive&&Register(Native.CaptureHotkeyId,oldCapture);
                string restored=(!toggleActive||ToggleRegistered)&&(!captureActive||CaptureRegistered) ? "原来的设置已保留。" : "原快捷键恢复失败，请重新设置。";
                return HotkeyBindings.Format(first?capture:toggle)+" 已被占用，请换一组。"+restored;
            }
            ToggleKeys=toggle;CaptureKeys=capture;ToggleRegistered=CaptureRegistered=true;
            if(Availability==HotkeyAvailability.None)Release();
            else if(Availability==HotkeyAvailability.ToggleOnly){Native.UnregisterHotKey(window,Native.CaptureHotkeyId);CaptureRegistered=false;}
            return null;
        }
        internal string SetAvailability(HotkeyAvailability value) {
            bool wantToggle=value!=HotkeyAvailability.None,wantCapture=value==HotkeyAvailability.All;
            var previous=Availability;bool oldToggle=ToggleRegistered,oldCapture=CaptureRegistered;
            if(!wantCapture&&CaptureRegistered){Native.UnregisterHotKey(window,Native.CaptureHotkeyId);CaptureRegistered=false;}
            if(!wantToggle&&ToggleRegistered){Native.UnregisterHotKey(window,Native.HotkeyId);ToggleRegistered=false;}
            if(wantToggle&&!ToggleRegistered)ToggleRegistered=Register(Native.HotkeyId,ToggleKeys);
            if(wantCapture&&ToggleRegistered&&!CaptureRegistered)CaptureRegistered=Register(Native.CaptureHotkeyId,CaptureKeys);
            if((wantToggle&&!ToggleRegistered)||(wantCapture&&!CaptureRegistered)) {
                Release();ToggleRegistered=oldToggle&&Register(Native.HotkeyId,ToggleKeys);CaptureRegistered=oldCapture&&Register(Native.CaptureHotkeyId,CaptureKeys);Availability=previous;
                return "快捷键恢复失败，请检查是否被其他应用占用。";
            }
            Availability=value;return null;
        }
        internal void Suspend() { SetAvailability(HotkeyAvailability.None); }
        internal string Resume() { return SetAvailability(HotkeyAvailability.All); }
        public void Dispose() { Release(); }
    }
}
