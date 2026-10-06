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
  [STAThread] static int Main(string[] args){
   try{
    root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,root);
    Core();
    if(args.Contains("--core")){Console.WriteLine("SKIP native desktop acceptance in core-only run");}
    else{Application.EnableVisualStyles();Modal(false,false);Modal(true,false);Modal(false,true);Modal(true,true);WindowStates();SaveDenied();Accessibility();DpiAndSmallGuide();BrowserLifecycle();}
    Console.WriteLine("TOTAL "+passed+" boundary checks; isolated fixtures only");return 0;
   }catch(Exception e){Console.WriteLine(e);return 1;}
  }
 }
}
