using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
namespace DeepSeekFloat {
internal sealed class ResizeProbeForm : Form {
 internal int LastResize;
 internal SmoothFrame Frame;
 protected override void WndProc(ref Message m){if(m.Msg==0xa1){LastResize=m.WParam.ToInt32();return;}if(m.Msg==0x231&&Frame!=null)Frame.BeginInteractiveResize();if(m.Msg==0x232&&Frame!=null)Frame.EndInteractiveResize();base.WndProc(ref m);}
}
internal static class SettingsTests {
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowLong(IntPtr handle,int index);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr handle);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr handle);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr handle,IntPtr dc);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc,int x,int y);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle,out Native.RECT rect);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern int GetBitmapBits(IntPtr bitmap,int size,byte[] bits);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process,int flag);
 static int passed;
 static void Check(bool value,string name) { if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name); }
 static int K(Keys key) { return (int)(Keys.Control|Keys.Alt|Keys.Shift|key); }
 static bool CleanStraightEdges(Form window) {
  // Read only four pixels in the empty margin of this offline test-owned HWND.
  // DrawToBitmap misses system frame painting, which bypasses client controls.
  float scale;using(var graphics=window.CreateGraphics())scale=graphics.DpiX/96f;
  int inset=(int)Math.Round(4*scale);var dc=GetWindowDC(window.Handle);
  if(dc==IntPtr.Zero)return false;
  try {return GetPixel(dc,window.Width/2,inset)==0xffffff
    &&GetPixel(dc,window.Width/2,window.Height-1-inset)==0xffffff
    &&GetPixel(dc,inset,window.Height/2)==0xffffff
    &&GetPixel(dc,window.Width-1-inset,window.Height/2)==0xffffff;
  }finally {ReleaseDC(window.Handle,dc);}
 }
 static object Field(object obj,string name) { return obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj); }
 static bool AboveWindow(IntPtr surface,IntPtr owner) {
  var previous=Native.GetWindow(owner,3);for(int i=0;i<512&&previous!=IntPtr.Zero;i++,previous=Native.GetWindow(previous,3))if(previous==surface)return true;
  return false;
 }
 static bool ModalCornersAbove(Form dialog) {
  var smooth=(SmoothFrame)Field(dialog,"smoothFrame");
  Console.WriteLine("Modal corners "+dialog.GetType().Name+": ready="+smooth.Ready+" visible="+smooth.Visible+" aligned="+smooth.Aligned+" modalTop="+((GetWindowLong(dialog.Handle,-20)&8)!=0)+" "+String.Join(";",smooth.Surfaces.Select(c=>"visible="+IsWindowVisible(c.Handle)+",enabled="+IsWindowEnabled(c.Handle)+",above="+AboveWindow(c.Handle,dialog.Handle)+",owner="+(Native.GetWindow(c.Handle,4)==dialog.Handle)+",top="+((GetWindowLong(c.Handle,-20)&8)!=0))));
  int band=GetWindowLong(dialog.Handle,-20)&8;
  return smooth.Ready&&smooth.Aligned&&smooth.UploadCount==4&&smooth.Surfaces.All(c=>IsWindowVisible(c.Handle)&&Native.GetWindow(c.Handle,4)==dialog.Handle&&(GetWindowLong(c.Handle,-20)&8)==band&&AboveWindow(c.Handle,dialog.Handle));
 }
 static object Com(object obj,string name,BindingFlags flags,params object[] args){return obj.GetType().InvokeMember(name,flags,null,obj,args);}
 static void TestShortcutIdentity(string directory) {
  var path=Path.Combine(directory,"identity with spaces.lnk");
  var other=Path.Combine(directory,"unrelated.lnk");
  object shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
  try {
   object link=Com(shell,"CreateShortcut",BindingFlags.InvokeMethod,path);
   try {
    Com(link,"TargetPath",BindingFlags.SetProperty,Assembly.GetEntryAssembly().Location);
    Com(link,"Arguments",BindingFlags.SetProperty,"--background");
    Com(link,"Description",BindingFlags.SetProperty,"identity sentinel");
    Com(link,"IconLocation",BindingFlags.SetProperty,Path.Combine(directory,"icon.ico"));
    Com(link,"Save",BindingFlags.InvokeMethod);
   }finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);}
   ShellIdentity.RegisterShortcut(path);
   Check(ShellIdentity.ShortcutId(path)==ShellIdentity.AppId,"real shortcut persists the process taskbar identity in a path with spaces");
   link=Com(shell,"CreateShortcut",BindingFlags.InvokeMethod,path);
   try{Check((string)Com(link,"Arguments",BindingFlags.GetProperty)=="--background"&&(string)Com(link,"Description",BindingFlags.GetProperty)=="identity sentinel"&&((string)Com(link,"IconLocation",BindingFlags.GetProperty)).StartsWith(Path.Combine(directory,"icon.ico")),"registering shortcut identity preserves arguments description and icon");}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);}
   link=Com(shell,"CreateShortcut",BindingFlags.InvokeMethod,other);
   try{Com(link,"TargetPath",BindingFlags.SetProperty,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"notepad.exe"));Com(link,"Save",BindingFlags.InvokeMethod);}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);}
   var before=File.ReadAllBytes(other);bool rejected=false;
   try{ShellIdentity.RegisterShortcut(other);}catch(InvalidOperationException){rejected=true;}
   Check(rejected&&before.SequenceEqual(File.ReadAllBytes(other)),"shortcut registration rejects another application's target without modifying its file");
  }finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
 }
 static string IconPixels(IntPtr icon,Size size) {
  using(var bitmap=new Bitmap(size.Width,size.Height)) {
   using(var graphics=Graphics.FromImage(bitmap)) {
    graphics.Clear(Color.Magenta);var dc=graphics.GetHdc();
    try{if(!Native.DrawIconEx(dc,0,0,icon,size.Width,size.Height,0,IntPtr.Zero,3))throw new Exception("Native icon render failed");}finally{graphics.ReleaseHdc(dc);}
   }
   var pixels=new System.Collections.Generic.List<int>();
   for(int y=0;y<size.Height;y++)for(int x=0;x<size.Width;x++)pixels.Add(bitmap.GetPixel(x,y).ToArgb()&0xffffff);
   return String.Join(",",pixels);
  }
 }
 static System.Collections.Generic.IEnumerable<Control> Children(Control parent) { foreach(Control child in parent.Controls) { yield return child;foreach(var descendant in Children(child))yield return descendant; } }
 static void PressKey(Control control,Keys keys) {typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[]{new KeyEventArgs(keys)});}
 static void TestInteractionPolish(string directory) {
  var original=WindowSettings.Defaults();original.ToggleKeys=K(Keys.F20);original.CaptureKeys=K(Keys.F21);
  using(var dialog=new SettingsDialog(original,delegate(WindowSettings next){return null;},delegate(string action){})) {
   dialog.Show();Application.DoEvents();var toggle=(HotkeyBox)Field(dialog,"toggle");var capture=(HotkeyBox)Field(dialog,"capture");var save=(PanelButton)Field(dialog,"save");var hint=(Label)Field(dialog,"hint");
   toggle.Focus();Check(toggle.Recording&&toggle.Text.Contains("请按")&&hint.Text.Contains("Esc"),"focusing a shortcut shows recording instructions without changing its binding");
   PressKey(toggle,Keys.D);Check(toggle.Invalid&&toggle.Combination==original.ToggleKeys&&hint.Text.Contains("Ctrl"),"modifierless input reports a useful error and preserves the shortcut");
   Native.PostMessage(toggle.Handle,0x100,new IntPtr((int)Keys.Escape),IntPtr.Zero);Pump(35);
   Check(dialog.Visible&&!toggle.Recording&&toggle.Combination==original.ToggleKeys&&hint.Text.Contains("取消"),"first real queued Escape cancels recording and keeps settings open");
   PressKey(toggle,(Keys)original.CaptureKeys);Check(!save.Enabled&&hint.Text.Contains("不能相同"),"duplicate shortcuts are reported inline before save");
   PressKey(toggle,(Keys)K(Keys.F22));Check(save.Enabled&&hint.Text.Contains("保存后生效")&&toggle.Text==HotkeyBindings.Format(K(Keys.F22)),"valid replacement clears duplicate feedback and remains pending until save");
   capture.Focus();Native.PostMessage(capture.Handle,0x100,new IntPtr((int)Keys.Tab),IntPtr.Zero);Pump(35);
   Check(!capture.Focused&&!capture.Recording&&capture.Combination==original.CaptureKeys,"Tab leaves shortcut recording and preserves the unedited binding");
   var choices=(PanelChoices)Field(dialog,"windowMode");choices.SelectedIndex=0;var buttons=choices.Controls.OfType<PanelButton>().ToArray();buttons[0].Focus();PressKey(buttons[0],Keys.Right);
   Check(choices.SelectedIndex==1&&buttons[1].Focused&&buttons.Count(b=>b.TabStop)==1,"segmented choices support arrow navigation and one keyboard tab stop");
   PressKey(buttons[1],Keys.End);Check(choices.SelectedIndex==2,"segmented choices support End without moving the panel geometry");
   float scale=(float)Field(dialog,"scale");var body=(Panel)Field(dialog,"body");var footer=(Panel)Field(dialog,"footer");
   var preferred=dialog.ClientSize;var sizes=new[]{new Size((int)(440*scale),(int)(644*scale)),new Size((int)(360*scale),(int)(480*scale)),new Size((int)(340*scale),(int)(390*scale))};
   foreach(var size in sizes) {
    dialog.ClientSize=size;Application.DoEvents();
    Check(dialog.ClientRectangle.Contains(footer.Bounds)&&footer.ClientRectangle.Contains(save.Bounds)&&footer.ClientRectangle.Contains(((PanelButton)Field(dialog,"cancel")).Bounds),"settings save and cancel stay visible at "+size);
    Check(!body.HorizontalScroll.Visible&&body.Width>0&&choices.Width<=body.ClientSize.Width,"settings content fits horizontally at "+size);
   }
   Check(body.VerticalScroll.Visible,"short settings viewport scrolls while the footer remains fixed");
   body.AutoScrollPosition=new Point(0,body.VerticalScroll.Maximum);Application.DoEvents();
   var help=Children(body).OfType<Button>().Single(b=>b.Text=="使用帮助");var helpRect=body.RectangleToClient(help.RectangleToScreen(help.ClientRectangle));
   Check(body.ClientRectangle.Contains(helpRect),"the last settings action remains reachable by scrolling");
   var surface=(Panel)Field(dialog,"surface");using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,surface.ClientRectangle);bitmap.Save(Path.Combine(directory,"settings-compact-preview.png"));}
   dialog.ClientSize=preferred;body.AutoScrollPosition=Point.Empty;Application.DoEvents();
   ((ChromeButton)Field(dialog,"close")).Focus();Application.DoEvents();using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,surface.ClientRectangle);bitmap.Save(Path.Combine(directory,"settings-polished-preview.png"));}
   Native.PostMessage(((ChromeButton)Field(dialog,"close")).Handle,0x100,new IntPtr((int)Keys.Escape),IntPtr.Zero);Pump(35);
   Check(!dialog.Visible&&dialog.DialogResult==DialogResult.Cancel,"Escape outside recording dismisses settings without applying edits");
  }
  foreach(float scale in new[]{1f,1.5f,2f}) {
   var area=new Rectangle(-1280,40,1280,680);var fitted=SettingsDialog.FitBounds(new Rectangle(-1400,-20,(int)(440*scale),(int)(644*scale)),area,scale);
   Check(area.Contains(fitted)&&fitted.Width>0&&fitted.Height>0,"settings fit a constrained negative-coordinate working area at DPI "+scale);
  }
  using(var window=new ChatWindow(new[]{"--background"})) {
   typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
   window.Show();Application.DoEvents();var bar=(Panel)Field(window,"selectionBar");bar.Show();var action=(PanelChoices)Field(window,"selectionAction");
   foreach(int width in new[]{window.MinimumSize.Width,window.Width,window.Width+140}) {
    window.Width=width;typeof(ChatWindow).GetMethod("LayoutContent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
    var fill=(PanelButton)Field(window,"insertSelection");var copy=(PanelButton)Field(window,"copySelection");var close=(ChromeButton)Field(window,"dismissSelection");
    Check(bar.ClientRectangle.Contains(action.Bounds)&&bar.ClientRectangle.Contains(close.Bounds)&&action.Right<=fill.Left&&fill.Right<=copy.Left&&copy.Right<=close.Left,"selection controls fit without overlaps at width "+width);
    using(var bitmap=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(bitmap,bar.ClientRectangle);bitmap.Save(Path.Combine(directory,"selection-bar-"+width+".png"));}
   }
   Check(Children(bar).OfType<ComboBox>().Count()==0&&action.Controls[1].AccessibleName=="学生解释","selection actions use the shared segmented control and keep their meaning");
   var browser=(Control)Field(window,"browser");int withBar=browser.Height;((ChromeButton)Field(window,"dismissSelection")).PerformClick();Check(!bar.Visible&&browser.Height>withBar,"dismissing the selection bar returns its space to chat");
   typeof(ChatWindow).GetField("quitting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Close();
  }
  using(var shell=new Form())using(var pin=new ChromeButton("pin","测试置顶"))using(var toggle=new PanelSwitch("测试开关")) {
   shell.Controls.Add(pin);shell.Controls.Add(toggle);pin.SetBounds(8,8,32,28);toggle.SetBounds(8,44,240,36);shell.Show();Application.DoEvents();
   typeof(Control).GetMethod("OnMouseEnter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{EventArgs.Empty});Pump(160);
   Check(((UiMotion)Field(toggle,"hoverMotion")).Value==1&&!((UiMotion)Field(toggle,"hoverMotion")).Running,"switch hover shares the button feedback and settles without a continuous timer");
   typeof(Control).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{new MouseEventArgs(MouseButtons.Left,1,10,10,0)});
   typeof(Control).GetMethod("OnMouseLeave",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{EventArgs.Empty});Pump(160);
   Check(((UiMotion)Field(toggle,"pressMotion")).Value==0&&((UiMotion)Field(toggle,"hoverMotion")).Value==0,"switch feedback clears when the pointer leaves during a press");
   pin.Active=true;pin.Active=false;pin.Active=true;Pump(160);var selected=(UiMotion)Field(pin,"choiceMotion");
   Check(pin.Active&&selected.Value==1&&!selected.Running,"rapid pin changes settle at the actual pinned state and stop animating");shell.Close();
  }
 }
 [STAThread] static void Main(string[] args) {
  try {
   ShellIdentity.InitializeProcess();Check(ShellIdentity.CurrentId==ShellIdentity.AppId,"process publishes the unique DeepSeek-Reflex taskbar identity before UI creation");
   TestShortcutIdentity(args[0]);
   Application.EnableVisualStyles();
   var root=Path.Combine(args[0],"test-preferences-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,root);
   Check(GettingStarted.NeedsIntroduction,"fresh or upgraded install offers introduction before acknowledgement");
   Preferences.Write(GettingStarted.Marker,"broken");Check(GettingStarted.NeedsIntroduction,"malformed introduction marker does not silently suppress help");
   File.Delete(Path.Combine(root,GettingStarted.Marker));Directory.CreateDirectory(Path.Combine(root,GettingStarted.Marker));
   Check(!GettingStarted.Acknowledge()&&GettingStarted.NeedsIntroduction,"unwritable introduction marker neither crashes nor pretends dismissal was saved");Directory.Delete(Path.Combine(root,GettingStarted.Marker));
   var guideSettings=WindowSettings.Defaults();guideSettings.ToggleKeys=K(Keys.F19);guideSettings.CaptureKeys=K(Keys.F18);guideSettings.HideToTrayOnToggle=true;
   using(var guide=new GettingStartedDialog(guideSettings)) {
    guide.Show();Application.DoEvents();var smooth=(SmoothFrame)Field(guide,"smoothFrame");
    Check(guide.Step==0&&smooth.Ready&&smooth.Aligned&&smooth.UploadCount==4,"introduction starts at login and uses the shared cached smooth frame");
    var next=(PanelButton)Field(guide,"next");next.PerformClick();Check(guide.Step==1&&((Label)Field(guide,"key")).Text==HotkeyBindings.Format(guideSettings.ToggleKeys)&&((Label)Field(guide,"hint")).Text.Contains("收进托盘"),"introduction shows current wake shortcut and hide preference");
    next.PerformClick();Check(guide.Step==2&&((Label)Field(guide,"key")).Text==HotkeyBindings.Format(guideSettings.CaptureKeys)&&((Label)Field(guide,"body")).Text.Contains("自己发送"),"selection guide uses actual shortcut and requires user to send draft");
    ((PanelButton)Field(guide,"previous")).PerformClick();Check(guide.Step==1,"introduction can return to the previous step");guide.SetStep(3);
    Check(next.Text=="开始使用"&&next.AccessibleName=="开始使用","final guide action is clearly named");
    var panel=guide.Controls.OfType<Panel>().Single(p=>p.Name=="guide-surface");guide.Controls.Remove(panel);panel.Font=guide.Font;panel.Size=guide.ClientSize;
    using(var bitmap=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[0],"guide-preview.png"));}guide.Controls.Add(panel);
    next.PerformClick();Check(!guide.Visible&&guide.DialogResult==DialogResult.OK&&!smooth.Visible,"finishing introduction closes all owned corner surfaces");
   }
   using(var guide=new GettingStartedDialog(guideSettings)){guide.Show();Application.DoEvents();Children(guide).OfType<Button>().Single(b=>b.Text=="跳过").PerformClick();Check(!guide.Visible&&guide.DialogResult==DialogResult.Cancel,"introduction can be skipped without traversing all steps");}
   // Test the real deferred startup path using only our own windows, never a website.
   guideSettings.Save();
   using(var window=new ChatWindow(new[]{"--background"}))using(var dismiss=new Timer {Interval=20}) {
    window.Text="Reflex 引导自有测试";typeof(ChatWindow).GetField("initializing",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,true);
    window.Show();Application.DoEvents();Check(!window.Visible&&GettingStarted.NeedsIntroduction,"background startup defers introduction and does not acknowledge it");
    bool opened=false,suspended=false,guideCorners=true;var timeout=System.Diagnostics.Stopwatch.StartNew();
    dismiss.Tick+=delegate {
     var guide=Application.OpenForms.OfType<GettingStartedDialog>().FirstOrDefault();
     if(guide!=null){opened=true;guideCorners&=ModalCornersAbove(guide);suspended=!((HotkeyBindings)Field(window,"hotkeys")).ToggleRegistered;Native.SendMessage(window.Handle,0x312,new IntPtr(Native.HotkeyId),IntPtr.Zero);Check(window.Visible&&guide.Visible,"queued hotkey cannot hide host under the modal introduction");guide.SetStep(3);((PanelButton)Field(guide,"next")).PerformClick();dismiss.Stop();}
     else if(timeout.ElapsedMilliseconds>4000){dismiss.Stop();window.Close();}
    };
    dismiss.Start();typeof(ChatWindow).GetMethod("ShowChat",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);Application.DoEvents();
    Check(opened&&suspended&&!GettingStarted.NeedsIntroduction,"first explicit wake opens guide, suspends hotkeys and persists dismissal");
    Check(guideCorners,"real modal guide keeps all four alpha corners above the dialog");
    Check(((HotkeyBindings)Field(window,"hotkeys")).ToggleRegistered,"closing introduction restores wake hotkey");
    typeof(ChatWindow).GetMethod("ShowChat",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);Application.DoEvents();Check(!Application.OpenForms.OfType<GettingStartedDialog>().Any(),"subsequent wake does not repeat acknowledged introduction");
    typeof(ChatWindow).GetField("quitting",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,true);window.Close();
   }
   using(var guide=new GettingStartedDialog(guideSettings)){Check(guide.Step==0&&!GettingStarted.NeedsIntroduction,"manual help remains available after startup acknowledgement");}
   using(var dialog=new SettingsDialog(WindowSettings.Defaults(),delegate(WindowSettings next){return null;},delegate(string action){Check(action=="help","settings help action routes to the guide");})) {
    dialog.Show();Application.DoEvents();Children(dialog).OfType<Button>().Single(b=>b.Text=="使用帮助").PerformClick();Check(!dialog.Visible,"help entry closes settings before opening another modal");
   }
   File.Delete(Path.Combine(root,"window-settings.json"));
   Check(!Preferences.AutomaticSelectionEnabled(),"fresh install never enables experimental mouse hook");
   Preferences.Write("selection-popup-disabled.json","broken");Check(!Preferences.AutomaticSelectionEnabled(),"malformed experimental preference remains disabled");
   Preferences.Write("selection-popup-disabled.json","false");Check(Preferences.AutomaticSelectionEnabled(),"explicit legacy experimental opt-in is retained");
   Preferences.Write("selection-popup-disabled.json","true");Preferences.Write("shortcut.json","true");Preferences.Write("capture-shortcut.json","false");
   Check(!Preferences.AutomaticSelectionEnabled(),"existing disabled experimental preference remains disabled");
   var defaults=WindowSettings.Load();
   Check(defaults.ToggleKeys==(int)(Keys.Control|Keys.Alt|Keys.Space)&&defaults.CaptureKeys==(int)(Keys.Control|Keys.Shift|Keys.D),"legacy shortcut migration");
   Check(defaults.FocusOnOpen&&defaults.Mode=="custom"&&!defaults.HideToTrayOnToggle,"new preferences retain current size");
   var saved=defaults.Clone();saved.ToggleKeys=K(Keys.F20);saved.CaptureKeys=K(Keys.F21);saved.Mode="reading";saved.FocusOnOpen=false;saved.HideToTrayOnToggle=true;saved.Save();
   var read=WindowSettings.Load();Check(read.ToggleKeys==saved.ToggleKeys&&read.CaptureKeys==saved.CaptureKeys&&read.Mode=="reading"&&!read.FocusOnOpen&&read.HideToTrayOnToggle,"all settings persist");
   Check(Preferences.ReadBool("selection-popup-disabled.json"),"automatic popup remains disabled");
   Preferences.Write("window-settings.json","null");Check(WindowSettings.Load().ToggleKeys==defaults.ToggleKeys,"null settings fallback");
   Preferences.Write("window-settings.json","broken");Check(WindowSettings.Load().ToggleKeys==defaults.ToggleKeys,"corrupt settings fallback");
   saved.ToggleKeys=saved.CaptureKeys;saved.Save();Check(WindowSettings.Load().ToggleKeys==defaults.ToggleKeys,"invalid persisted duplicate fallback");
   Check(HotkeyBindings.Validate(K(Keys.F20),K(Keys.F20))!=null,"reject duplicate bindings");
   Check(!HotkeyBindings.ValidKey((int)Keys.D)&&!HotkeyBindings.ValidKey((int)(Keys.Alt|Keys.F4))&&!HotkeyBindings.ValidKey((int)(Keys.Shift|Keys.F1)),"reject unsafe or modifierless keys");
   Check(HotkeyBindings.Format((int)(Keys.Control|Keys.Alt|Keys.D1))=="Ctrl+Alt+1","format digit hotkey");
   using(var host=new Form())using(var blocker=new Form())using(var bindings=new HotkeyBindings(host.Handle,K(Keys.F20),K(Keys.F21))) {
    Check(bindings.Apply(K(Keys.F20),K(Keys.F21))==null&&bindings.ToggleRegistered&&bindings.CaptureRegistered,"real Windows hotkey registration");
    Check(Native.RegisterHotKey(blocker.Handle,91,0x4007,(uint)Keys.F22),"occupy candidate binding");
    try {
     Check(bindings.Apply(K(Keys.F23),K(Keys.F22))!=null&&bindings.ToggleKeys==K(Keys.F20)&&bindings.CaptureKeys==K(Keys.F21)&&bindings.ToggleRegistered&&bindings.CaptureRegistered,"conflict rolls back both original bindings");
     Check(Native.RegisterHotKey(blocker.Handle,92,0x4007,(uint)Keys.F23),"failed transaction frees candidate toggle");Native.UnregisterHotKey(blocker.Handle,92);
     Check(bindings.Apply(K(Keys.F20),K(Keys.F20))!=null&&bindings.ToggleRegistered&&bindings.CaptureRegistered,"invalid update preserves registrations");
    }finally { Native.UnregisterHotKey(blocker.Handle,91); }
    Check(bindings.Apply(K(Keys.F21),K(Keys.F20))==null,"swap existing toggle and capture keys");
    bindings.Suspend();Check(!bindings.ToggleRegistered&&!bindings.CaptureRegistered,"dialog suspends active hotkeys");
    Check(bindings.Apply(K(Keys.F23),K(Keys.F24))==null&&!bindings.ToggleRegistered&&!bindings.CaptureRegistered,"probe new keys without activating during dialog");
    Check(bindings.Resume()==null&&bindings.ToggleRegistered&&bindings.CaptureRegistered,"resume newly accepted bindings");
   }
   var area=new Rectangle(-1920,0,1920,1080);var current=new Rectangle(-300,100,420,740);
   var compact=WindowModes.Calculate("compact",current,area,1);var reading=WindowModes.Calculate("reading",current,area,1);
   Check(compact.Size==new Size(410,616)&&area.Contains(compact),"compact geometry on negative-coordinate monitor");
   Check(reading.Size==new Size(752,720)&&area.Contains(reading),"reading preset stays below its previous width and on monitor");
   var smallArea=new Rectangle(100,100,500,600);Check(smallArea.Contains(WindowModes.Calculate("reading",current,smallArea,2)),"large DPI preset clamps to small monitor");
   Check(WindowModes.Calculate("compact",new Rectangle(0,0,780,1120),new Rectangle(0,0,3840,2160),2).Size==new Size(820,1232),"compact preset matches screenshot proportions at 200 percent");
   Check(WindowModes.Calculate("reading",new Rectangle(0,0,840,1480),new Rectangle(0,0,3840,2160),2).Size==new Size(1504,1440),"200 percent DPI geometry");
   var original=WindowSettings.Defaults();int calls=0;WindowSettings applied=null;
   using(var dialog=new SettingsDialog(original,delegate(WindowSettings next) { calls++;applied=next;return null; })) {
    var toggle=(HotkeyBox)Field(dialog,"toggle");
    typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{new KeyEventArgs((Keys)K(Keys.F20))});
    Check(toggle.Combination==K(Keys.F20)&&toggle.ReadOnly,"shortcut field records valid key combination");
    typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{new KeyEventArgs(Keys.Alt|Keys.F4)});
    Check(toggle.Combination==K(Keys.F20),"shortcut field ignores Alt F4");
    toggle.CancelCapture();
    ((PanelChoices)Field(dialog,"windowMode")).SelectedIndex=2;((PanelChoices)Field(dialog,"hideMode")).SelectedIndex=1;((PanelSwitch)Field(dialog,"focus")).Checked=false;
    // Headless render of our own controls, no desktop capture or browser inspection.
    var panel=dialog.Controls.OfType<Panel>().Single(p=>p.Name=="settings-surface");dialog.Controls.Remove(panel);panel.Font=dialog.Font;panel.Size=dialog.ClientSize;panel.PerformLayout();
    using(var bitmap=new Bitmap(panel.Width,panel.Height)) { panel.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[0],"settings-preview.png")); }
    dialog.Controls.Add(panel);
    var save=Children(dialog).OfType<Button>().Single(b=>b.Text=="保存");
    typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(save,new object[]{EventArgs.Empty});
    Check(calls==1&&applied.ToggleKeys==K(Keys.F20)&&applied.Mode=="reading"&&!applied.FocusOnOpen&&applied.HideToTrayOnToggle,"dialog save applies all selected options");
   }
   Check(original.FocusOnOpen&&original.Mode=="custom","editing dialog does not mutate original before apply");
   using(var shell=new Form())using(var button=new PanelButton("平滑按钮"))using(var toggle=new PanelSwitch("平滑开关")) {
    shell.Controls.Add(button);shell.Controls.Add(toggle);button.SetBounds(10,10,160,38);toggle.SetBounds(10,60,210,32);shell.Show();Application.DoEvents();
    var motion=(UiMotion)Field(button,"hoverMotion");typeof(Control).GetMethod("OnMouseEnter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});
    bool started=motion.Running;Pump(40);if(started)Check(motion.Value>0&&motion.Value<1,"hover uses an intermediate color rather than jumping states");else Check(motion.Value==1,"system-disabled animations apply hover instantly");
    Pump(150);Check(!motion.Running&&motion.Value==1,"hover settles and stops its timer");
    typeof(Control).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{new MouseEventArgs(MouseButtons.Left,1,20,20,0)});Pump(35);
    typeof(Control).GetMethod("OnMouseLeave",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});Pump(150);
    Check(((UiMotion)Field(button,"pressMotion")).Value==0&&!motion.Running,"leaving while pressed releases feedback without sticking");
    typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{EventArgs.Empty});Check(toggle.Checked,"switch state changes immediately for settings save");Pump(200);Check(toggle.SlidePosition==1&&!((UiMotion)Field(toggle,"slide")).Running,"switch thumb reaches its destination and timer stops");
    toggle.Checked=false;toggle.Checked=true;Pump(200);Check(toggle.SlidePosition==1,"rapid switch reversal settles at the latest value");shell.Close();
   }
   using(var dialog=new SettingsDialog(original,delegate(WindowSettings next){return null;})) {
    dialog.Show();Application.DoEvents();var outline=(WindowFrame)Field(dialog,"frame");var smooth=(SmoothFrame)Field(dialog,"smoothFrame");
    Check(smooth.Ready&&smooth.Aligned&&smooth.UploadCount==4&&WindowFrame.NativeCornersClipped(dialog),"settings use the same cached 22 DIP antialiased outer frame as chat");
    dialog.Location=new Point(dialog.Left+8,dialog.Top+6);Application.DoEvents();Check(smooth.Aligned&&smooth.UploadCount==4,"moving settings keeps the same cached corners aligned");
    dialog.Hide();Check(!smooth.Visible,"closing or hiding settings removes all corner surfaces");
   }
   using(var window=new ChatWindow(new[]{"--background"})) {
    // Keep this a self-contained layout fixture: no navigation, website DOM,
    // physical input or existing browser is involved.
    Check(window.Icon!=null&&window.Icon.Size==SystemInformation.IconSize,"window loads the native taskbar icon size from the multi-resolution fish ICO");
    var trayIcon=((NotifyIcon)Field(window,"tray")).Icon;
    Check(trayIcon!=null&&trayIcon.Size==SystemInformation.SmallIconSize,"tray loads the native small icon layer instead of resampling the large window icon");
    using(var bitmap=new Bitmap(trayIcon.Width,trayIcon.Height)) {
     using(var graphics=Graphics.FromImage(bitmap)) {
      graphics.Clear(Color.Magenta);var dc=graphics.GetHdc();
      try{Check(Native.DrawIconEx(dc,0,0,trayIcon.Handle,trayIcon.Width,trayIcon.Height,0,IntPtr.Zero,3),"Windows renders the actual tray HICON");}finally{graphics.ReleaseHdc(dc);}
     }
     Check((bitmap.GetPixel(0,0).ToArgb()&0xffffff)==0xff00ff&&(bitmap.GetPixel(bitmap.Width-1,bitmap.Height-1).ToArgb()&0xffffff)==0xff00ff,"actual tray icon rendering retains transparent corners: "+bitmap.GetPixel(0,0)+" / "+bitmap.GetPixel(bitmap.Width-1,bitmap.Height-1));
    }
    window.Text="DeepSeek 自有布局测试";
    // Modal settings resume native registration. Use fixture-only bindings so
    // the user's running application cannot cause a conflict message box.
    var fixtureSettings=(WindowSettings)Field(window,"settings");
    fixtureSettings.ToggleKeys=K(Keys.F20);fixtureSettings.CaptureKeys=K(Keys.F21);
    typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
    typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
    window.Show();Application.DoEvents();var smooth=(SmoothFrame)Field(window,"smoothFrame");
    var bigIcon=Native.SendMessage(window.Handle,0x7f,new IntPtr(1),IntPtr.Zero);
    var smallIcon=Native.SendMessage(window.Handle,0x7f,IntPtr.Zero,IntPtr.Zero);
    Check(bigIcon!=IntPtr.Zero&&IconPixels(bigIcon,window.Icon.Size)==IconPixels(window.Icon.Handle,window.Icon.Size),"actual taskbar HWND icon pixels match the selected window fish icon");
    Check(smallIcon!=IntPtr.Zero&&IconPixels(smallIcon,trayIcon.Size)==IconPixels(trayIcon.Handle,trayIcon.Size),"actual small window icon pixels match the selected tray fish icon");
    Check(smooth.Ready&&smooth.Aligned,"actual chat host has four cached corners before native sizing message");
    int style=GetWindowLong(window.Handle,-16);
    Check((style&0xa0000)==0xa0000&&(style&0xc00000)==0&&(GetWindowLong(window.Handle,-20)&0x40000)!=0,"borderless chat retains native system menu minimize and app taskbar styles without a caption");
    Check((style&0x40000)!=0,"actual chat HWND enables the native sizing frame, not only resize hit testing");
    Check(window.ClientSize==window.Size,"native sizing frame reserves no system border or caption space");
    window.Refresh();
    Check(CleanStraightEdges(window),"owned straight margins start white before native activation");
    Native.SendMessage(window.Handle,0x86,IntPtr.Zero,IntPtr.Zero);
    Check(CleanStraightEdges(window),"native deactivation does not draw a gray sizing frame over custom margins");
    Native.SendMessage(window.Handle,0x86,new IntPtr(1),IntPtr.Zero);
    Check(CleanStraightEdges(window),"native reactivation does not draw a gray sizing frame over custom margins");
    Native.SendMessage(window.Handle,0x85,new IntPtr(1),IntPtr.Zero);
    Check(CleanStraightEdges(window),"native nonclient repaint leaves all four custom straight edges intact");
    var modalBounds=window.Bounds;bool modalDisabledOwner=false,settingsCorners=true;int modalClosed=0;
    using(var closeSettings=new Timer {Interval=30}) {
     closeSettings.Tick+=delegate {
      foreach(var dialog in Application.OpenForms.OfType<SettingsDialog>().ToArray()) {
       modalDisabledOwner|=!IsWindowEnabled(window.Handle)&&Native.GetWindow(dialog.Handle,4)==window.Handle;
       settingsCorners&=ModalCornersAbove(dialog)&&((GetWindowLong(dialog.Handle,-20)&8)!=0)==window.TopMost;
       dialog.Location=new Point(dialog.Left+8,dialog.Top+6);settingsCorners&=ModalCornersAbove(dialog);modalClosed++;dialog.Close();
      }
     };
     closeSettings.Start();
     try {for(int cycle=0;cycle<3;cycle++) {
      window.TopMost=cycle%2==1;
      Check(((GetWindowLong(window.Handle,-20)&8)!=0)==window.TopMost&&smooth.Surfaces.All(c=>(GetWindowLong(c.Handle,-20)&8)==(GetWindowLong(window.Handle,-20)&8)),"actual chat pin transition preserves native owner and corner bands: cycle="+cycle);
      typeof(ChatWindow).GetMethod("ShowSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
      Application.DoEvents();
      Check(IsWindowEnabled(window.Handle)&&window.Bounds==modalBounds&&CleanStraightEdges(window),"actual modal settings return preserves geometry and white margins: cycle="+cycle);
     }}finally {closeSettings.Stop();}
    }
    Check(modalDisabledOwner&&modalClosed==3&&((HotkeyBindings)Field(window,"hotkeys")).ToggleRegistered,"real settings modal disables owner then closes and resumes shortcut binding on every cycle");
    Check(settingsCorners,"real modal settings keep all four cached alpha corners above the dialog before and after moving with pinning on and off");
    bool helpDisabledOwner=false,helpCorners=true;int helpClosed=0;
    using(var closeHelp=new Timer {Interval=30}) {
     closeHelp.Tick+=delegate {
      foreach(var guide in Application.OpenForms.OfType<GettingStartedDialog>().ToArray()) {
       helpDisabledOwner|=!IsWindowEnabled(window.Handle)&&Native.GetWindow(guide.Handle,4)==window.Handle;
       helpCorners&=ModalCornersAbove(guide)&&((GetWindowLong(guide.Handle,-20)&8)!=0)==window.TopMost;
       guide.Location=new Point(guide.Left+8,guide.Top+6);helpCorners&=ModalCornersAbove(guide);helpClosed++;guide.Close();
      }
     };
     closeHelp.Start();
     try {for(int cycle=0;cycle<3;cycle++) {
      window.TopMost=cycle%2==1;
      typeof(ChatWindow).GetMethod("ShowHelp",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
      Application.DoEvents();
      Check(IsWindowEnabled(window.Handle)&&window.Bounds==modalBounds&&CleanStraightEdges(window),"actual modal guide return preserves geometry and white margins: cycle="+cycle);
     }}finally {closeHelp.Stop();}
    }
    Check(helpDisabledOwner&&helpClosed==3&&((HotkeyBindings)Field(window,"hotkeys")).ToggleRegistered,"real guide modal disables owner then closes and resumes shortcut binding on every cycle");
    Check(helpCorners,"real modal guide keeps all four cached alpha corners above the dialog before and after moving with pinning on and off");
    bool nativeSizingStarted=false;
    EventHandler resizeStarted=delegate {nativeSizingStarted=true;};
    window.ResizeBegin+=resizeStarted;
    using(var cancelResize=new Timer {Interval=30}) {
     // Drive Windows' own sizing command on this isolated test-owned window,
     // then cancel its modal loop without loading or reading a website.
     cancelResize.Tick+=delegate {Native.PostMessage(window.Handle,0x100,new IntPtr(27),IntPtr.Zero);};
     cancelResize.Start();
     try {Native.SendMessage(window.Handle,0x112,new IntPtr(0xf002),IntPtr.Zero);}
     finally {cancelResize.Stop();window.ResizeBegin-=resizeStarted;}
    }
    Check(nativeSizingStarted&&!smooth.InteractiveResize&&smooth.Aligned,"real system sizing command enters and exits the native resize loop with cached corners aligned");
    var restoredBounds=window.Bounds;
    for(int cycle=0;cycle<2;cycle++) {
     window.TopMost=cycle==1;
     Native.SendMessage(window.Handle,0x112,new IntPtr(0xf020),IntPtr.Zero);Application.DoEvents();
     Check(window.WindowState==FormWindowState.Minimized&&!smooth.Visible,"native system command minimizes chat and hides all corner surfaces: pinned="+window.TopMost);
     Native.SendMessage(window.Handle,0x112,new IntPtr(0xf120),IntPtr.Zero);Application.DoEvents();
     Check(window.WindowState==FormWindowState.Normal&&window.Visible&&window.Bounds==restoredBounds&&smooth.Visible&&smooth.Aligned&&smooth.UploadCount==4,"native system command restores chat with aligned cached corners and original bounds: pinned="+window.TopMost+" actual="+window.Bounds+" expected="+restoredBounds+" aligned="+smooth.Aligned);
    }
    var next=new Rectangle(window.Left+6,window.Top+6,window.Width+10,window.Height-12);var rect=new Native.RECT{left=next.Left,top=next.Top,right=next.Right,bottom=next.Bottom};
    var buffer=System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.RECT)));
    try {
     System.Runtime.InteropServices.Marshal.StructureToPtr(rect,buffer,false);Native.SendMessage(window.Handle,0x231,IntPtr.Zero,IntPtr.Zero);
     Check(smooth.Ready&&smooth.Visible,"holding native chat resize retains antialiasing before any movement");
     var ownerBefore=window.Bounds;var site=(Microsoft.Web.WebView2.WinForms.WebView2)Field(window,"browser");var webBefore=site.Bounds;
     var outline=(WindowFrame)Field(window,"frame");int shapesBefore=outline.ShapeUpdates,batchesBefore=smooth.MoveBatches;
     Native.SendMessage(window.Handle,0x214,new IntPtr(8),buffer);
     Check(window.Bounds==ownerBefore&&site.Bounds==webBefore&&smooth.Aligned,"WM SIZING proposal leaves owner, website and cached corners at committed geometry");
     bool proposalsStable=true;
     for(int cycle=0;cycle<60;cycle++) {
      var proposal=new Native.RECT{left=ownerBefore.Left+cycle%3*8,top=ownerBefore.Top+cycle%4*6,right=ownerBefore.Right+cycle%3*26,bottom=ownerBefore.Bottom+cycle%4*22};
      System.Runtime.InteropServices.Marshal.StructureToPtr(proposal,buffer,false);
      Native.SendMessage(window.Handle,cycle%2==0?0x216:0x214,new IntPtr(8),buffer);
      proposalsStable&=window.Bounds==ownerBefore&&site.Bounds==webBefore&&smooth.Aligned;
     }
     Check(proposalsStable&&outline.ShapeUpdates==shapesBefore&&smooth.MoveBatches==batchesBefore&&smooth.UploadCount==4,"sixty uncommitted drag / layout proposals perform no geometry, clipping or pixel updates");
     var tooSmall=new Native.RECT{left=ownerBefore.Left,top=ownerBefore.Top,right=ownerBefore.Left+window.MinimumSize.Width-10,bottom=ownerBefore.Top+window.MinimumSize.Height-10};
     System.Runtime.InteropServices.Marshal.StructureToPtr(tooSmall,buffer,false);Native.SendMessage(window.Handle,0x214,new IntPtr(8),buffer);
     Check(window.Bounds==ownerBefore&&smooth.Aligned,"undersized drag proposal also leaves actual geometry to Windows validation");
     Check(Native.SetWindowPos(window.Handle,IntPtr.Zero,next.X,next.Y,next.Width,next.Height,0x214)&&window.Bounds==next&&smooth.Aligned,"native geometry commit resizes chat and aligns cached corners without positioning the owner again");
     var moved=new Rectangle(next.Left+12,next.Top+10,next.Width,next.Height);rect.left=moved.Left;rect.top=moved.Top;rect.right=moved.Right;rect.bottom=moved.Bottom;System.Runtime.InteropServices.Marshal.StructureToPtr(rect,buffer,false);
     Native.SendMessage(window.Handle,0x216,IntPtr.Zero,buffer);
     Check(window.Bounds==next&&smooth.Aligned,"WM MOVING preview does not move the committed chat rectangle");
     Check(Native.SetWindowPos(window.Handle,IntPtr.Zero,moved.X,moved.Y,moved.Width,moved.Height,0x214)&&window.Bounds==moved&&smooth.Aligned,"native move commit positions cached antialiased corners after the actual owner move");
     bool nativeAligned=true;for(int corner=0;corner<4;corner++){Native.RECT actual;GetWindowRect(smooth.Surfaces[corner].Handle,out actual);nativeAligned&=Rectangle.FromLTRB(actual.left,actual.top,actual.right,actual.bottom)==smooth.Surfaces[corner].Bounds;}
     Check(nativeAligned,"all four layered HWND positions match managed geometry after native resize and move commits");
     Native.SendMessage(window.Handle,0x232,IntPtr.Zero,IntPtr.Zero);
     float fixtureScale;using(var g=window.CreateGraphics())fixtureScale=g.DpiX/96f;
     Check(!smooth.InteractiveResize&&smooth.UploadCount==4&&((WindowFrame)Field(window,"frame")).NativeResizeSurfaceReady(window,fixtureScale,smooth),"native drag exit preserves cached pixels and both curved and straight hit surfaces");
     Check(site.CoreWebView2==null&&site.Source==null,"layout fixture never initialized a website session");
    }finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);window.Hide();}
   }
   using(var dialog=new SettingsDialog(original,delegate(WindowSettings next) { calls++;return "冲突测试"; })) {
    Check(dialog.FormBorderStyle==FormBorderStyle.None&&Children(dialog).OfType<ComboBox>().Count()==0&&Children(dialog).OfType<CheckBox>().Count()==0,"standalone panel uses custom choices and switch");
    typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Children(dialog).OfType<Button>().Single(b=>b.Text=="保存"),new object[]{EventArgs.Empty});
    Check(((Label)Field(dialog,"error")).Text=="冲突测试"&&dialog.DialogResult!=DialogResult.OK,"save failure remains editable with explicit error");
   }
   int before=calls;using(var dialog=new SettingsDialog(original,delegate(WindowSettings next) { calls++;return null; }))dialog.Dispose();Check(calls==before,"cancel does not apply settings");
   TestInteractionPolish(args[0]);
   using(var window=new ChatWindow(new[]{"--background"})) {
    var method=typeof(ChatWindow).GetMethod("BuildMenu",BindingFlags.Instance|BindingFlags.NonPublic);var menu=(ContextMenuStrip)method.Invoke(window,null);
    bool same=true;for(int i=0;i<30;i++)same&=Object.ReferenceEquals(menu,method.Invoke(window,null));
    Check(same&&!menu.IsDisposed&&menu.Items[menu.Items.Count-1].Text=="退出","persistent menu lifecycle and deferred exit retained");
    Check(menu.Items.Cast<ToolStripItem>().Any(i=>i.Text=="设置")&&menu.Items.Count==5,"compact tray actions and settings entry wired");Check(Object.ReferenceEquals(((NotifyIcon)Field(window,"tray")).ContextMenuStrip,menu)&&menu.AutoClose,"real tray icon owns the automatically dismissible menu");
    var button=(ChromeButton)Field(window,"mode");Check(button.AccessibleName.Length>0&&button.Text==button.AccessibleName,"size button has accessible name");
    var settingsButton=(ChromeButton)Field(window,"more");Check(settingsButton.Kind=="settings"&&settingsButton.AccessibleName=="设置","titlebar settings icon replaces dropdown trigger");
    var label=(Label)Field(window,"title");var minimize=(ChromeButton)Field(window,"minimize");var pin=(ChromeButton)Field(window,"pin");var sizeButton=(ChromeButton)Field(window,"mode");
    Check(label.Text=="DeepSeek-Reflex"&&label.TextAlign==ContentAlignment.MiddleCenter&&Math.Abs(label.Left+label.Width/2-window.Width/2)<=1,"full DeepSeek-Reflex brand is centered without Beta label");
    Check(minimize.Left<pin.Left&&pin.Left<sizeButton.Left&&sizeButton.Right<label.Left&&settingsButton.Left>label.Right,"left controls ordered minimize pin size and right settings");
    Check(window.Region!=null&&!window.Region.IsVisible(1,1)&&window.Region.IsVisible(window.Width/2,1)&&window.Region.IsVisible(window.Width/2,window.Height-2),"real window region clips corners while retaining top and bottom edges");
    var outline=(WindowFrame)Field(window,"frame");Check(!outline.Region.IsVisible(window.Width/2,window.Height/2)&&outline.Region.IsVisible(window.Width/2,1),"outline covers border only and leaves website interaction open");
        Check(WindowFrame.DefaultZoom==0.9,"default browser scale reduced one step");
    // Render only the owned toolbar; never inspect or print website content.
    var header=(Panel)Field(window,"chrome");window.Controls.Remove(header);header.BackColor=Color.White;header.PerformLayout();
    using(var image=new Bitmap(header.Width,header.Height)){header.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(args[0],"toolbar-preview.png"));}window.Controls.Add(header);
    var chrome=(Panel)Field(window,"chrome");Check(chrome.Top>0&&(window.GetChildAtPoint(new Point(window.Width/2,1))==null||window.GetChildAtPoint(new Point(window.Width/2,1)) is WindowFrame),"top resize strip is not covered by child controls");
    using(var owner=new Form())using(var bindings=new HotkeyBindings(owner.Handle,K(Keys.F20),K(Keys.F21))) {
     bindings.Apply(K(Keys.F20),K(Keys.F21));
     typeof(ChatWindow).GetField("hotkeys",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,bindings);
     var prior=(WindowSettings)Field(window,"settings");prior.ToggleKeys=K(Keys.F20);prior.CaptureKeys=K(Keys.F21);
     var next=prior.Clone();next.ToggleKeys=K(Keys.F23);next.Mode="reading";next.HideToTrayOnToggle=true;
     var apply=typeof(ChatWindow).GetMethod("ApplySettings",BindingFlags.Instance|BindingFlags.NonPublic);
     Check(apply.Invoke(window,new object[]{next})==null&&WindowSettings.Load().ToggleKeys==K(Keys.F23)&&((WindowSettings)Field(window,"settings")).HideToTrayOnToggle&&button.Active,"window applies and persists accepted settings");
     Check(window.Width>=752&&window.Height>=720,"window preset updates actual form bounds");
     var newPath=Path.Combine(root,"window-settings.json");File.Move(newPath,newPath+".save-test");Directory.CreateDirectory(newPath);
     try {
      var bad=next.Clone();bad.ToggleKeys=K(Keys.F24);bad.HideToTrayOnToggle=false;
      Check((string)apply.Invoke(window,new object[]{bad})!=null&&bindings.ToggleKeys==K(Keys.F23)&&bindings.ToggleRegistered&&((WindowSettings)Field(window,"settings")).HideToTrayOnToggle,"save I O failure restores runtime binding and prior settings");
     }finally { Directory.Delete(newPath);File.Move(newPath+".save-test",newPath); }
     Check(Preferences.ReadBool("selection-popup-disabled.json"),"window settings do not reenable popup");
    }
   }
   var hits=new[]{new Point(2,2),new Point(100,2),new Point(798,2),new Point(2,100),new Point(798,100),new Point(2,598),new Point(100,598),new Point(798,598)};
   var expected=new[]{13,12,14,10,11,16,15,17};bool all=true;
   for(int i=0;i<hits.Length;i++)all&=WindowResize.HitTest(hits[i],new Size(800,600),5,false)==expected[i];Check(all,"all eight native resize hit zones");
   Check(WindowResize.HitTest(new Point(100,20),new Size(800,600),5,false)==0&&WindowResize.HitTest(new Point(-1,20),new Size(800,600),5,false)==0&&WindowResize.HitTest(new Point(100,2),new Size(800,600),5,true)==0,"caption interior outside and maximized window do not resize");
   Check(WindowResize.CursorFor(12)==Cursors.SizeNS&&WindowResize.CursorFor(13)==Cursors.SizeNWSE&&WindowResize.CursorFor(14)==Cursors.SizeNESW&&WindowResize.CursorFor(10)==Cursors.SizeWE,"correct bidirectional and diagonal resize cursors");
   Check(WindowResize.HitTest(new Point(6,6),new Size(800,600),5,false,20)==13&&WindowResize.HitTest(new Point(794,6),new Size(800,600),5,false,20)==14,"rounded curve band keeps diagonal resizing");
   Check(WindowFrame.Radius==22,"radius reduced from 28 to 22 DIP, approximately twenty percent");
   using(var form=new Form()){form.FormBorderStyle=FormBorderStyle.None;form.ClientSize=new Size(410,616);using(var outline=new WindowFrame()){form.Controls.Add(outline);outline.UpdateShape(form,1);Check(form.Region!=null&&!form.Region.IsVisible(1,1)&&form.Region.IsVisible(WindowFrame.Radius,1),"custom 22 DIP radius applies to full window");var hostRegion=form.Region;var ringRegion=outline.Region;int updates=outline.ShapeUpdates;for(int i=0;i<30;i++)outline.UpdateShape(form,1);Check(outline.ShapeUpdates==updates&&Object.ReferenceEquals(form.Region,hostRegion)&&Object.ReferenceEquals(outline.Region,ringRegion),"unchanged layout does not allocate or reinstall native regions");form.WindowState=FormWindowState.Maximized;outline.UpdateShape(form,1);Check(form.Region==null,"maximized window restores square corners");form.WindowState=FormWindowState.Normal;outline.UpdateShape(form,1);Check(form.Region!=null&&!form.Region.IsVisible(1,1),"restored window reapplies rounded shape");}}
   var signed=WindowResize.ScreenPoint(new IntPtr(unchecked(((long)(ushort)(short)-100<<16)|(ushort)(short)-300)));Check(signed==new Point(-300,-100),"negative monitor coordinates decode correctly");
   using(var window=new ChatWindow(new[]{"--background"})) {
    var hwnd=window.Handle;var location=window.PointToScreen(new Point(window.ClientSize.Width/2,2));
    var encoded=new IntPtr(((long)(ushort)(short)location.Y<<16)|(ushort)(short)location.X);
    Check(Native.SendMessage(hwnd,0x84,IntPtr.Zero,encoded).ToInt32()==12,"real form WM NCHITTEST routes top border to vertical resize");
    var chrome=(Panel)Field(window,"chrome");location=window.PointToScreen(new Point(2,20));encoded=new IntPtr(((long)(ushort)(short)location.Y<<16)|(ushort)(short)location.X);
    Check(Native.SendMessage(chrome.Handle,0x84,IntPtr.Zero,encoded).ToInt32()==-1,"real header child forwards border hit to parent");
    var close=(ChromeButton)Field(window,"close");location=window.PointToScreen(new Point(window.Width-5,20));encoded=new IntPtr(((long)(ushort)(short)location.Y<<16)|(ushort)(short)location.X);
    Check(Native.SendMessage(close.Handle,0x84,IntPtr.Zero,encoded).ToInt32()==-1,"real titlebar button forwards overlapped right edge");
    Check(WindowFrame.NativeCornersClipped(window),"native HWND region excludes all outer rectangular corners");
    Check(WindowFrame.NativeBorderSuppressed(hwnd),"DWM nonclient rendering is disabled on the actual HWND");
    var liveFrame=(SmoothFrame)Field(window,"smoothFrame");int uploadBefore=liveFrame.UploadCount;
    Native.SendMessage(hwnd,0x231,IntPtr.Zero,IntPtr.Zero);
    Check(liveFrame.InteractiveResize&&!liveFrame.Visible&&!liveFrame.Ready,"actual ChatWindow enter-size-move activates one-surface resize path");
    window.Size=new Size(window.Width+14,window.Height+12);
    Check(liveFrame.UploadCount==uploadBefore&&WindowFrame.NativeCornersClipped(window),"live ChatWindow resize keeps rounded owner and skips alpha uploads");
    float liveScale;using(var g=window.CreateGraphics())liveScale=g.DpiX/96f;
    var liveRing=(WindowFrame)Field(window,"frame");
    Check(liveRing.Handle!=IntPtr.Zero&&liveRing.NativeResizeSurfaceReady(window,liveScale),"actual ChatWindow native corner routing stays ready during drag");
    Native.SendMessage(hwnd,0x232,IntPtr.Zero,IntPtr.Zero);
    Check(!liveFrame.InteractiveResize,"actual ChatWindow exit-size-move ends interactive state");
    int cornerPreference=1;
    Check(Native.DwmSetWindowAttribute(hwnd,33,ref cornerPreference,4)==0,"system corner style does not compete with custom radius");
    var frame=(WindowFrame)Field(window,"frame");
    float scale;using(var g=window.CreateGraphics())scale=g.DpiX/96f;
    int radius=(int)Math.Round(WindowFrame.Radius*scale),band=(int)Math.Round(WindowResize.Border*scale);
    var nativeRegion=Native.CreateRectRgn(0,0,0,0);var frameRegion=Native.CreateRectRgn(0,0,0,0);
    int[] counts=new int[4];bool covered=true,routed=true;
    try {
     Check(Native.GetWindowRgn(hwnd,nativeRegion)==3&&Native.GetWindowRgn(frame.Handle,frameRegion)==3,"actual main and interaction HWNDs both have nonrectangular regions");
     for(int corner=0;corner<4;corner++)for(int yy=0;yy<radius;yy++)for(int xx=0;xx<radius;xx++) {
      int x=(corner%2==0)?xx:window.Width-1-xx,y=(corner<2)?yy:window.Height-1-yy;
      if(!Native.PtInRegion(nativeRegion,x,y))continue;
      int hit=WindowResize.HitTest(new Point(x,y),window.ClientSize,band,false,radius);
      if(hit==0)continue;
      counts[corner]++;covered&=Native.PtInRegion(frameRegion,x,y);
      var screen=window.PointToScreen(new Point(x,y));var packed=new IntPtr(((long)(ushort)(short)screen.Y<<16)|(ushort)(short)screen.X);
      routed&=Native.SendMessage(hwnd,0x84,IntPtr.Zero,packed).ToInt32()==hit&&Native.SendMessage(frame.Handle,0x84,IntPtr.Zero,packed).ToInt32()==-1;
     }
    }finally {Native.DeleteObject(nativeRegion);Native.DeleteObject(frameRegion);}
    Console.WriteLine("Corner native pixel counts: "+String.Join(",",counts));
    Check(counts.All(n=>n>0)&&covered,"every rounded resize pixel in all four corners is owned above WebView2");
    Check(routed,"native corner hit messages forward to the owner across the whole curved band");
    Check(window.Controls.GetChildIndex(frame)==0&&Native.GetWindow(frame.Handle,3)==IntPtr.Zero,"resize interaction surface remains foremost native child");Check(frame.NativeResizeSurfaceReady(window,scale),"runtime health verifies native curved surfaces and owner routing");
    int br=window.Width-(int)Math.Round(radius*(1-1/Math.Sqrt(2.0)))-2;
    int bottom=window.Height-(int)Math.Round(radius*(1-1/Math.Sqrt(2.0)))-2;
    Check(WindowResize.HitTest(new Point(br,bottom),window.ClientSize,band,false,radius)==17&&WindowResize.CursorFor(17)==Cursors.SizeNWSE,"visible bottom right curve maps to diagonal double arrow");
    var oldCursor=Cursor.Current;
    try {Check(Native.SendMessage(hwnd,0x20,hwnd,new IntPtr(17)).ToInt32()==1&&Cursor.Current.Handle==Cursors.SizeNWSE.Handle,"owned native SETCURSOR sets diagonal resize cursor");}
    finally {Cursor.Current=oldCursor;}
   }
   using(var window=new ChatWindow(new[]{"--background","--window-diagnostics="+Path.Combine(root,"window-health.json")})){Check(!(bool)Field(window,"pageDiagnostics"),"window-only diagnostics do not inspect website content or enable DevTools");}File.WriteAllText(Path.Combine(args[0],"focus-script.json"),Serialize(ComposerFocus.Script));
   using(var menu=new TrayMenu(1)) {
    int menuCalls=0;menu.AddAction("打开 DeepSeek 小窗",delegate{menuCalls++;});menu.AddAction("取消置顶",delegate{menuCalls++;});menu.AddDivider();menu.AddAction("设置",delegate{menuCalls++;});menu.AddAction("退出",delegate{menuCalls++;});
    menu.PerformLayout();
    Check(!menu.ShowImageMargin&&!menu.ShowCheckMargin&&menu.Renderer is TrayRenderer,"custom rounded renderer without native check gutter");
    Check(menu.Region!=null&&!menu.Region.IsVisible(1,1)&&menu.Region.IsVisible(menu.Width/2,1),"popup shape is genuinely rounded");
    menu.Items[0].PerformClick();menu.Items[1].PerformClick();menu.Items[3].PerformClick();menu.Items[4].PerformClick();Check(menuCalls==4,"all four menu actions dispatch");
    bool alive=true;for(int i=0;i<50;i++){typeof(ToolStripDropDown).GetMethod("OnClosed",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.AppClicked)});alive&=!menu.IsDisposed;}Check(alive,"fifty close cycles retain reusable popup");
    var high=new TrayMenu(2);using(high){high.AddAction("打开 DeepSeek 小窗",delegate{});high.AddAction("取消置顶",delegate{});high.AddDivider();high.AddAction("设置",delegate{});high.AddAction("退出",delegate{});high.PerformLayout();Console.WriteLine("low="+menu.Size+" row="+menu.Items[0].Size+" high="+high.Size+" row="+high.Items[0].Size);Check(high.Width>menu.Width&&high.Items[0].Height==menu.Items[0].Height*2,"menu row geometry respects two scaling factors");using(var bitmap=new Bitmap(high.Width,high.Height)){high.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[0],"tray-menu-preview-high-dpi.png"));}}
    using(var bitmap=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[0],"tray-menu-preview.png"));}
   }
      // Exercise the framework's real NotifyIcon menu path, with only test-owned
   // menus and windows. No physical input or existing browser UI is operated.
   using(var menu=new TrayMenu(1))using(var icon=new NotifyIcon())using(var outside=new Form()) {
    int actions=0;menu.AddAction("Test action",delegate{actions++;});
    icon.Icon=SystemIcons.Application;icon.ContextMenuStrip=menu;icon.Visible=true;
    var show=typeof(NotifyIcon).GetMethod("ShowContextMenu",BindingFlags.Instance|BindingFlags.NonPublic);
    var processKey=typeof(ToolStripDropDown).GetMethod("ProcessDialogKey",BindingFlags.Instance|BindingFlags.NonPublic);
    Check(show!=null&&processKey!=null,"installed Framework exposes tray lifecycle and dialog-key paths");
    ToolStripDropDownCloseReason lastReason=ToolStripDropDownCloseReason.CloseCalled;
    menu.Closed+=delegate(object sender,ToolStripDropDownClosedEventArgs e){lastReason=e.CloseReason;};
    show.Invoke(icon,null);Application.DoEvents();Check(menu.Visible,"tray-bound popup really opens");Check(menu.NativeCornersRequested&&menu.Region==null&&((TrayRenderer)menu.Renderer).NativeOutline,"real tray popup requests compositor corners without hard region or double border");
    Check((bool)processKey.Invoke(menu,new object[]{Keys.Escape})&&!menu.Visible&&lastReason==ToolStripDropDownCloseReason.Keyboard,"Escape closes the visible tray popup through framework handling");
    outside.StartPosition=FormStartPosition.Manual;outside.Bounds=new Rectangle(100,100,180,100);outside.Text="DeepSeek menu regression";outside.Show();Application.DoEvents();
    show.Invoke(icon,null);Application.DoEvents();
    Native.PostMessage(outside.Handle,0x201,new IntPtr(1),new IntPtr((20<<16)|20));
    Native.PostMessage(outside.Handle,0x202,IntPtr.Zero,new IntPtr((20<<16)|20));
    for(int i=0;i<20&&menu.Visible;i++){Application.DoEvents();System.Threading.Thread.Sleep(10);}
    Console.WriteLine("Outside dismissal: visible="+menu.Visible+" reason="+lastReason);
    Check(!menu.Visible&&(lastReason==ToolStripDropDownCloseReason.AppClicked||lastReason==ToolStripDropDownCloseReason.AppFocusChange),"outside click queued to a test-owned window dismisses the real tray menu");
        show.Invoke(icon,null);Application.DoEvents();
    outside.Activate();bool activated=Native.SetForegroundWindow(outside.Handle);for(int i=0;i<30&&menu.Visible;i++){Application.DoEvents();System.Threading.Thread.Sleep(10);}
    Console.WriteLine("Focus dismissal: visible="+menu.Visible+" reason="+lastReason);
    if(activated&&Native.GetForegroundWindow()==outside.Handle)Check(!menu.Visible&&lastReason==ToolStripDropDownCloseReason.AppFocusChange,"activation of a test-owned external window closes the visible popup");else {Console.WriteLine("SKIP foreground switch: Windows did not grant test window foreground");processKey.Invoke(menu,new object[]{Keys.Escape});}show.Invoke(icon,null);Application.DoEvents();menu.Items[0].PerformClick();Application.DoEvents();
    Check(actions==1&&!menu.Visible&&lastReason==ToolStripDropDownCloseReason.ItemClicked,"selecting a menu action executes once and dismisses popup");
    bool reusable=true;
    for(int i=0;i<30;i++){show.Invoke(icon,null);Application.DoEvents();reusable&=menu.Visible;processKey.Invoke(menu,new object[]{Keys.Escape});Application.DoEvents();reusable&=!menu.Visible&&!menu.IsDisposed;}
    Check(reusable,"thirty real tray popup open and Escape close cycles stay reusable");
    outside.Close();icon.Visible=false;
   }   foreach(float scale in new[]{1f,2f})using(var image=SmoothFrame.Render(new Size((int)(410*scale),(int)(616*scale)),scale)) {
    int stroke=WindowFrame.OutlineWidth(scale),middle=image.Width/2;
    bool solid=true;for(int y=0;y<stroke;y++)solid&=image.GetPixel(middle,y).ToArgb()==WindowFrame.Outline.ToArgb();
    Check(solid&&image.GetPixel(middle,stroke).ToArgb()==Color.White.ToArgb(),"outline has one logical pixel of solid contrast with no extra band at scale "+scale);
    Check(WindowFrame.Outline.R<=180&&WindowFrame.Outline.G<=190,"outline remains visible against white at scale "+scale);
    int partial=0;for(int y=0;y<(int)(WindowFrame.Radius*scale);y++)for(int x=0;x<(int)(WindowFrame.Radius*scale);x++){int alpha=image.GetPixel(x,y).A;if(alpha>0&&alpha<255)partial++;}
    Check(partial>20,"outer curve has actual partial alpha coverage at scale "+scale);
    Check(image.GetPixel(0,0).A==0&&image.GetPixel(image.Width/2,image.Height/2).A==0&&image.GetPixel(image.Width/2,2).A==255,"outside and browser center transparent while border remains opaque at scale "+scale);
    using(var owner=new Form())using(var ring=new WindowFrame()) {
     owner.FormBorderStyle=FormBorderStyle.None;owner.AutoScaleMode=AutoScaleMode.None;owner.ClientSize=image.Size;owner.Controls.Add(ring);ring.UpdateShape(owner,scale,true);
     bool hidden=true;int boundaries=0,radius=(int)(WindowFrame.Radius*scale);
     for(int y=1;y<radius;y++)for(int x=1;x<radius;x++) {
      bool inside=owner.Region.IsVisible(x,y);
      if(inside!=owner.Region.IsVisible(x+1,y)||inside!=owner.Region.IsVisible(x,y+1)){boundaries++;hidden&=image.GetPixel(x,y).A==255;}
     }
     Check(boundaries>10&&hidden,"every binary content clipping step is hidden beneath opaque alpha frame at scale "+scale);
     bool allCornersCovered=true,patchesMatch=true;int side=SmoothFrame.PatchSize(scale);
     for(int index=0;index<4;index++)using(var patch=SmoothFrame.RenderCorner(index,scale)) {
      int partialCorner=0;for(int y=0;y<side;y++)for(int x=0;x<side;x++) {
       int xx=index%2==0?x:image.Width-side+x,yy=index<2?y:image.Height-side+y;
       var currentPixel=patch.GetPixel(x,y);var expectedPixel=image.GetPixel(xx,yy);if(currentPixel.ToArgb()!=expectedPixel.ToArgb()&&(currentPixel.A!=0||expectedPixel.A!=0)){if(patchesMatch)Console.WriteLine("Patch mismatch corner="+index+" point="+x+","+y+" got="+currentPixel+" expected="+expectedPixel);patchesMatch=false;}int alpha=currentPixel.A;if(alpha>0&&alpha<255)partialCorner++;
       bool inside=owner.Region.IsVisible(xx,yy);if(inside&&(!owner.Region.IsVisible(xx+1,yy)||!owner.Region.IsVisible(xx,yy+1)||!owner.Region.IsVisible(xx-1,yy)||!owner.Region.IsVisible(xx,yy-1))){if(alpha!=255&&allCornersCovered)Console.WriteLine("Coverage mismatch corner="+index+" point="+x+","+y+" alpha="+alpha);allCornersCovered&=alpha==255;}
      }
      Check(partialCorner>20,"cached corner "+index+" retains fractional alpha at scale "+scale);
     }
     Check(allCornersCovered&&patchesMatch,"all four native clip boundaries stay behind opaque pixels and small patches match full reference");
     // Render only the app's own shape against contrasting backgrounds.
     using(var preview=new Bitmap(radius+16,radius+16))using(var graphics=Graphics.FromImage(preview)) {
      graphics.Clear(Color.FromArgb(42,54,70));graphics.SetClip(owner.Region,System.Drawing.Drawing2D.CombineMode.Replace);graphics.FillRectangle(Brushes.White,0,0,preview.Width,preview.Height);graphics.ResetClip();graphics.DrawImageUnscaled(image,0,0);
      preview.Save(Path.Combine(args[0],"smooth-corner-"+scale+".png"));
     }
    }
    Console.WriteLine("Antialias alpha pixels scale="+scale+" count="+partial);
    var nativeImage=image.GetHbitmap(Color.FromArgb(0));
    try {
     var bits=new byte[image.Width*image.Height*4];int nativeBytesRead=GetBitmapBits(nativeImage,bits.Length,bits);int nativePartial=0;bool premultiplied=true;
     for(int i=0;i<bits.Length;i+=4){if(bits[i+3]>0&&bits[i+3]<255)nativePartial++;premultiplied&=bits[i]<=bits[i+3]&&bits[i+1]<=bits[i+3]&&bits[i+2]<=bits[i+3];}
     Check(nativeBytesRead==bits.Length&&nativePartial>20&&premultiplied,"uploaded native bitmap retains fractional alpha and premultiplied colors at scale "+scale);
    }finally{Native.DeleteObject(nativeImage);}
   }
   using(var owner=new ResizeProbeForm())using(var ring=new WindowFrame()) {
    // Match production: configure the borderless form before CreateGraphics
    // creates its first HWND, avoiding a stale style in this fixture.
    owner.FormBorderStyle=FormBorderStyle.None;owner.AutoScaleMode=AutoScaleMode.None;owner.StartPosition=FormStartPosition.Manual;
    float scale;using(var g=owner.CreateGraphics())scale=g.DpiX/96f;
    owner.Bounds=new Rectangle(200,200,(int)(410*scale),(int)(616*scale));owner.Controls.Add(ring);
    SmoothFrame smooth=null;
    try {
     smooth=new SmoothFrame(owner,scale,delegate{ring.UpdateShape(owner,scale,smooth.Ready);});owner.Resize+=delegate{ring.UpdateShape(owner,scale,smooth.Ready);};ring.UpdateShape(owner,scale);
     owner.Frame=smooth;
     owner.Show();Application.DoEvents();smooth.SyncOwner();
     Check(smooth.Ready&&smooth.Visible&&smooth.Aligned&&smooth.Surfaces.All(s=>s.Owner==owner),"actual layered surface uploads and follows host without replacing browser window");
    Check(smooth.Surfaces.All(s=>(GetWindowLong(s.Handle,-20)&0x8080000)==0x8080000)&&(GetWindowLong(owner.Handle,-20)&0x80000)==0,"four small visual corners are layered and nonactivating; owner stays opaque");
    Check(smooth.UploadCount==4&&smooth.Surfaces.All(s=>s.Size==new Size(SmoothFrame.PatchSize(scale),SmoothFrame.PatchSize(scale))),"exactly four tiny images replace full-window alpha upload");
     var center=owner.PointToScreen(new Point(owner.Width/2,owner.Height/2));var packed=new IntPtr(((long)(ushort)(short)center.Y<<16)|(ushort)(short)center.X);
     Check(Native.SendMessage(smooth.Surfaces[3].Handle,0x84,IntPtr.Zero,packed).ToInt32()==-1,"alpha surface center forwards input to original content");
     int d=(int)Math.Round(WindowFrame.Radius*scale*(1-1/Math.Sqrt(2.0)))+2;
     var corner=owner.PointToScreen(new Point(owner.Width-d,owner.Height-d));packed=new IntPtr(((long)(ushort)(short)corner.Y<<16)|(ushort)(short)corner.X);
     Check(Native.SendMessage(smooth.Surfaces[3].Handle,0x84,IntPtr.Zero,packed).ToInt32()==17,"smoothed bottom right edge keeps diagonal resize hit");
     Native.SendMessage(smooth.Surfaces[3].Handle,0xa1,new IntPtr(17),packed);
     Check(owner.LastResize==17,"alpha frame sends resize initiation to original owner instead of resizing itself");
     owner.Location=new Point(240,220);Application.DoEvents();Check(smooth.Aligned,"moving owner keeps alpha edge aligned");
     int stableUploads=smooth.UploadCount;for(int i=0;i<20;i++)smooth.SyncOwner();Check(smooth.UploadCount==stableUploads,"repeated sync without geometry changes does not upload pixels");
     Native.SendMessage(owner.Handle,0x231,IntPtr.Zero,IntPtr.Zero);Check(smooth.InteractiveResize&&smooth.Ready&&smooth.Visible,"native live resize keeps four cached antialiased corners visible");
     for(int i=0;i<30;i++){Check(Native.SetWindowPos(owner.Handle,IntPtr.Zero,240-i%3,220-i%4,(int)(410*scale)+i%7,(int)(616*scale)+i%8,0x214),"native live geometry commit accepted "+i);Application.DoEvents();}
     Check(smooth.UploadCount==stableUploads&&WindowFrame.NativeCornersClipped(owner),"thirty live drag updates preserve rounded owner without bitmap work");
     bool nativeAligned=true;for(int i=0;i<4;i++){Native.RECT rect;GetWindowRect(smooth.Surfaces[i].Handle,out rect);nativeAligned&=Rectangle.FromLTRB(rect.left,rect.top,rect.right,rect.bottom)==SmoothFrame.PatchBounds(owner.Bounds,i,SmoothFrame.PatchSize(scale));}
     Check(nativeAligned&&smooth.Aligned&&smooth.Ready&&smooth.Visible,"native HWND rectangles remain aligned with host while all corners stay antialiased");
     var heldRegion=owner.Region;int heldUploads=smooth.UploadCount;smooth.BeginInteractiveResize();Application.DoEvents();
     Check(smooth.Ready&&smooth.Visible&&smooth.UploadCount==heldUploads&&Object.ReferenceEquals(heldRegion,owner.Region),"holding mouse without movement never swaps to hard-edge clipping");
     bool cornerRoutes=true;for(int i=0;i<4;i++)cornerRoutes&=smooth.NativeCornerReady(i,new Point(i%2==0?d:owner.Width-1-d,i<2?d:owner.Height-1-d));
     Check(cornerRoutes&&Native.GetWindow(ring.Handle,3)==IntPtr.Zero,"four alpha surfaces own curved hit zones while straight ring remains foremost child");
     Native.SendMessage(owner.Handle,0x232,IntPtr.Zero,IntPtr.Zero);Application.DoEvents();
     Check(!smooth.InteractiveResize&&smooth.Ready&&smooth.Visible&&smooth.Aligned&&smooth.UploadCount==stableUploads,"ending native drag keeps antialiasing without another pixel upload");
     smooth.BeginInteractiveResize();owner.Hide();smooth.EndInteractiveResize();Check(!smooth.Visible&&!smooth.Ready&&!smooth.InteractiveResize,"hide during drag cannot resurrect the alpha frame at drag end");owner.Show();Application.DoEvents();
     for(int i=0;i<5;i++)owner.Size=new Size((int)(410*scale)+i,(int)(616*scale));
     int gdiBefore=GetGuiResources(new IntPtr(-1),0);
     var resizeTime=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<30;i++){owner.Size=new Size((int)(410*scale)+i%3,(int)(616*scale)+i%4);Application.DoEvents();}resizeTime.Stop();Console.WriteLine("Thirty resize elapsed ms="+resizeTime.ElapsedMilliseconds);
     int gdiAfter=GetGuiResources(new IntPtr(-1),0);
     Console.WriteLine("GDI objects before="+gdiBefore+" after="+gdiAfter);
     Console.WriteLine("Alpha resize ready="+smooth.Ready+" aligned="+smooth.Aligned+" host="+owner.Bounds+" error="+smooth.LastError);Check(smooth.Ready&&smooth.Aligned&&gdiAfter-gdiBefore<=4,"thirty resize uploads retain alignment and bounded GDI resources");
     for(int cycle=0;cycle<4;cycle++) {
      bool pinned=cycle%2==0;owner.TopMost=pinned;smooth.SyncOwner();Application.DoEvents();
      int band=GetWindowLong(owner.Handle,-20)&8;
      Check(smooth.Surfaces.All(s=>s.TopMost==(band!=0)&&(GetWindowLong(s.Handle,-20)&8)==band&&AboveWindow(s.Handle,owner.Handle)),"pin transition synchronizes corners to the actual owner HWND band and stacking order: cycle="+cycle);
     }
     var stale=smooth.Surfaces[0];
     Check(Native.SetWindowPos(stale.Handle,new IntPtr(-1),0,0,0,0,0x213)&&!stale.TopMost&&(GetWindowLong(stale.Handle,-20)&8)!=0&&(GetWindowLong(owner.Handle,-20)&8)==0,"fixture creates a stale corner HWND band without changing managed TopMost or the owner band");
     smooth.SyncOwner();Application.DoEvents();
     Check(smooth.Aligned&&smooth.UploadCount==stableUploads&&smooth.Surfaces.All(s=>(GetWindowLong(s.Handle,-20)&8)==0&&AboveWindow(s.Handle,owner.Handle)),"sync repairs stale native corner band without activating or reuploading pixels");
     owner.Hide();Application.DoEvents();Check(!smooth.Visible&&!smooth.Ready,"hiding owner removes alpha surface");
     owner.Show();Application.DoEvents();Check(smooth.Visible&&smooth.Ready,"showing owner restores alpha surface");
     owner.WindowState=FormWindowState.Minimized;Application.DoEvents();Check(!smooth.Visible&&!smooth.Ready,"minimize removes owned alpha surface");
     owner.WindowState=FormWindowState.Normal;Application.DoEvents();Check(smooth.Ready,"restore resumes alpha rendering");
     owner.WindowState=FormWindowState.Maximized;Application.DoEvents();Check(!smooth.Visible&&!smooth.Ready&&owner.Region==null,"maximized window uses square edges without alpha overlay");
     owner.WindowState=FormWindowState.Normal;Application.DoEvents();Check(smooth.Visible&&smooth.Ready,"unmaximize restores large smooth corners");
    }finally{if(smooth!=null)smooth.Dispose();owner.Close();}
    Check(owner.OwnedForms.Length==0,"alpha surface detaches on disposal without orphan window");
   }


   File.WriteAllText(Path.Combine(args[0],"settings-tests.txt"),"passed="+passed+"\nWindows native registration, isolated preferences, headless owned controls; no physical keyboard or website focus test.\n");
   Console.WriteLine("TOTAL "+passed);
  }catch(Exception e) { Console.WriteLine(e);Environment.ExitCode=1; }
 }
 static void Pump(int milliseconds){var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();System.Threading.Thread.Sleep(5);}}
 static string Serialize(string value) { using(var stream=new MemoryStream()) { new DataContractJsonSerializer(typeof(string)).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray()); } }
}}
