using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeepSeekFloat {
 internal static class WebViewFocusTests {
  static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
  static int passed;
  sealed class DispatchProbe : NativeWindow,IDisposable {
   internal int Depth;
   internal DispatchProbe(Control target){AssignHandle(target.Handle);}
   protected override void WndProc(ref Message message){
    bool focus=message.Msg==7;
    if(focus)Depth++;
    try{base.WndProc(ref message);}finally{if(focus)Depth--;}
   }
   public void Dispose(){ReleaseHandle();}
  }
  static void Check(bool yes,string name){if(!yes)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
  static object Field(object o,string name){return o.GetType().GetField(name,Flags).GetValue(o);}
  static void Set(object o,string name,object value){o.GetType().GetField(name,Flags).SetValue(o,value);}
  static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,Flags).Invoke(o,args);}
  static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();System.Threading.Thread.Sleep(5);}while(DateTime.UtcNow<until);}
  static void Wait(Task t){var until=DateTime.UtcNow.AddSeconds(15);while(!t.IsCompleted&&DateTime.UtcNow<until)Pump(5);if(!t.IsCompleted)throw new TimeoutException();t.GetAwaiter().GetResult();}
  static string Eval(WebView2 view,string script){var t=view.CoreWebView2.ExecuteScriptAsync(script);Wait(t);return t.Result;}
  [STAThread] static int Main(string[] args){
   try{
    var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
    typeof(Preferences).GetField("Root",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,root);
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    var config=WindowSettings.Defaults();config.ToggleKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F23);config.CaptureKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F24);config.EdgeDockEnabled=false;config.Save();GettingStarted.Acknowledge();
    using(var window=new ChatWindow(new[]{"--background"})){
     Set(window,"initializing",true);Set(window,"allowShow",true);window.Text="DSR native focus regression (isolated)";
     var view=(WebView2)Field(window,"browser");var away=new Button{Text="isolated focus target",Location=new Point(10,60)};
     window.Controls.Add(away);away.BringToFront();
     int focused=0,synchronous=0;var probe=new DispatchProbe(view);
     view.GotFocus+=delegate{focused++;if(probe.Depth!=0)synchronous++;};
     window.Show();window.Activate();Pump(100);
     Check(Native.GetForegroundWindow()==window.Handle,"native focus fixture owns the foreground; no skipped acceptance");
     for(int n=0;n<3;n++){away.Focus();Pump(20);view.Focus();Pump(20);}
     Check(focused>=3,"browser receives genuine native focus messages");
     Check(synchronous==0,"WebView focus transfer never runs inside WM_SETFOCUS");
     away.Focus();Pump(20);int before=focused;view.Focus();away.Focus();Pump(50);
     Check(away.Focused&&focused==before,"queued browser focus cannot steal a newer control focus");
     view.Focus();window.Hide();Pump(50);
     Check(!window.Visible&&focused==before,"hiding cancels a queued browser focus transfer");
     window.Show();window.Activate();Pump(50);
     var loaded=new TaskCompletionSource<bool>();
     view.CoreWebView2InitializationCompleted+=delegate{
      var core=view.CoreWebView2;core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
      core.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs e){e.Response=core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><textarea aria-label='isolated draft'>test draft only</textarea>")),200,"OK","Content-Type: text/html; charset=utf-8");};
      core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(e.Uri!="https://chat.deepseek.com/")e.Cancel=true;};
      core.NavigationCompleted+=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e){loaded.TrySetResult(e.IsSuccess);};
     };
     // The production host creates its environment on a real WinForms context.
     using(var context=new WindowsFormsSynchronizationContext()){
      System.Threading.SynchronizationContext.SetSynchronizationContext(context);
      Set(window,"initializing",false);Wait((Task)Call(window,"InitializeBrowser"));Wait(loaded.Task);Check(loaded.Task.Result,"actual DSR WebView loads only the isolated local response");
      Eval(view,"(()=>{const e=document.querySelector('textarea');e.focus();e.setSelectionRange(2,7,'backward');})()");
      for(int n=0;n<3;n++){
       window.Hide();Pump(20);Call(window,"ShowChatAt",WakeTarget.At(new Point(window.Left+window.Width/2,window.Top+window.Height/2)));Pump(250);
       Check(Eval(view,"(()=>{const e=document.querySelector('textarea');return e.value==='test draft only'&&e.selectionStart===2&&e.selectionEnd===7&&e.selectionDirection==='backward'&&document.activeElement===e})()") == "true","wake preserves draft and backward selection: "+n);
      }
      window.WindowState=FormWindowState.Minimized;Pump(30);Call(window,"ShowChatAt",WakeTarget.At(new Point(800,400)));Pump(250);
      Check(window.WindowState==FormWindowState.Normal&&Eval(view,"document.querySelector('textarea').value==='test draft only'")=="true","minimized restore retains the local draft");
      Check(synchronous==0,"initialized Chromium also receives focus only after native dispatch");
     }
     away.Focus();Pump(20);before=focused;view.Focus();probe.Dispose();Set(window,"quitting",true);window.Close();Pump(50);
     Check(focused==before,"closing cancels queued focus without a disposed-control callback");
    }
    Console.WriteLine("TOTAL "+passed+" native focus checks; IME candidate positions require separate real-input verification");return 0;
   }catch(Exception e){Console.Error.WriteLine(e);return 1;}
  }
 }
}
