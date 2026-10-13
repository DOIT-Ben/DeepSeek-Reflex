using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
namespace DeepSeekFloat {
 internal static class BoundaryTests {
  static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
  static string root;static int passed;
  static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
  static object Field(object value,string name){return value.GetType().GetField(name,Flags).GetValue(value);}
  static void Set(object value,string name,object data){value.GetType().GetField(name,Flags).SetValue(value,data);}
  static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Flags).Invoke(value,args);}
  static void Export(string name,string value){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(string)).WriteObject(stream,value);File.WriteAllText(Path.Combine(root,name),Encoding.UTF8.GetString(stream.ToArray()));}}
  static ChatWindow Window(bool hide) {
   var settings=WindowSettings.Defaults();settings.ToggleKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F23);settings.CaptureKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F24);settings.HideToTrayOnToggle=hide;settings.Save();GettingStarted.Acknowledge();
   var window=new ChatWindow(new[]{"--background"});Set(window,"initializing",true);Set(window,"allowShow",true);
   window.Text="DeepSeek-Reflex isolated boundary fixture";window.Show();Application.DoEvents();return window;
  }
  static void Close(ChatWindow window){Set(window,"quitting",true);window.Close();Application.DoEvents();}
  static void Modal(bool hide,bool help) {
   using(var window=Window(hide))using(var timer=new Timer{Interval=30}) {
    bool fired=false;int ticks=0;
    timer.Tick+=delegate {
     var dialog=window.OwnedForms.FirstOrDefault(f=>help?f is GettingStartedDialog:f is SettingsDialog);
     if(dialog!=null&&!fired){
      fired=true;Call(window,"ToggleWindow");Application.DoEvents();
      Check(window.Visible&&window.WindowState==FormWindowState.Normal&&dialog.Visible,"modal tray click focuses panel: hide="+hide+" guide="+help);
      window.WindowState=FormWindowState.Minimized;Call(window,"ShowChat");Application.DoEvents();
      Check(window.Visible&&window.WindowState==FormWindowState.Normal&&dialog.Visible,"explicit wake restores modal and owner");
      dialog.DialogResult=DialogResult.Cancel;dialog.Close();timer.Stop();
     }else if(++ticks>80){timer.Stop();if(dialog!=null)dialog.Close();throw new Exception("Modal timed out");}
    };
    timer.Start();Call(window,help?"ShowHelp":"ShowSettings");timer.Stop();Check(fired,"modal test executed");
    Close(window);
   }
  }
  static void WindowStates() {
   foreach(bool hide in new[]{false,true})using(var window=Window(hide)){
    window.WindowState=FormWindowState.Maximized;Application.DoEvents();Call(window,"ToggleWindow");Call(window,"ShowChat");Application.DoEvents();
    Check(window.Visible&&window.WindowState==FormWindowState.Maximized,"wake preserves maximized state: hide="+hide);
    window.WindowState=FormWindowState.Normal;Call(window,"ToggleWindow");Call(window,"ShowChat");
    Check(window.WindowState==FormWindowState.Normal,"wake preserves normal state");
    Close(window);
   }
  }
  static void SaveDenied() {
   using(var window=Window(false)) {
    var original=(WindowSettings)Field(window,"settings");var next=original.Clone();next.ToggleKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F20);
    string blocked=Path.Combine(root,"window-settings.json.new");Directory.CreateDirectory(blocked);
    try{
     string error=(string)Call(window,"ApplySettings",next);var keys=(HotkeyBindings)Field(window,"hotkeys");
     Check(error!=null&&keys.ToggleKeys==original.ToggleKeys&&keys.ToggleRegistered&&WindowSettings.Load().ToggleKeys==original.ToggleKeys,"access-denied save rolls back actual binding and disk settings");
    }finally{Directory.Delete(blocked);}
    Close(window);
   }
  }
  static void Accessibility() {
   using(var shell=new Form())using(var custom=new PanelSwitch("Boundary switch")){
    custom.Bounds=new Rectangle(10,10,280,32);shell.Controls.Add(custom);shell.Show();Application.DoEvents();
    foreach(bool value in new[]{true,false}){
     custom.Checked=value;Application.DoEvents();
     Check(((custom.AccessibilityObject.State&AccessibleStates.Checked)!=0)==value,"MSAA checked state matches "+value);
     var element=System.Windows.Automation.AutomationElement.FromHandle(custom.Handle);object pattern;
     Check(element.TryGetCurrentPattern(System.Windows.Automation.TogglePattern.Pattern,out pattern),"UIA exposes TogglePattern");
     Check(((System.Windows.Automation.TogglePattern)pattern).Current.ToggleState==(value?System.Windows.Automation.ToggleState.On:System.Windows.Automation.ToggleState.Off),"UIA switch matches "+value);
    }
    custom.AccessibilityObject.DoDefaultAction();Check(custom.Checked,"accessible default action toggles");
    shell.Close();
   }
  }
  static void DpiAndSmallGuide() {
   using(var window=Window(false)){
    var smooth=(SmoothFrame)Field(window,"smoothFrame");var before=window.Bounds;
    window.ApplyDpi(1.5f,new Rectangle(before.Location,new Size(720,930)));Application.DoEvents();
    Check((float)Field(window,"dpiScale")==1.5f&&smooth.Ready&&smooth.Aligned,"chat DPI transition rebuilds aligned corners");
    int uploads=smooth.UploadCount;window.ApplyDpi(1.5f,window.Bounds);
    Check(smooth.UploadCount==uploads,"same DPI does not reupload corner pixels");
    var rect=new Native.RECT{left=-720,top=40,right=0,bottom=970};
    var pointer=System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.RECT)));
    try{
     System.Runtime.InteropServices.Marshal.StructureToPtr(rect,pointer,false);
     Native.SendMessage(window.Handle,0x2e0,new IntPtr(144|(144<<16)),pointer);Application.DoEvents();
     Console.WriteLine("DPI message evidence: bounds="+window.Bounds+" scale="+Field(window,"dpiScale")+" aligned="+smooth.Aligned);
     Check(window.Left==-720&&(float)Field(window,"dpiScale")==1.5f&&smooth.Aligned,"native DPI message honors suggested bounds including negative coordinates");
    }finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);}
    window.ApplyDpi(1,before);Application.DoEvents();Check(smooth.Aligned,"chat DPI round trip remains aligned");Close(window);
   }
   using(var dialog=new SettingsDialog(WindowSettings.Defaults(),delegate{return null;})){
    dialog.Show();dialog.ApplyDpi(1.5f,new Rectangle(40,40,660,900));Application.DoEvents();
    Check(((SmoothFrame)Field(dialog,"smoothFrame")).Aligned,"settings DPI transition keeps corners aligned");dialog.Close();
   }
   using(var guide=new GettingStartedDialog(WindowSettings.Defaults())){
    guide.Show();guide.ClientSize=new Size(400,330);Application.DoEvents();
    var next=(PanelButton)Field(guide,"next");var skip=(PanelButton)Field(guide,"skip");
    Check(next.Bottom<=guide.ClientSize.Height&&skip.Bottom<=guide.ClientSize.Height,"short guide keeps fixed footer reachable");
    var viewport=(Panel)Field(guide,"viewport");Check(viewport.VerticalScroll.Visible,"short guide content scrolls");
    guide.ApplyDpi(1.5f,new Rectangle(40,40,600,650));Application.DoEvents();
    Check(((SmoothFrame)Field(guide,"smoothFrame")).Aligned,"guide DPI transition keeps corners aligned");guide.Close();
   }
   using(var popup=new PopupWindow(new Uri("https://example.com"),1)){
    popup.Show();Application.DoEvents();Check(popup.FormBorderStyle==FormBorderStyle.None&&((SmoothFrame)Field(popup,"smoothFrame")).Aligned,"popup shares native rounded frame");
    Check(((Label)Field(popup,"origin")).Text=="example.com","popup presents page domain");
    popup.ApplyDpi(1.5f,new Rectangle(40,40,660,900));Application.DoEvents();Check(((SmoothFrame)Field(popup,"smoothFrame")).Aligned,"popup DPI transition");
    popup.Close();Check(popup.Browser.IsDisposed,"popup close disposes WebView");
   }
  }
  static void Core() {
   Check(BrowserRecovery.For(CoreWebView2ProcessFailedKind.BrowserProcessExited)==RecoveryAction.Recreate,"browser exit requires recreation");
   Check(BrowserRecovery.For(CoreWebView2ProcessFailedKind.RenderProcessExited)==RecoveryAction.Reload,"renderer exit permits reload");
   Check(BrowserRecovery.For(CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)==RecoveryAction.Reload,"unresponsive renderer offers reload");
   Check(BrowserRecovery.For(CoreWebView2ProcessFailedKind.GpuProcessExited)==RecoveryAction.Ignore,"GPU auto recovery has no blocking overlay");
   Check(BrowserRecovery.For(CoreWebView2ProcessFailedKind.UtilityProcessExited)==RecoveryAction.Ignore,"utility auto recovery has no blocking overlay");
   Check(Preferences.IsStorageFailure(new UnauthorizedAccessException())&&Preferences.IsStorageFailure(new IOException())&&!Preferences.IsStorageFailure(new InvalidOperationException()),"storage exception classification");
   var settings=WindowSettings.Defaults();settings.Save();Check(WindowSettings.Load().ToggleKeys==settings.ToggleKeys,"isolated preferences round trip");
   Check(HotkeyBindings.Validate(settings.ToggleKeys,settings.ToggleKeys)!=null,"shortcut collision validation");
   Check(settings.ChatMode=="normal","new settings default to ordinary conversation");
   settings.ChatMode="translate";settings.Save();Check(WindowSettings.Load().ChatMode=="translate","chat mode persists separately from window size");
   settings.ChatMode="future-unknown";settings.Save();Check(WindowSettings.Load().ChatMode=="normal"&&WindowSettings.Load().ToggleKeys==settings.ToggleKeys,"unknown mode falls back without resetting other settings");
   settings.ChatMode=null;settings.Save();Check(WindowSettings.Load().ChatMode=="normal","legacy missing mode uses ordinary conversation");
   foreach(var definition in ChatModes.All)Export("chat-mode-"+definition.Id+".json",ChatModeInjector.Script(definition.Id));
   Export("chat-mode-copy.json",ChatModeInjector.ExpectOneShotScript("COPIED ONE SHOT"));
   Export("prompt-first.json",PromptInserter.Script("PRIOR REQUEST",null));Export("prompt-second.json",PromptInserter.Script("NEXT REQUEST","PRIOR REQUEST"));Export("focus-script.json",ComposerFocus.Script);
  }
  static void Wait(System.Threading.Tasks.Task task){
   var watch=System.Diagnostics.Stopwatch.StartNew();
   while(!task.IsCompleted&&watch.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
   if(!task.IsCompleted)throw new Exception("Isolated WebView operation timed out");
   task.GetAwaiter().GetResult();
  }
  static void BlockNetwork(Microsoft.Web.WebView2.WinForms.WebView2 view){
   view.CoreWebView2InitializationCompleted+=delegate{if(!view.IsDisposed&&view.CoreWebView2!=null)view.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs args){if(args.Uri!="about:blank")args.Cancel=true;};};
  }
  static void BrowserLifecycle(){
   using(var window=Window(false)){
    // Every newly created control blocks external navigation before host wiring.
    window.ControlAdded+=delegate(object sender,ControlEventArgs args){var view=args.Control as Microsoft.Web.WebView2.WinForms.WebView2;if(view!=null)BlockNetwork(view);};
    var before=(Microsoft.Web.WebView2.WinForms.WebView2)Field(window,"browser");BlockNetwork(before);Set(window,"initializing",false);
    Wait((System.Threading.Tasks.Task)Call(window,"InitializeBrowser"));Application.DoEvents();
    Check(before.CoreWebView2!=null,"isolated main browser initializes without website navigation");
    var environment=(CoreWebView2Environment)Field(window,"environment");
    Check(environment.UserDataFolder.StartsWith(root,StringComparison.OrdinalIgnoreCase),"fault injection owns its unique temporary profile");
    // Kill only the browser PID obtained from this isolated control/environment.
    int pid=(int)before.CoreWebView2.BrowserProcessId;
    System.Diagnostics.Process.GetProcessById(pid).Kill();
    var watch=System.Diagnostics.Stopwatch.StartNew();
    while(!(bool)Field(window,"browserExited")&&watch.ElapsedMilliseconds<10000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
    Check((bool)Field(window,"browserExited")&&(bool)Field(window,"recreateBrowser"),"real isolated browser exit schedules controller recreation");
    Wait((System.Threading.Tasks.Task)Call(window,"InitializeBrowser"));Application.DoEvents();
    var after=(Microsoft.Web.WebView2.WinForms.WebView2)Field(window,"browser");
    Check(!Object.ReferenceEquals(before,after)&&before.IsDisposed&&after.CoreWebView2!=null,"retry creates a new functioning control after real process exit");
    Check(((CoreWebView2Environment)Field(window,"environment")).UserDataFolder==environment.UserDataFolder,"recovery retains the profile path");
    ((Label)Field(window,"status")).Hide();window.HandleProcessFailure(CoreWebView2ProcessFailedKind.GpuProcessExited);
    Check(!((Label)Field(window,"status")).Visible,"GPU recovery does not introduce a blocking status layer");
    Close(window);
   }
   var create=CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"popup-cancel-profile"));Wait(create);
   using(var popup=new PopupWindow(new Uri("https://example.com"),1)){
    popup.Show();var initialization=popup.InitializeAsync(create.Result);popup.Close();Wait(initialization);
    Check(!initialization.Result&&popup.IsDisposed,"close during real WebView initialization is a quiet cancellation");
   }
  }
  static void ChatModeBrowser(){
   const string url="https://chat.deepseek.com/reflex-isolated-mode-check";
   const string html=@"<!doctype html><meta charset='utf-8'><textarea id='chat-input' placeholder='给 DeepSeek 发送消息'></textarea><button id='send'>发送</button><textarea id='edit-history'>HISTORY</textarea><script>
window.sent=[];const e=document.querySelector('#chat-input');
document.querySelector('#send').onclick=()=>{window.sent.push(e.value);e.value='';e.dispatchEvent(new Event('input',{bubbles:true}));};
e.onkeydown=event=>{if(event.key==='Enter'&&!event.shiftKey&&!event.isComposing)document.querySelector('#send').click();};
window.enter=text=>{e.value=text;e.dispatchEvent(new Event('input',{bubbles:true}));};
</script>";
   using(var shell=new Form())using(var view=new Microsoft.Web.WebView2.WinForms.WebView2{Dock=DockStyle.Fill}){
    shell.Controls.Add(view);shell.Show();
    var create=CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"chat-mode-profile"));Wait(create);Wait(view.EnsureCoreWebView2Async(create.Result));
    var core=view.CoreWebView2;core.Settings.IsWebMessageEnabled=false;core.Settings.AreHostObjectsAllowed=false;
    core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs args){if(args.Uri!=url)args.Cancel=true;};
    core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
    core.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs args){args.Response=create.Result.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(args.Request.Uri==url?html:"blocked")),args.Request.Uri==url?200:403,"OK","Content-Type: text/html; charset=utf-8\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'");};
    var navigation=new System.Threading.Tasks.TaskCompletionSource<bool>();core.NavigationCompleted+=delegate(object sender,CoreWebView2NavigationCompletedEventArgs args){navigation.TrySetResult(args.IsSuccess);};core.Navigate(url);Wait(navigation.Task);Check(navigation.Task.Result,"real WebView composer fixture loads with all external requests intercepted");
    Func<string,string> run=delegate(string script){var task=core.ExecuteScriptAsync(script);Wait(task);return task.Result;};
    Check(run(ChatModeInjector.Script("translate"))=="\"ready\"","real WebView enables persistent mode without filling an empty composer");
    run("enter('hello');document.querySelector('#send').click();enter('world');document.querySelector('#chat-input').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));");
    Check(run("sent.length===2&&sent.every(x=>x.startsWith('请翻译')&&x.split('待处理内容：').length===2)&&sent[0].endsWith('hello')&&sent[1].endsWith('world')")=="true","real WebView mouse and Enter submit consecutive messages with one prefix each");
    run("enter('keep my draft');");run(ChatModeInjector.Script("explain"));run(ChatModeInjector.Script("normal"));
    Check(run("document.querySelector('#chat-input').value==='keep my draft'&&document.querySelector('#edit-history').value==='HISTORY'")=="true","real WebView preserves material on mode switch and does not mutate history editors");
    run(ChatModeInjector.Script("translate"));run("enter('');");
    Check(run(PromptInserter.Script("ONE SHOT SELECTION",null))=="\"ambiguous\"","one-shot fill still rejects an unrelated second visible editor");
    run("document.querySelector('#edit-history').hidden=true;");
    Check(run(PromptInserter.Script("ONE SHOT SELECTION",null))=="\"filled\"","real WebView one-shot fill bypasses the persistent prefix");
    Check(run("document.querySelector('#chat-input').value==='ONE SHOT SELECTION'")=="true","real WebView one-shot request stays unwrapped");
    shell.Close();
   }
   ChatModeHost(html);
  }
  static void ChatModeHost(string html){
   using(var window=Window(false)){
    // This machine's runtime rejects the host's existing explicit background-color
    // interface. Use an otherwise equivalent default control for mode-chain QA.
    var original=(Microsoft.Web.WebView2.WinForms.WebView2)Field(window,"browser");window.Controls.Remove(original);original.Dispose();
    var view=new Microsoft.Web.WebView2.WinForms.WebView2();Set(window,"browser",view);window.Controls.Add(view);Call(window,"LayoutContent");
    var navigation=new System.Threading.Tasks.TaskCompletionSource<bool>();
    view.CoreWebView2InitializationCompleted+=delegate{
     try{
     var core=view.CoreWebView2;core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs args){if(args.Uri!="https://chat.deepseek.com/")args.Cancel=true;};
     core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
     core.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs args){args.Response=core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)),200,"OK","Content-Type: text/html; charset=utf-8\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'");};
     core.NavigationCompleted+=delegate(object sender,CoreWebView2NavigationCompletedEventArgs args){navigation.TrySetResult(args.IsSuccess);};
     }catch(Exception error){Console.WriteLine("Mode fixture initialization callback failed: "+error);throw;}
    };
    Wait((System.Threading.Tasks.Task)Call(window,"SwitchChatMode","translate"));Set(window,"initializing",false);Wait((System.Threading.Tasks.Task)Call(window,"InitializeBrowser"));
    Console.WriteLine("Mode host fixture uses the default WebView background property; explicit production white-background acceptance is not covered.");
    Check(((Health)Field(window,"health")).initialized,"production host initializes before waiting for isolated navigation");
    Wait(navigation.Task);
    Func<string,string> run=delegate(string script){var task=view.CoreWebView2.ExecuteScriptAsync(script);Wait(task);return task.Result;};
    Check(navigation.Task.Result&&run("window.__deepSeekFloatChatMode.mode")=="\"translate\"","production host restores persisted chat mode when the document loads");
    run("enter('native host material');");Wait((System.Threading.Tasks.Task)Call(window,"SwitchChatMode","explain"));
    Check(run("document.querySelector('#chat-input').value.startsWith('请面向学生')&&document.querySelector('#chat-input').value.endsWith('native host material')")=="true","production mode selector replaces the visible prefix and retains draft material");
    Wait((System.Threading.Tasks.Task)Call(window,"SwitchChatMode","translate"));
    var otherScreen=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName!=Screen.FromHandle(window.Handle).DeviceName);
    if(otherScreen!=null){
     var area=otherScreen.WorkingArea;Call(window,"ShowChatAt",WakeTarget.At(new Point(area.Left+area.Width/2,area.Top+area.Height/2)));Application.DoEvents();
     Check(Object.ReferenceEquals(view,Field(window,"browser"))&&run("document.querySelector('#chat-input').value.endsWith('native host material')&&window.__deepSeekFloatChatMode.mode==='translate'")=="true","real WebView keeps its draft and selected chat mode during an actual cross-screen wake");
    }else Console.WriteLine("SKIP WebView draft across physical screens: only one connected display");
    var bar=(Panel)Field(window,"chatModeBar");using(var bitmap=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(bitmap,bar.ClientRectangle);bitmap.Save(Path.Combine(root,"chat-mode-host-translate.png"));}
    navigation=new System.Threading.Tasks.TaskCompletionSource<bool>();view.CoreWebView2.Reload();Wait(navigation.Task);
    Check(navigation.Task.Result&&run("window.__deepSeekFloatChatMode.mode")=="\"translate\"","production host reapplies the chosen chat mode after reload");
    Wait((System.Threading.Tasks.Task)Call(window,"SwitchChatMode","normal"));run("enter('raw message');document.querySelector('#send').click();");
    Check(run("sent[0]")=="\"raw message\"","production ordinary mode sends original material without a prefix");Close(window);
   }
  }
  [STAThread] static int Main(string[] args){
   try{
    root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,root);
    Core();
    WakeTests.Core(Check);
    if(args.Contains("--wake")){Application.EnableVisualStyles();WakeTests.Desktop(Check,root);}
    else if(args.Contains("--chat-mode")){Application.EnableVisualStyles();ChatModeBrowser();}
    else if(args.Contains("--core")){Console.WriteLine("SKIP native desktop acceptance in core-only run");}
    else{Application.EnableVisualStyles();Modal(false,false);Modal(true,false);Modal(false,true);Modal(true,true);WindowStates();SaveDenied();Accessibility();DpiAndSmallGuide();BrowserLifecycle();ChatModeBrowser();WakeTests.Desktop(Check,root);}
    Console.WriteLine("TOTAL "+passed+" boundary checks; isolated fixtures only");return 0;
   }catch(Exception e){Console.WriteLine(e);return 1;}
  }
 }
}
