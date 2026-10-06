using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace DeepSeekFloat
{
    internal sealed class SelectionResult
    {
        internal string Text, Error, Method;
        internal static SelectionResult Fail(string error) { return new SelectionResult { Error=error }; }
    }
    internal static class SelectionCapture
    {
        internal const int Limit=20000;
        private static int automationBusy;
        private static SelectionResult Validate(string text, string method)
        {
            if (string.IsNullOrWhiteSpace(text)) return SelectionResult.Fail("没有取到文字。请先选中文字，再按取词快捷键。");
            if (text.Length>Limit) return SelectionResult.Fail("这段文字太长，请选择不超过 20,000 字的内容。");
            return new SelectionResult { Text=text, Method=method };
        }
        private static bool SameWindow(IntPtr source) { return source!=IntPtr.Zero && Native.GetForegroundWindow()==source; }
        private static bool ModifierDown() { return (Native.GetAsyncKeyState(0x11)<0 || Native.GetAsyncKeyState(0x10)<0 || Native.GetAsyncKeyState(0x12)<0); }
        private static SelectionResult ReadAutomation()
        {
            try {
                return ReadElement(AutomationElement.FocusedElement);
            } catch(Exception) { return null; }
        }
        internal static SelectionResult ReadElement(AutomationElement element)
        {
            try {
                if(element==null) return null;
                for(int i=0;i<5 && element!=null;i++) {
                    if(element.Current.IsPassword) return SelectionResult.Fail("密码输入框不支持取词。");
                    object pattern;
                    if(element.TryGetCurrentPattern(TextPattern.Pattern,out pattern)) {
                        var ranges=((TextPattern)pattern).GetSelection();
                        string text="";
                        foreach(var range in ranges) { text+=range.GetText(Limit+1); if(text.Length>Limit) break; }
                        return Validate(text,"UI Automation");
                    }
                    element=TreeWalker.ControlViewWalker.GetParent(element);
                }
            } catch(Exception) { }
            return null;
        }
        internal static async Task<SelectionResult> CaptureAsync(IntPtr source)
        {
            uint pid; Native.GetWindowThreadProcessId(source,out pid);
            if(pid==(uint)Process.GetCurrentProcess().Id || !SameWindow(source)) return SelectionResult.Fail("请在其他窗口选中文字后，按取词快捷键。");
            for(int i=0;i<40 && ModifierDown();i++) await Task.Delay(20);
            if(ModifierDown() || !SameWindow(source)) return SelectionResult.Fail("取词已取消，请松开快捷键后重试。");
            if(Interlocked.CompareExchange(ref automationBusy,1,0)!=0) return SelectionResult.Fail("上一次取词还在等待应用响应，请稍后重试。");
            var task=Task.Run(delegate { try { return ReadAutomation(); } finally { Interlocked.Exchange(ref automationBusy,0); } });
            if(await Task.WhenAny(task,Task.Delay(900))!=task) return SelectionResult.Fail("此应用取词响应较慢。可先复制文字，再从设置导入。");
            var result=await task;
            if(!SameWindow(source)) return SelectionResult.Fail("当前窗口已切换，取词已取消。");
            if(result!=null) return result;
            return await CopySelectionAsync(source);
        }
        // Passive detection never simulates Ctrl+C and never reads the clipboard.
        internal static async Task<SelectionResult> ReadOnlyAsync(IntPtr source,System.Drawing.Point point)
        {
            uint pid; Native.GetWindowThreadProcessId(source,out pid);
            if(pid==(uint)Process.GetCurrentProcess().Id || !SameWindow(source)) return null;
            if(Interlocked.CompareExchange(ref automationBusy,1,0)!=0) return null;
            var task=Task.Run(delegate {
                try {
                    var focused=AutomationElement.FocusedElement;
                    if(focused!=null && focused.Current.IsPassword) return null;
                    SelectionResult result=null;
                    if(focused!=null && focused.Current.ProcessId==(int)pid) result=ReadElement(focused);
                    if(result!=null && result.Text!=null) return result;
                    var atPoint=AutomationElement.FromPoint(new System.Windows.Point(point.X,point.Y));
                    return atPoint!=null && atPoint.Current.ProcessId==(int)pid ? ReadElement(atPoint) : null;
                }catch(Exception) { return null; }
                finally { Interlocked.Exchange(ref automationBusy,0); }
            });
            if(await Task.WhenAny(task,Task.Delay(900))!=task || !SameWindow(source)) return null;
            return await task;
        }
        internal static SelectionResult FromClipboard()
        {
            try { return Validate(Clipboard.ContainsText() ? Clipboard.GetText() : null,"剪贴板导入"); }
            catch(ExternalException) { return SelectionResult.Fail("剪贴板正忙，请稍后重试。"); }
        }
        // Keep a materialized snapshot, not a delayed IDataObject owned by another application.
        private static DataObject Snapshot()
        {
            var source=Clipboard.GetDataObject();
            if(source==null) return null;
            var target=new DataObject();
            var formats=source.GetFormats(false);
            if(formats.Length>32) throw new InvalidOperationException();
            foreach(string format in formats) {
                object value=source.GetData(format,false);
                if(value==null) continue;
                var stream=value as MemoryStream;
                if(stream!=null) { if(stream.Length>16*1024*1024) throw new InvalidOperationException(); value=new MemoryStream(stream.ToArray()); }
                else if(value is byte[]) { var bytes=(byte[])value; if(bytes.Length>16*1024*1024) throw new InvalidOperationException(); value=bytes.Clone(); }
                else if(value is Image) value=((Image)value).Clone();
                else if(value is string[]) value=((string[])value).Clone();
                else if(!(value is string) && !(value is int) && !(value is bool)) throw new InvalidOperationException();
                target.SetData(format,false,value);
            }
            return target;
        }
        internal static async Task<SelectionResult> CopySelectionAsync(IntPtr source)
        {
            DataObject previous;
            uint before=Native.GetClipboardSequenceNumber(), copied=0;
            try { previous=Snapshot(); }
            catch(Exception) { return SelectionResult.Fail("当前剪贴板无法安全保留。请手动复制后，从设置导入。"); }
            if(!SameWindow(source) || ModifierDown() || Native.GetClipboardSequenceNumber()!=before) return SelectionResult.Fail("取词环境已变化，请重试。");
            var input=new[]{Native.Key(0x11,false),Native.Key(0x43,false),Native.Key(0x43,true),Native.Key(0x11,true)};
            if(Native.SendInput(4,input,Marshal.SizeOf(typeof(Native.INPUT)))!=4) return SelectionResult.Fail("此窗口不支持快捷取词，请手动复制后导入。");
            try {
                for(int i=0;i<25;i++) {
                    await Task.Delay(20);
                    copied=Native.GetClipboardSequenceNumber();
                    if(copied!=before&&!CopyOwnedBy(source))return SelectionResult.Fail("剪贴板已被其他应用更新，取词已取消。");
                    if(!SameWindow(source)) return SelectionResult.Fail("当前窗口已切换，取词已取消。");
                    if(copied!=before) break;
                }
                if(copied==before) return SelectionResult.Fail("没有检测到复制结果。请先选中文字，或手动复制后导入。");
                return FromClipboard();
            } finally {
                if(copied!=0 && copied!=before && Native.GetClipboardSequenceNumber()==copied && CopyOwnedBy(source)) {
                    try { if(previous==null) Clipboard.Clear(); else Clipboard.SetDataObject(previous,true); }
                    catch(ExternalException) { }
                }
            }
        }
        private static bool CopyOwnedBy(IntPtr source) {
            uint expected,actual;
            var owner=Native.GetClipboardOwner();
            if(owner==IntPtr.Zero||source==IntPtr.Zero)return false;
            Native.GetWindowThreadProcessId(source,out expected);Native.GetWindowThreadProcessId(owner,out actual);
            return expected!=0&&expected==actual;
        }
    }
}
