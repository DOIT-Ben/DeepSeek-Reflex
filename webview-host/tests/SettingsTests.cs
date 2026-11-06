using System;
using System.Collections.Generic;
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
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PrintWindow(IntPtr handle,IntPtr dc,uint flags);
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
  uint expected=(uint)(PanelTheme.Back.R|(PanelTheme.Back.G<<8)|(PanelTheme.Back.B<<16));
  try {return GetPixel(dc,window.Width/2,inset)==expected
    &&GetPixel(dc,window.Width/2,window.Height-1-inset)==expected
    &&GetPixel(dc,inset,window.Height/2)==expected
    &&GetPixel(dc,window.Width-1-inset,window.Height/2)==expected;
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
  WindowSettings received=null;
  using(var dialog=new SettingsDialog(original,delegate(WindowSettings next){received=next;return null;},delegate(string action){})) {
   dialog.Show();Application.DoEvents();var toggle=(HotkeyBox)Field(dialog,"toggle");var capture=(HotkeyBox)Field(dialog,"capture");var save=(PanelButton)Field(dialog,"save");var hint=(Label)Field(dialog,"hint");
   dialog.Activate();Native.SetForegroundWindow(dialog.Handle);toggle.Focus();Pump(30);
   Console.WriteLine("Shortcut entry evidence: focused="+toggle.Focused+" recording="+toggle.Recording+" text="+toggle.Text+" hint="+hint.Text);
   Check(toggle.Recording&&toggle.Text.Contains("请按")&&hint.Text.Contains("Esc"),"focusing a shortcut shows recording instructions without changing its binding");
   PressKey(toggle,Keys.D);Check(toggle.Invalid&&toggle.Combination==original.ToggleKeys&&hint.Text.Contains("Ctrl"),"modifierless input reports a useful error and preserves the shortcut");
   Native.PostMessage(toggle.Handle,0x100,new IntPtr((int)Keys.Escape),IntPtr.Zero);Pump(35);
   Check(dialog.Visible&&!toggle.Recording&&toggle.Combination==original.ToggleKeys&&hint.Text.Contains("取消"),"first real queued Escape cancels recording and keeps settings open");
   PressKey(toggle,(Keys)original.CaptureKeys);Check(!save.Enabled&&hint.Text.Contains("不能相同"),"duplicate shortcuts are reported inline before save");
   PressKey(toggle,(Keys)K(Keys.F22));Check(save.Enabled&&hint.Text.Contains("保存后生效")&&toggle.Text==HotkeyBindings.Format(K(Keys.F22)),"valid replacement clears duplicate feedback and remains pending until save");
   capture.Focus();Native.PostMessage(capture.Handle,0x100,new IntPtr((int)Keys.Tab),IntPtr.Zero);Pump(35);
   Check(!capture.Focused&&!capture.Recording&&capture.Combination==original.CaptureKeys,"Tab leaves shortcut recording and preserves the unedited binding");
   var restore=(PanelButton)Field(dialog,"restoreDefault");var contentPanel=restore.Parent;
   var restoreCenter=new Point(restore.Left+restore.Width/2,restore.Top+restore.Height/2);
   Check(contentPanel.GetChildAtPoint(restoreCenter)==restore,"restore-default button sits above the full-width hint at its center");
   restore.PerformClick();
   Check(!toggle.Recording&&toggle.Combination==(int)(Keys.Control|Keys.Space)&&capture.Combination==(int)(Keys.Control|Keys.D|Keys.Shift)&&hint.Text.Contains("已恢复默认"),"restore-default resets both bindings pending save");
   var choices=(PanelChoices)Field(dialog,"windowMode");choices.SelectedIndex=0;var buttons=choices.Controls.OfType<PanelButton>().ToArray();buttons[0].Focus();PressKey(buttons[0],Keys.Right);
   Check(choices.SelectedIndex==1&&buttons[1].Focused&&buttons.Count(b=>b.TabStop)==1,"segmented choices support arrow navigation and one keyboard tab stop");
   PressKey(buttons[1],Keys.End);Check(choices.SelectedIndex==2,"segmented choices support End without moving the panel geometry");
   Check(buttons[0].BackColor==PanelTheme.Back,"segmented choices blend into the page without per-option boxes");
   var dialogScale=(float)Field(dialog,"scale");
   Check(dialog.ClientSize.Height==(int)Math.Round(664*dialogScale),"general page auto-fits the dialog height");
   var themeChoice=(PanelChoices)Field(dialog,"themeChoice");
   Check(themeChoice.SelectedIndex==0&&themeChoice.Controls.OfType<PanelButton>().Count()==3,"theme row defaults to following the system with three choices");
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
   using(var themeDialog=new SettingsDialog(original,delegate(WindowSettings next){received=next;return null;},null)) {
    themeDialog.Show();Application.DoEvents();
    var choices2=(PanelChoices)Field(themeDialog,"themeChoice");choices2.SelectedIndex=2;var saveButton=(PanelButton)Field(themeDialog,"save");saveButton.Enabled=true;Click(saveButton);
   }
   Check(received!=null&&received.Theme=="dark","saving persists the dark theme preference");
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
    var fill=(PanelButton)Field(window,"insertSelection");var close=(ChromeButton)Field(window,"dismissSelection");
    var more=(PanelButton)Field(window,"moreSelection");var status=(Label)Field(window,"selectionStatus");
    Check(bar.ClientRectangle.Contains(action.Bounds)&&bar.ClientRectangle.Contains(close.Bounds)&&bar.ClientRectangle.Contains(status.Bounds)
     &&action.Right<=more.Left&&more.Right<=close.Left
     &&status.Right<=fill.Left&&fill.Right<=bar.ClientRectangle.Right-8
     &&action.Bottom<=status.Top&&fill.Bottom<=bar.ClientRectangle.Bottom,"selection controls fit the two-row bar without overlaps at width "+width);
    typeof(ChatWindow).GetMethod("ShowMoreCommandsMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
    var commandsMenu=(TrayMenu)Field(window,"openMenu");
    Check(commandsMenu.Items.Cast<ToolStripItem>().Any(i=>i.Text=="复制请求")&&!commandsMenu.Items.Cast<ToolStripItem>().First(i=>i.Text=="复制请求").Enabled,"copy request lives in the commands menu and stays disabled without captured text");
    commandsMenu.Close();Application.DoEvents();
    var modes=(Panel)Field(window,"chrome");var selector=(PanelButton)Field(window,"chatModeButton");
    Check(selector.Visible&&selector.Parent==modes&&modes.ClientRectangle.Contains(selector.Bounds)&&((Control)Field(window,"mode")).Right<=selector.Left&&selector.Right<=((Control)Field(window,"more")).Left&&bar.Top==modes.Bottom,"persistent mode fits inside title chrome without an extra row at width "+width);
    Check(Math.Abs((selector.Left+selector.Width/2)-modes.Width/2)<=2,"mode pill is centered on the title bar at width "+width);
    using(var bitmap=new Bitmap(modes.Width,modes.Height)){modes.DrawToBitmap(bitmap,modes.ClientRectangle);bitmap.Save(Path.Combine(directory,"chat-mode-bar-"+width+".png"));}
    using(var bitmap=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(bitmap,bar.ClientRectangle);bitmap.Save(Path.Combine(directory,"selection-bar-"+width+".png"));}
   }
   Check(Children(bar).OfType<ComboBox>().Count()==0&&action.Controls[1].AccessibleName=="学生解释","selection actions use the shared segmented control and keep their meaning");
   var browser=(Control)Field(window,"browser");int withBar=browser.Height;((ChromeButton)Field(window,"dismissSelection")).PerformClick();Check(!bar.Visible&&browser.Height>withBar,"dismissing the selection bar returns its space to chat");
   Check(((PanelButton)Field(window,"chatModeButton")).Visible&&browser.Top==((Control)Field(window,"chrome")).Bottom,"dismissing selection keeps the mode accessible and returns all space below chrome");
   foreach(float scale in new[]{1f,1.5f,2f})foreach(int logicalWidth in new[]{360,410,752}){
    window.ApplyDpi(scale,new Rectangle(window.Left,window.Top,(int)(logicalWidth*scale),(int)(616*scale)));Application.DoEvents();
    var selector=(Control)Field(window,"chatModeButton");var chrome=(Control)Field(window,"chrome");
    Check(chrome.ClientRectangle.Contains(selector.Bounds)&&((Control)Field(window,"mode")).Right<=selector.Left&&selector.Right<=((Control)Field(window,"more")).Left&&browser.Top==(int)Math.Round(44*scale),"compact title geometry recovers 38 DIP at width "+logicalWidth+" scale "+scale);
   }
   var change=typeof(ChatWindow).GetMethod("SwitchChatMode",BindingFlags.Instance|BindingFlags.NonPublic);
   ((System.Threading.Tasks.Task)change.Invoke(window,new object[]{"translate"})).GetAwaiter().GetResult();
   Check(WindowSettings.Load().ChatMode=="translate"&&((PanelButton)Field(window,"chatModeButton")).Text.StartsWith("翻译模式"),"native mode selection updates display and persisted preference before browser loading");
   Check(((PanelButton)Field(window,"chatModeButton")).Chosen,"non-normal mode marks the pill for the status dot");
   var activeModes=(Panel)Field(window,"chrome");using(var bitmap=new Bitmap(activeModes.Width,activeModes.Height)){activeModes.DrawToBitmap(bitmap,activeModes.ClientRectangle);bitmap.Save(Path.Combine(directory,"chat-mode-translate.png"));}
   using(var locked=File.Open(Path.Combine(Preferences.Root,"window-settings.json"),FileMode.Open,FileAccess.Read,FileShare.Read)){
    ((System.Threading.Tasks.Task)change.Invoke(window,new object[]{"explain"})).GetAwaiter().GetResult();Check(WindowSettings.Load().ChatMode=="translate"&&((WindowSettings)Field(window,"settings")).ChatMode=="translate"&&((Control)Field(window,"chatModeButton")).AccessibleDescription.Contains("未保存"),"denied mode save preserves mode and exposes its failure accessibly");
   }
   ((System.Threading.Tasks.Task)change.Invoke(window,new object[]{"normal"})).GetAwaiter().GetResult();
   var modeMenu=(TrayMenu)typeof(ChatWindow).GetMethod("CreateChatModeMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
   Check(modeMenu.Items.Count==8&&ChatModes.All.All(m=>modeMenu.Items.Cast<ToolStripItem>().Any(item=>item.Text.EndsWith(m.Name)))&&modeMenu.Items.Cast<ToolStripItem>().Any(item=>item.Text=="自定义提示词…"),"title menu retains all six modes plus the custom-prompts entry");
   var modeSelector=(PanelButton)Field(window,"chatModeButton");
   window.ApplyDpi(WindowDpi.Scale(window),new Rectangle(window.Left,window.Top,410,616));window.Activate();Pump(150);
   ToolStripDropDownCloseReason? closeReason=null;modeMenu.Closed+=delegate(object sender,ToolStripDropDownClosedEventArgs e){closeReason=e.CloseReason;};
   var itemFont=modeMenu.Items[0].Font;int widest=0;
   foreach(var item in modeMenu.Items.Cast<ToolStripItem>())if(!string.IsNullOrEmpty(item.Text))widest=Math.Max(widest,TextRenderer.MeasureText(item.Text,itemFont,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.Left|TextFormatFlags.NoPadding).Width);
   var menuScale=(float)Field(modeMenu,"scale");
   Func<int,int> ms=v=>Math.Max(1,(int)Math.Round(v*menuScale));
   var side=Math.Max(ms(8),(int)Math.Round(widest*0.16f));
   var expected=Math.Max(ms(56),Math.Min(ms(300),widest+side*2+ms(20)));
   Check(modeMenu.Width==expected,"mode menu width hugs the widest item plus fixed chrome, no blank column");
   window.Activate();Native.SetForegroundWindow(window.Handle);modeSelector.Focus();Application.DoEvents();
   modeMenu.Show(modeSelector,new Point(0,modeSelector.Height));var menuHandle=modeMenu.Handle;var anchorScreen=modeSelector.PointToScreen(Point.Empty);
   Check(modeMenu.Left==anchorScreen.X&&modeMenu.Top==anchorScreen.Y+modeSelector.Height,"mode menu drops with its left edge flush to the anchor button");
   using(var menuBitmap=new Bitmap(modeMenu.Width,modeMenu.Height)){modeMenu.DrawToBitmap(menuBitmap,new Rectangle(0,0,modeMenu.Width,modeMenu.Height));menuBitmap.Save(Path.Combine(directory,"mode-menu-open.png"));}
   Native.PostMessage(menuHandle,0x100,new IntPtr((int)Keys.Escape),IntPtr.Zero);Pump(80);
   Console.WriteLine("Menu Escape evidence: reason="+closeReason+" visible="+modeMenu.Visible+" selectorEnabled="+modeSelector.Enabled+" focused="+modeSelector.Focused);
   Check(closeReason==ToolStripDropDownCloseReason.Keyboard&&!modeMenu.Visible&&modeSelector.Focused,"Escape dismisses the native mode menu and returns keyboard focus");
   Check(ChatWindow.ResolveTheme("light")==UiTheme.Light&&ChatWindow.ResolveTheme("dark")==UiTheme.Dark&&((ChatWindow.ResolveTheme("auto")==UiTheme.Dark)==PanelTheme.SystemPrefersDark()),"theme preference resolution follows explicit and system values");
   PanelTheme.SetMode(UiTheme.Dark);typeof(ChatWindow).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
   var chromePanel=(Control)Field(window,"chrome");
   Check(PanelTheme.Mode==UiTheme.Dark&&chromePanel.BackColor==PanelTheme.Back&&chromePanel.BackColor!=System.Drawing.Color.White,"dark theme repaints the title chrome");
   Check(((Control)Field(window,"chatModeButton")).ForeColor==PanelTheme.Ink,"dark repaint recolors the mode pill text");
   var themeSettings=(WindowSettings)Field(window,"settings");var originalPreference=themeSettings.Theme;var sync=typeof(ChatWindow).GetMethod("UpdateThemeSubscription",BindingFlags.Instance|BindingFlags.NonPublic);
   themeSettings.Theme="dark";sync.Invoke(window,null);
   Check(!(bool)Field(window,"themeHooked"),"fixed theme does not watch the system preference");
   themeSettings.Theme="auto";sync.Invoke(window,null);
   Check((bool)Field(window,"themeHooked"),"auto theme watches the system preference");
   themeSettings.Theme=originalPreference;sync.Invoke(window,null);
   using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(0,0,window.Width,window.Height));bitmap.Save(Path.Combine(directory,"dark-titlebar.png"));}
   PanelTheme.SetMode(UiTheme.Light);typeof(ChatWindow).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
   Check(chromePanel.BackColor==System.Drawing.Color.White&&PanelTheme.Mode==UiTheme.Light,"light theme restores the white chrome");
   typeof(ChatWindow).GetMethod("ShowChatModeMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
   var liveMenu=(TrayMenu)Field(window,"openMenu");
   Check(liveMenu!=null&&liveMenu.Visible&&!liveMenu.IsDisposed,"the live mode menu registers for outside dismissal");
   window.CloseOpenMenus();Application.DoEvents();
   Check(liveMenu.IsDisposed||!liveMenu.Visible&&Field(window,"openMenu")==null,"deactivating the window dismisses the open mode menu");
   typeof(ChatWindow).GetMethod("ShowChatModeMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
   var pill=(Control)Field(window,"chatModeButton");
   Cursor.Position=window.PointToScreen(new Point(window.Width/2,Math.Max(0,window.Height-40)));
   var awaySwallow=window.CloseOpenMenuForClick();Application.DoEvents();
   Check(!awaySwallow&&Field(window,"openMenu")==null,"a click elsewhere inside the window dismisses the mode menu and lets the press through");
   var pillState=typeof(ChatWindow).GetField("pillSuppressTick",BindingFlags.Instance|BindingFlags.NonPublic);
   pillState.SetValue(window,Environment.TickCount);
   typeof(ChatWindow).GetMethod("ShowChatModeMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
   Check(Field(window,"openMenu")==null,"a pill press observed by the hook suppresses immediate reopen");
   pillState.SetValue(window,0);
   typeof(ChatWindow).GetMethod("ShowChatModeMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
   Cursor.Position=pill.PointToScreen(new Point(pill.Width/2,pill.Height/2));
   var pillSwallow=window.CloseOpenMenuForClick();Application.DoEvents();
   Check(pillSwallow&&Field(window,"openMenu")==null,"pressing the mode pill itself closes the menu without immediately reopening it");
   Cursor.Position=window.PointToScreen(new Point(window.Width/2,window.Height/2));
   typeof(ChatWindow).GetMethod("ShowChatModeMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);Application.DoEvents();
   var hookMenu=(TrayMenu)Field(window,"openMenu");
   Check(hookMenu!=null&&hookMenu.Visible,"the mode menu arms the low-level mouse hook");
   var click=new Native.INPUT[2];
   click[0]=new Native.INPUT{type=0,data=new Native.INPUTUNION{mouse=new Native.MOUSEINPUT{flags=0x2}}}; // LEFTDOWN
   click[1]=new Native.INPUT{type=0,data=new Native.INPUTUNION{mouse=new Native.MOUSEINPUT{flags=0x4}}}; // LEFTUP
   Check(Native.SendInput((uint)click.Length,click,System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.INPUT)))==2,"synthetic physical click is delivered");
   Pump(160);
   Check(Field(window,"openMenu")==null||!((TrayMenu)Field(window,"openMenu")).Visible,"a physical click beside the menu dismisses it even where the page host eats the message");
   Cursor.Position=window.PointToScreen(new Point(window.Width/2,window.Height/2));
   using(var probeForm=new Form{TopMost=true}){probeForm.Show();Application.DoEvents();
    Check(Native.IsTopmostWindow(probeForm.Handle)&&!Native.IsTopmostWindow(window.Handle),"topmost probe reads the real ex-style");}
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
 static void TestEdgeDockAndCommands(string directory) {
  GettingStarted.Acknowledge();
  var root=(string)typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  // N05 catalog persistence and validation
  var fresh=PromptCatalog.Load();
  Check(fresh.Commands.Length==3&&fresh.Error==null&&fresh.Commands.All(c=>!c.Custom),"missing commands file loads builtin prompts only");
  var custom=new PromptCommand{Id=PromptCatalog.NewId(),Name="提炼要点",Instruction="请提炼要点。",BuiltinIndex=-1};
  Check(PromptCatalog.Save(new[]{custom})==null,"valid catalog saves");
  var loaded=PromptCatalog.Load();
  Check(loaded.Commands.Length==4&&!loaded.Commands[0].Custom&&loaded.Commands[3].Custom&&loaded.Commands[3].Name=="提炼要点","saved catalog round trips with builtins first");
  Check(PromptCatalog.ValidateName("翻译",loaded.Commands,null)!=null,"custom names may not collide with builtins");
  Check(PromptCatalog.ValidateName("提炼要点",loaded.Commands,loaded.Commands[3])==null,"editing keeps the entry's own name allowed");
  Check(PromptCatalog.ValidateName(new string('名',17),loaded.Commands,null)!=null&&PromptCatalog.ValidateName("  ",loaded.Commands,null)!=null,"name length and blank rules apply");
  Check(PromptCatalog.ValidateInstruction(new string('字',2000))==null&&PromptCatalog.ValidateInstruction(new string('字',2001))!=null&&PromptCatalog.ValidateInstruction("  ")!=null,"instruction limits enforced");
  var tooMany=new List<PromptCommand>();for(int i=0;i<21;i++)tooMany.Add(new PromptCommand{Id="x"+i,Name="条目"+i,Instruction="内容",BuiltinIndex=-1});
  Check(PromptCatalog.Save(tooMany)!=null,"more than twenty custom prompts rejected");
  var catalogPath=Path.Combine(root,"commands.json");
  var good=File.ReadAllText(catalogPath);
  File.Move(catalogPath,catalogPath+".keep");Directory.CreateDirectory(catalogPath);
  Check(PromptCatalog.Save(new[]{custom})!=null,"failed catalog write reports instead of throwing");
  Directory.Delete(catalogPath);File.Move(catalogPath+".keep",catalogPath);
  File.WriteAllText(catalogPath,"{not json");
  var bad=PromptCatalog.Load();
  Check(bad.Error!=null&&bad.Commands.Length==3&&File.ReadAllText(catalogPath)=="{not json","unreadable catalog falls back to builtins and keeps the file");
  File.WriteAllText(catalogPath,"{\"Version\":9,\"Commands\":[]}");
  Check(PromptCatalog.Load().Error!=null,"unknown catalog version is reported");
  // N06 compose envelope
  var material="带\"引号\"的材料";
  var customPrompt=PromptInserter.ComposeInstruction("请总结。",material);
  Check(customPrompt.StartsWith("请总结。")&&customPrompt.Contains("<选中文字>\n"+material+"\n</选中文字>"),"custom instructions use the shared quoted material envelope");
  Check(PromptInserter.Compose(material,0).Contains(ChatModes.Find("translate").Instruction)&&PromptInserter.Compose(material,1).Contains(ChatModes.Find("explain").Instruction)&&PromptInserter.Compose(material,2).Contains(ChatModes.Find("question").Instruction),"selection actions reuse the persistent mode instructions verbatim behind the directive");
  // BR bridge reply contract
  var bridgeId="d1f39a2b-1c66-4de5-9d94-30db2b58a701";
  Check(new BridgeRequest{id=bridgeId,mode="polish",text="内容"}.Valid&&!new BridgeRequest{id=bridgeId,mode="polish",text="内容"}.IsReply,"stage request stays valid without an op");
  var replyRequest=new BridgeRequest{id=bridgeId,op="reply"};
  Check(replyRequest.Valid&&replyRequest.IsReply,"reply request is valid without text");
  Check(!new BridgeRequest{id=bridgeId,op="reply",text="x"}.Valid,"reply request rejects payload text");
  Check(!new BridgeRequest{id=bridgeId,op="quux"}.Valid&&!new BridgeRequest{id=bridgeId}.Valid,"unknown op and op-less request without mode are rejected");
  var withText=Encoding.UTF8.GetString(ReflexBridge.Json(new BridgeReply(bridgeId,"ok","回复正文")));
  Check(withText.Contains("\"text\":\"回复正文\""),"reply json carries the payload text");
  Check(!Encoding.UTF8.GetString(ReflexBridge.Json(new BridgeReply(bridgeId,"loading"))).Contains("text"),"stage reply omits the text member");
  var payload=ReplyReader.Parse(Encoding.UTF8.GetString(ReflexBridge.Json(new ReplyPayload{Status="ok",Text="正文"})));
  Check(payload.Status=="ok"&&payload.Text=="正文","reply payload parses through the named members");
  Check(ReplyReader.Parse("not json").Status=="unsupported"&&ReplyReader.Parse("{}").Status=="unsupported","malformed page payload degrades to unsupported");
  // N01 dock geometry
  var area=new Rectangle(0,0,2560,1300);
  Check(DockGeometry.Hit(new Rectangle(4,100,410,616),area,12,DockSide.None)==DockSide.Left,"release within threshold docks left");
  Check(DockGeometry.Hit(new Rectangle(13,100,410,616),area,12,DockSide.None)==DockSide.None,"outside threshold never docks");
  Check(DockGeometry.Hit(new Rectangle(2160,100,410,616),area,12,DockSide.None)==DockSide.Right&&DockGeometry.Hit(new Rectangle(2900,100,410,616),area,12,DockSide.None)==DockSide.None,"right edge docks and far offscreen does not");
  Check(DockGeometry.Hit(new Rectangle(0,100,2548,616),area,12,DockSide.None)==DockSide.Right&&DockGeometry.Hit(new Rectangle(0,100,2548,616),area,12,DockSide.Left)==DockSide.Left,"edge ties keep the previous side or default right");
  Check(DockGeometry.Hit(new Rectangle(0,1400,410,616),area,12,DockSide.None)==DockSide.None,"windows outside the vertical span never dock");
  Check(!DockGeometry.HasSeam(area,DockSide.Left,new Rectangle(-16,600,32,80),new Screen[0]),"no neighbouring screens means no seam");
  var tabLeft=DockGeometry.TabBounds(DockSide.Left,area,650,1f);
  Check(tabLeft.X==area.Left&&tabLeft.Width==32&&tabLeft.Height==80&&tabLeft.Y==610,"left tab hugs the working area edge centered on the window");
  var tabRight=DockGeometry.TabBounds(DockSide.Right,area,100000,1f);
  Check(tabRight.Right==area.Right&&tabRight.Bottom==area.Bottom,"right tab clamps into the working area");
  // N02/N03 dock lifecycle on the real chat window
  using(var window=new ChatWindow(new[]{"--background"})) {
   typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
   window.Show();Application.DoEvents();
   var settings=(WindowSettings)Field(window,"settings");
   var controller=(EdgeDockController)Field(window,"edgeDock");
   var workArea=Screen.FromControl(window).WorkingArea;
   var rect=new Rectangle(workArea.Left+2,workArea.Top+40,Math.Max(window.MinimumSize.Width+20,Math.Min(410,workArea.Width-40)),Math.Max(window.MinimumSize.Height+20,Math.Min(616,workArea.Height-80)));
   window.Bounds=rect;window.SaveBounds();Application.DoEvents();
   var before=new Rectangle(workArea.Left+102,rect.Y,rect.Width,rect.Height);
   controller.OnDragEnded(before);
   Check(!controller.Docked,"edge dock disabled by default does not dock");
   settings.EdgeDockEnabled=true;
   controller.OnDragEnded(before);
   Check(controller.Docked&&!window.Visible,"releasing at the edge docks and hides the chat");
   Check(controller.Side==DockSide.Left,"docked side is recorded for tab direction and display changes");
   var dockTab=controller.Tab;
   Check(dockTab!=null&&!dockTab.IsDisposed&&dockTab.Visible&&!dockTab.ShowInTaskbar,"dock tab shows without a taskbar presence");
   Check(dockTab.Bounds.X==workArea.Left&&dockTab.Bounds.Width>=32,"left tab hugs the working area edge");
   Check(!dockTab.Focused,"dock tab does not take keyboard focus");
   var stored=File.ReadAllText(Path.Combine(root,"webview-bounds.txt")).Split(',').Select(int.Parse).ToArray();
   Check(stored[0]!=dockTab.Bounds.X||stored[2]!=dockTab.Bounds.Width,"tab rectangle never reaches saved bounds");
   window.WakeFromDock();Application.DoEvents();
   Check(!controller.Docked&&window.Visible&&window.Bounds==rect,"tab activation restores the saved rectangle");
   Check(controller.Tab==null||controller.Tab.IsDisposed||!controller.Tab.Visible,"restoring dismisses the tab");
   Check(controller.Dock(window.Bounds)&&!window.Visible,"second dock after restore");
   Com(window,"ToggleWindow",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);Application.DoEvents();
   Check(!controller.Docked&&window.Visible,"docked wake expands instead of toggling closed");
   window.Bounds=rect;Application.DoEvents();
   controller.OnDragEnded(new Rectangle(workArea.Left,rect.Y,rect.Width,rect.Height));
   Check(!controller.Docked,"a release already inside the band is not a dock gesture");
   settings.EdgeDockEnabled=false;
   controller.OnDragEnded(new Rectangle(workArea.Left,rect.Y,rect.Width,rect.Height));
   Check(!controller.Docked,"disabled setting ignores drag ends");
   settings.EdgeDockEnabled=true;
   typeof(ChatWindow).GetField("settingsOpen",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
   controller.OnDragEnded(new Rectangle(workArea.Left,rect.Y,rect.Width,rect.Height));
   Check(!controller.Docked,"modal state blocks docking");
   typeof(ChatWindow).GetField("settingsOpen",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,false);
   Check(controller.Dock(rect)&&!window.Visible,"dock before shutdown");
   controller.Restore();controller.Restore();
   Check(!controller.Docked&&window.Bounds==rect,"restore is idempotent");
   var rightRect=new Rectangle(workArea.Right-rect.Width-2,rect.Y,rect.Width,rect.Height);
   Check(controller.Dock(rightRect)&&controller.Side==DockSide.Right,"right edge dock records the right side");
   controller.Restore();
  }
  // N04 custom selection state and composition
  using(var window=new ChatWindow(new[]{"--background"})) {
   typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
   window.Show();Application.DoEvents();
   File.WriteAllText(Path.Combine(root,"commands.json"),"{\"Version\":1,\"Commands\":[{\"Id\":\"c1\",\"Name\":\"划重点\",\"Instruction\":\"请划出重点。\"}]}");
   Com(window,"ReloadCommands",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);
   var customs=(PromptCommand[])Field(window,"customCommands");
   Check(customs.Length==1&&customs[0].Name=="划重点","window reloads the custom command catalog");
   var more=(PanelButton)Field(window,"moreSelection");
   typeof(ChatWindow).GetField("customChoice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,customs[0]);
   Com(window,"RefreshSelectionChoice",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);
   Check(more.Text=="划重点"&&more.DropDown&&more.Chosen&&((PanelChoices)Field(window,"selectionAction")).SelectedIndex==-1,"choosing a custom command moves the chosen state to the more button");
   Check(more.AccessibleName=="自定义指令 划重点"&&((Label)Field(window,"selectionStatus")).Text.Contains("划重点"),"chosen command exposes its full name and a fill hint");
   typeof(ChatWindow).GetField("selectedText",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,"演示材料");
   var composeSelector=typeof(ChatWindow).GetMethod("ComposeSelection",BindingFlags.Instance|BindingFlags.NonPublic);
   var composed=(string)composeSelector.Invoke(window,null);
   Check(composed.StartsWith("请划出重点。")&&composed.Contains("<选中文字>"),"custom selection composes through the shared envelope");
   typeof(ChatWindow).GetField("customChoice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,null);
   Com(window,"RefreshSelectionChoice",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);
   composed=(string)composeSelector.Invoke(window,null);
   Check(composed.StartsWith("【当前模式：翻译模式】")&&composed.Contains(ChatModes.Find("translate").Instruction),"clearing the custom choice returns to the builtin translation behind the directive");
   typeof(ChatWindow).GetField("selectedText",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,null);
   Com(window,"RefreshSelectionChoice",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);
   Check(more.AccessibleName=="更多取词指令"&&((Label)Field(window,"selectionStatus")).Text.Length==0,"clearing the custom choice restores the default name and hint");
  }
  // commands management page and dock preference
  var baseSettings=WindowSettings.Defaults();baseSettings.ToggleKeys=K(Keys.F19);baseSettings.CaptureKeys=K(Keys.F18);
  int applied=0;WindowSettings received=null;
  using(var dialog=new SettingsDialog(baseSettings,delegate(WindowSettings next){applied++;received=next;return null;},null,"commands")) {
   dialog.Show();Application.DoEvents();
   Check(((Label)Field(dialog,"heading")).Text=="快捷指令"&&((Panel)Field(dialog,"commandsPage")).Visible,"settings opens the commands page on request");
   var nameBox=(TextBox)Field(dialog,"commandName");var bodyBox=(TextBox)Field(dialog,"commandBody");
   // The modes page carries its own identically-labelled entry button while
   // hidden, so visible-page scoping keeps the text selector unambiguous.
   Func<Button> saveEntryButton=delegate(){return Children(dialog).OfType<Button>().Single(b=>b.Text=="保存到列表"&&b.Visible);};
   Click(saveEntryButton());
   Check(((Label)Field(dialog,"nameError")).Text.Length>0,"empty command name reports next to the field");
   nameBox.Text="今日待办";bodyBox.Text="请整理成待办清单。";
   Click(saveEntryButton());
   Check(((Label)Field(dialog,"nameError")).Text==""&&((List<PromptCommand>)Field(dialog,"draft")).Count==2,"valid entry joins the draft without touching the file");
   Check(!File.ReadAllText(Path.Combine(root,"commands.json")).Contains("今日待办"),"draft entry stays out of the file until saved");
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="返回"));
   Check((bool)Field(dialog,"confirming")&&dialog.Visible,"unsaved command edits ask before leaving");
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="继续编辑"));
   Check(!(bool)Field(dialog,"confirming")&&dialog.Visible,"继续编辑 keeps editing");
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="保存指令"));
   Check(File.ReadAllText(Path.Combine(root,"commands.json")).Contains("今日待办")&&((Label)Field(dialog,"heading")).Text=="小窗设置","saving commands persists the draft and returns to the general page");
   Check(applied==0,"saving commands never applies general settings");
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="快捷指令"));
   Check(((Panel)Field(dialog,"commandsPage")).Visible&&((Label)Field(dialog,"heading")).Text=="快捷指令"&&dialog.Visible,"settings actions row opens command management in place");
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="返回"));
   Check(((Label)Field(dialog,"heading")).Text=="小窗设置"&&dialog.Visible,"returning from commands keeps the settings window open");
   // A DPI change rescales every body child before the modes page has ever
   // been opened; the mode-list render used to throw there mid-layout,
   // which is exactly how controls go missing on screen.
   typeof(SettingsDialog).GetMethod("ApplyDpi",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,new object[]{1.5f,new Rectangle(dialog.Location,new Size((int)(dialog.Width*1.5),(int)(dialog.Height*1.5)))});
   typeof(SettingsDialog).GetMethod("ShowModesPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,new object[]{true});Application.DoEvents();
   var addMode=(PanelButton)Field(dialog,"addMode");var page=(Panel)Field(dialog,"modesPage");
   Check(addMode.Visible&&addMode.Bottom+18<=page.ClientSize.Height,"a DPI change before the first modes visit never throws and keeps the action row inside the page");
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="返回"));
   var dockSwitch=(PanelSwitch)Field(dialog,"dock");dockSwitch.Checked=true;
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="保存"));
   Check(applied==1&&received!=null&&received.EdgeDockEnabled,"general save applies the edge dock preference");
  }
  using(var dialog=new SettingsDialog(baseSettings,delegate(WindowSettings next){applied++;return null;},null,"commands")) {
   dialog.Show();Application.DoEvents();
   ((TextBox)Field(dialog,"commandName")).Text="未保存";
   ((TextBox)Field(dialog,"commandBody")).Text="内容";
   Click(Children(dialog).OfType<ChromeButton>().Single(b=>b.Kind=="close"));
   Check((bool)Field(dialog,"confirming")&&dialog.Visible,"closing with unsaved command edits asks first");
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="放弃并返回"));
   Check(!dialog.Visible&&dialog.DialogResult==DialogResult.Cancel,"discarding unsaved edits from close leaves the dialog");
  }
  TestFeedback(directory);
  TestFeedbackLayout(directory);
 }
 static void TestFeedback(string directory) {
  // FB feedback dialog contract; the poster is always isolated from the network.
  string captured=null;
  using(var dialog=new FeedbackDialog(json=>{captured=json;return null;})) {
   dialog.Show();Application.DoEvents();
   System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
   var text=(TextBox)Field(dialog,"body");var send=(PanelButton)Field(dialog,"send");
   var contactBox=(TextBox)Field(dialog,"contact");var chips=(PanelButton[])Field(dialog,"chips");
   var hint=(Label)Field(dialog,"bodyHint");
   Check(hint.Parent.GetChildAtPoint(new Point(hint.Left+2,hint.Top+2))==hint,"empty feedback placeholder is above the native edit control");
   var contactPlaceholder=(Label)Field(dialog,"contactHint");
   Check(contactPlaceholder.Parent.GetChildAtPoint(new Point(contactPlaceholder.Left+2,contactPlaceholder.Top+2))==contactPlaceholder,"empty contact placeholder is above the native edit control");
   Check(chips[0].Chosen&&chips.Count(chip=>chip.Chosen)==1,"feedback has one clear default category");
   Click(chips[0]);Check(chips[0].Chosen,"clicking selected category keeps a selection");
   Check(((Label)Field(dialog,"bodyLabel")).Visible&&((Label)Field(dialog,"contactLabel")).Visible,"feedback field labels remain visible outside the placeholders");
   text.Focus();Check(((InputPanel)Field(dialog,"bodyFrame")).Accent,"feedback body focus lights its shared input outline");
   contactBox.Focus();Check(((InputPanel)Field(dialog,"contactFrame")).Accent&&!((InputPanel)Field(dialog,"bodyFrame")).Accent,"contact focus moves the outline to exactly one field");
   Check(!send.Enabled&&((Label)Field(dialog,"bodyHint")).Visible&&((Label)Field(dialog,"counter")).Text=="0 / 1000","empty feedback explains its content and length limit without enabling send");
   text.Text="希望支持深色模式";
   Check(send.Enabled&&!((Label)Field(dialog,"bodyHint")).Visible&&((Label)Field(dialog,"counter")).Text==text.Text.Length+" / 1000","typing replaces the placeholder and updates the visible counter");
   text.Text=new string('很',900);
   Check(((Label)Field(dialog,"counter")).Text.Contains("1000"),"counter appears near the limit");
   text.Text="希望支持深色模式";
   Click(chips[1]);contactBox.Text="QQ 12345678";
   Check(chips[1].Chosen&&!chips[0].Chosen&&!chips[2].Chosen,"category chip selects alone");
   using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"feedback-dialog-preview.png"));}
   Click(send);
   for(int i=0;i<80&&!((Label)Field(dialog,"thanksText")).Visible&&dialog.DialogResult==DialogResult.None;i++){Application.DoEvents();System.Threading.Thread.Sleep(10);}
   Check(captured!=null&&captured.Contains("希望支持深色模式")&&captured.Contains("\"category\":\"建议\"")&&captured.Contains("\"contact\":\"QQ 12345678\"")&&captured.Contains("\"version\"")&&captured.Contains("\"app\":\"deepseek-reflex\""),"feedback posts app, version, text, category and contact");
   Check(((Label)Field(dialog,"thanksText")).Visible&&send.Visible==false&&!((Label)Field(dialog,"status")).Visible&&!((Label)Field(dialog,"keyboardHint")).Visible,"successful send shows only the thank-you state");
   Check(Preferences.ReadText("feedback-contact")=="QQ 12345678","a successful send remembers the contact for next time");
   using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(directory,"feedback-thanks-preview.png"));}
   // The suite's earlier recording tests leave message filters that swallow
   // WM_TIMER under DoEvents, so drive the elapsed tick directly instead of
   // waiting for wall-clock delivery here.
   typeof(Timer).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke((Timer)Field(dialog,"thanksTimer"),new object[]{EventArgs.Empty});
   Application.DoEvents();
   Check(dialog.DialogResult==DialogResult.OK&&!dialog.Visible,"thank-you state closes the dialog when the timer elapses");
  }
  using(var again=new FeedbackDialog(json=>null)) {
   Check(((TextBox)Field(again,"contact")).Text=="QQ 12345678","the contact prefills from the last successful send");
  }
  Preferences.Write("feedback-contact","");
  int posts=0;
  using(var release=new System.Threading.ManualResetEventSlim(false))using(var pending=new FeedbackDialog(json=>{System.Threading.Interlocked.Increment(ref posts);release.Wait(3000);return "隔离发送失败";})) {
   pending.Show();Application.DoEvents();System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var text=(TextBox)Field(pending,"body");var submit=(PanelButton)Field(pending,"send");text.Text="进行中的反馈";Click(submit);
   try {
    Check(text.ReadOnly&&((TextBox)Field(pending,"contact")).ReadOnly&&!submit.Enabled&&((PanelButton[])Field(pending,"chips")).All(chip=>!chip.Enabled),"sending freezes the submitted fields and categories");
    text.Text="发送期间程序化修改";Click(submit);Pump(30);Check(!submit.Enabled&&posts==1,"text changes while sending cannot enable a duplicate submission");
   }finally{release.Set();}
   for(int i=0;i<100&&!((LinkLabel)Field(pending,"issueLink")).Visible;i++)Pump(10);
   Check(!text.ReadOnly&&submit.Enabled&&text.Text=="发送期间程序化修改","failed submission restores editing and preserves the draft");
   var failure=(Label)Field(pending,"status");var link=(LinkLabel)Field(pending,"issueLink");
   Check(failure.Visible&&link.Visible&&failure.Bottom<=link.Top&&link.Bottom<=submit.Top,"expanded failure message and fallback link do not overlap submit: status="+failure.Visible+"/"+failure.Bounds+" link="+link.Visible+"/"+link.Bounds+" submit="+submit.Bounds+" height="+pending.Height);
   SurfaceScreenshot(pending,Path.Combine(directory,"feedback-error-layout.png"));
   int errorHeight=pending.Height;text.Text="修改后重试";Check(!failure.Visible&&!link.Visible&&pending.Height<errorHeight,"editing after failure clears the message and returns to compact layout");
  }
  using(var dialog=new FeedbackDialog(json=>"网络不可用")) {
   dialog.Show();Application.DoEvents();
   System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
   var issueLink=(LinkLabel)Field(dialog,"issueLink");
   Check(!issueLink.Visible,"the issue link stays hidden before any failure");
   ((TextBox)Field(dialog,"body")).Text="测试反馈";
   Click(Children(dialog).OfType<Button>().Single(b=>b.Text=="发送反馈"));
   for(int i=0;i<60&&dialog.Visible&&!((Label)Field(dialog,"status")).Text.Contains("失败");i++){Application.DoEvents();System.Threading.Thread.Sleep(10);}
   Check(dialog.Visible&&((Label)Field(dialog,"status")).Text.Contains("失败")&&issueLink.Visible&&((PanelButton)Field(dialog,"send")).Enabled,"failed feedback keeps the reason inline and offers the issue link");
   Native.PostMessage(dialog.Handle,0x100,new IntPtr(27),IntPtr.Zero);Application.DoEvents(); // VK_ESCAPE via KeyPreview
   Check(dialog.DialogResult==DialogResult.Cancel&&!dialog.Visible,"escape closes the feedback dialog");
  }
 }
 static void TestFeedbackLayout(string directory) {
  var previous=PanelTheme.Mode;
  try {
   foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})foreach(float target in new[]{1f,1.5f,2f}) {
    PanelTheme.SetMode(theme);
    using(var dialog=new FeedbackDialog(json=>"隔离预览")) {
     dialog.Show();Application.DoEvents();System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());dialog.ApplyDpi(target,new Rectangle(dialog.Left,dialog.Top,(int)Math.Round(420*target),(int)Math.Round(408*target)));Application.DoEvents();
     var parent=(Panel)Field(dialog,"surface");var label=(Label)Field(dialog,"bodyLabel");var contactLabel=(Label)Field(dialog,"contactLabel");var body=(TextBox)Field(dialog,"body");var contact=(TextBox)Field(dialog,"contact");var bodyFrame=(InputPanel)Field(dialog,"bodyFrame");var contactFrame=(InputPanel)Field(dialog,"contactFrame");var privacy=(Label)Field(dialog,"privacy");var send=(PanelButton)Field(dialog,"send");string sample=theme+"-"+(int)(target*100);
     Check(!parent.HorizontalScroll.Visible,"feedback never needs horizontal scrolling: "+sample);
     Check(label.Bottom<bodyFrame.Top&&bodyFrame.Bottom<contactLabel.Top&&contactLabel.Bottom<contactFrame.Top&&contactFrame.Bottom<privacy.Top&&privacy.Bottom<send.Top,"feedback groups have separated labels and readable footer: "+sample);
     Check(parent.ClientRectangle.Contains(bodyFrame.Bounds)&&parent.ClientRectangle.Contains(contactFrame.Bounds)&&parent.ClientRectangle.Contains(send.Bounds),"feedback fields and submit remain inside the window: "+sample);
     Check(bodyFrame.ClientRectangle.Contains(body.Bounds)&&contactFrame.ClientRectangle.Contains(contact.Bounds),"feedback text stays inside its single outline: "+sample);
     Check(body.BackColor==PanelTheme.Back&&contact.BackColor==PanelTheme.Back,"feedback inputs share the selected page theme: "+sample);
     foreach(var field in new[]{body,contact}){var hint=(Label)Field(dialog,field==body?"bodyHint":"contactHint");Check(hint.Parent.GetChildAtPoint(new Point(hint.Left+2,hint.Top+2))==hint,"feedback placeholder is actually above the textbox: "+sample);}
     FeedbackScreenshot(dialog,Path.Combine(directory,"feedback-empty-"+sample+".png"));
     body.Text="打开设置后，希望仍能用快捷键收起窗口。";body.Focus();FeedbackScreenshot(dialog,Path.Combine(directory,"feedback-focused-"+sample+".png"));
     System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
     Click(send);for(int i=0;i<100&&!((LinkLabel)Field(dialog,"issueLink")).Visible;i++)Pump(10);Pump(20);
     var link=(LinkLabel)Field(dialog,"issueLink");var failure=(Label)Field(dialog,"status");
     Check(link.Visible&&link.Bottom<send.Top&&failure.Bottom<link.Top,"feedback failure remains above submit at every DPI: "+sample+" link="+link.Visible+"/"+link.Bounds+" status="+failure.Bounds+" submit="+send.Bounds);
    }
   }
   foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark}) {
    PanelTheme.SetMode(theme);
    using(var dialog=new FeedbackDialog(json=>"隔离短屏错误")) {
     dialog.Show();Pump(20);System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
     var text=(TextBox)Field(dialog,"body");text.Text="短屏反馈草稿";
     dialog.ApplyDpi(4f,new Rectangle(dialog.Left,dialog.Top,1680,1632));Pump(20);
     var area=Screen.FromControl(dialog).WorkingArea;var viewport=(Panel)Field(dialog,"surface");var submit=(PanelButton)Field(dialog,"send");
     Check(area.Contains(dialog.Bounds),"feedback height is constrained to the actual working area at extreme DPI: "+theme);
     Check(viewport.AutoScroll&&viewport.VerticalScroll.Visible,"short-screen feedback exposes its overflow by scrolling: "+theme);
     viewport.ScrollControlIntoView(submit);Pump(20);
     Check(viewport.ClientRectangle.Contains(viewport.RectangleToClient(submit.Parent.RectangleToScreen(submit.Bounds)))&&submit.Enabled,"short-screen send action can be fully revealed: "+theme);
     System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
     Click(submit);for(int i=0;i<100&&!((LinkLabel)Field(dialog,"issueLink")).Visible;i++)Pump(10);Pump(20);
     var link=(LinkLabel)Field(dialog,"issueLink");
     Check(area.Contains(dialog.Bounds)&&link.Visible&&text.Text=="短屏反馈草稿","short-screen failure stays on screen and preserves the draft: "+theme);
     Check(!viewport.HorizontalScroll.Visible,"constrained feedback only scrolls vertically: "+theme);
     viewport.ScrollControlIntoView(link);Pump(20);
     Check(viewport.ClientRectangle.Contains(viewport.RectangleToClient(link.Parent.RectangleToScreen(link.Bounds))),"short-screen fallback link can be fully revealed: "+theme+" viewport="+viewport.ClientRectangle+" shown="+viewport.RectangleToClient(link.Parent.RectangleToScreen(link.Bounds))+" scroll="+viewport.AutoScrollPosition+" canvas="+link.Parent.Bounds);
     viewport.ScrollControlIntoView(submit);Pump(20);
     Check(viewport.ClientRectangle.Contains(viewport.RectangleToClient(submit.Parent.RectangleToScreen(submit.Bounds))),"short-screen retry remains reachable after failure: "+theme);
    }
   }
  }finally{PanelTheme.SetMode(previous);}
 }
 static void FeedbackScreenshot(Form dialog,string path) {
  using(var bitmap=new Bitmap(dialog.Width,dialog.Height)) {
   using(var graphics=Graphics.FromImage(bitmap)){var dc=graphics.GetHdc();try{Check(PrintWindow(dialog.Handle,dc,2),"native feedback capture succeeds");}finally{graphics.ReleaseHdc(dc);}}
   bitmap.Save(path);
  }
 }
 static void TestModeCatalog(string directory) {
  var root=(string)typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  var modesPath=Path.Combine(root,"modes.json");
  File.Delete(modesPath);
  ChatModes.Reset();
  // storage layer
  var fresh=ModeCatalog.Load();
  Check(fresh.Error==null&&fresh.Overrides.Count==0&&fresh.Custom.Count==0,"missing modes file loads builtin modes only");
  var catalog=new ModeCatalog.Catalog();
  catalog.Overrides["translate"]="请逐句精翻。";
  catalog.Overrides["normal"]="不该被保存";
  catalog.Custom.Add(new CustomChatMode{Id=ModeCatalog.NewId(),Name="口语化",Instruction="请改成口语。"});
  Check(ModeCatalog.Save(catalog)==null,"valid mode catalog saves");
  var loaded=ModeCatalog.Load();
  Check(loaded.Error==null&&loaded.Overrides.Count==1&&loaded.Overrides["translate"]=="请逐句精翻。"&&!loaded.Overrides.ContainsKey("normal"),"overrides round trip and normal is never overridable");
  Check(loaded.Custom.Count==1&&loaded.Custom[0].Name=="口语化"&&loaded.Custom[0].Instruction=="请改成口语。","custom modes round trip");
  Check(ModeCatalog.ValidateName("翻译模式",loaded.Custom,null)!=null,"custom mode names may not collide with builtins");
  Check(ModeCatalog.ValidateName("口语化",loaded.Custom,loaded.Custom[0])==null,"editing a mode keeps its own name allowed");
  Check(ModeCatalog.ValidateName(new string('名',17),loaded.Custom,null)!=null&&ModeCatalog.ValidateName("  ",loaded.Custom,null)!=null,"mode name length and blank rules apply");
  Check(ModeCatalog.ValidateInstruction(new string('字',2000))==null&&ModeCatalog.ValidateInstruction(new string('字',2001))!=null&&ModeCatalog.ValidateInstruction("  ")!=null,"mode instruction limits enforced");
  var tooMany=new ModeCatalog.Catalog();for(int i=0;i<ModeCatalog.MaxCustomModes+1;i++)tooMany.Custom.Add(new CustomChatMode{Id="x"+i,Name="模式"+i,Instruction="内容"});
  Check(ModeCatalog.Save(tooMany)!=null,"more than ten custom modes rejected");
  File.WriteAllText(modesPath,"{not json");
  var bad=ModeCatalog.Load();
  Check(bad.Error!=null&&bad.Overrides.Count==0&&bad.Custom.Count==0&&File.ReadAllText(modesPath)=="{not json","unreadable modes file falls back to builtins and keeps the file");
  File.WriteAllText(modesPath,"{\"Version\":9}");
  Check(ModeCatalog.Load().Error!=null,"unknown modes version is reported");
  // catalog layer
  File.WriteAllText(modesPath,"{\"Version\":1,\"Overrides\":[{\"Id\":\"translate\",\"Instruction\":\"请逐句精翻。\"},{\"Id\":\"made-up\",\"Instruction\":\"未知模式\"}],\"Custom\":[{\"Id\":\"m-1\",\"Name\":\"口语化\",\"Instruction\":\"请改成口语。\"},{\"Id\":\"m-1\",\"Name\":\"重名\",\"Instruction\":\"重复 id 被丢弃。\"}]}");
  var applied=ModeCatalog.Load();
  Check(applied.Overrides.Count==1&&applied.Custom.Count==1,"loading drops unknown override ids and duplicate custom ids");
  ChatModes.Reset();
  Check(ChatModes.Effective.Length==6&&ChatModes.Find("nope").Id=="normal","clean slate keeps six builtins with normal fallback");
  ChatModes.Apply(applied);
  Check(ChatModes.Effective.Length==7&&ChatModes.Find("translate").Instruction=="请逐句精翻。"&&ChatModes.Find("m-1").Name=="口语化","applied catalog serves overrides and custom modes");
  Check(ChatModes.Prefix(ChatModes.Find("translate")).StartsWith("【当前模式：翻译模式】")&&ChatModes.Prefix(ChatModes.Find("translate")).Contains("请逐句精翻。")&&PromptInserter.Compose("材料",0).StartsWith("【当前模式：翻译模式】")&&PromptInserter.Compose("材料",0).Contains("请逐句精翻。"),"mode prefix and selection compose open with the directive and follow the overridden wording");
  ChatModes.Reset();
  Check(ChatModes.Effective.Length==6&&ChatModes.Find("m-1").Id=="normal","reset restores builtins and deleted custom ids fall back to normal");
  // settings page
  GettingStarted.Acknowledge();
  string routed=null;
  using(var dialog=new SettingsDialog(WindowSettings.Defaults(),delegate(WindowSettings next){return null;},delegate(string action){routed=action;},"modes")) {
   dialog.Show();Application.DoEvents();
   Check(((Label)Field(dialog,"heading")).Text=="聊天模式","modes page opens with its heading");
   var modeList=(Panel)Field(dialog,"modeList");
   Check(modeList.Controls.OfType<Label>().Count()==7&&modeList.Controls.OfType<Label>().Any(l=>l.Text=="口语化"),"mode list shows ordinary, five builtins and the existing custom mode");
   typeof(SettingsDialog).GetMethod("EditBuiltinMode",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,new object[]{"translate"});
   var body=(TextBox)Field(dialog,"modeBody");
   var draft=(ModeCatalog.Catalog)Field(dialog,"modeDraft");
   Check(body.Text==draft.Overrides["translate"]&&((TextBox)Field(dialog,"modeName")).ReadOnly,"builtin editor loads the live instruction with a locked name");
   Check(((TextBox)Field(dialog,"modeName")).ForeColor==PanelTheme.Muted,"readonly mode name retains its field boundary with muted text");SurfaceScreenshot(dialog,Path.Combine(directory,"readonly-mode-preview.png"));
   body.Text="请先列术语表再翻译。";
   typeof(SettingsDialog).GetMethod("SaveModeEntry",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);
   Click((Button)Field(dialog,"saveModes"));
   Check(routed=="modes"&&((Label)Field(dialog,"heading")).Text=="小窗设置","saving modes routes the reload action and returns to the general page");
  }
  var persisted=ModeCatalog.Load();
  Check(persisted.Error==null&&persisted.Overrides["translate"]=="请先列术语表再翻译。"&&persisted.Custom.Count==1&&persisted.Custom[0].Name=="口语化","the modes page persists the edited override and keeps unrelated custom modes");
  File.Delete(modesPath);
  ChatModes.Reset();
 }
 static int SessionToggleKeys {get{return (int)(Keys.Control|Keys.F20);}}
 static void TestInputFocus(string directory) {
  var previous=PanelTheme.Mode;
  try {
   foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})foreach(float dpi in new[]{1f,1.5f,2f}) {
    PanelTheme.SetMode(theme);string sample=theme+"-"+dpi;
    Action<TextBox,Control,string> verify=delegate(TextBox field,Control away,string label) {
     field.Focus();Pump(10);
     Check(field.Focused&&field.SelectionLength==0,"first field focus never selects existing contents: "+label+sample);
     var frame=field.Parent as InputPanel;Check(frame!=null&&frame.Accent,"focused field has its shared outline: "+label+sample);
     field.Select(1,Math.Min(3,field.TextLength-1));int start=field.SelectionStart,length=field.SelectionLength;
     away.Focus();Pump(10);Check(!frame.Accent&&field.HideSelection,"inactive field hides active focus and selection: "+label+sample);
     field.Focus();Pump(10);Check(field.SelectionStart==start&&field.SelectionLength==length,"returning focus preserves an intentional selection: "+label+sample);
     field.SelectAll();Check(field.SelectionLength==field.TextLength,"explicit select-all remains available: "+label+sample);
    };
    using(var dialog=new SettingsDialog(WindowSettings.Defaults(),next=>null,null,"commands")) {
     dialog.Show();Pump(10);dialog.ApplyDpi(dpi,new Rectangle(dialog.Location,new Size((int)(460*dpi),(int)(580*dpi))));
     Click((Button)Field(dialog,"addCommand"));var name=(TextBox)Field(dialog,"commandName");var body=(TextBox)Field(dialog,"commandBody");var away=(Control)Field(dialog,"close");
     verify(name,away,"command name ");away.Focus();
     verify(body,away,"command body ");name.Focus();dialog.SelectNextControl(name,true,true,true,true);Check(body.Focused,"Tab moves command name to body: "+sample);
     name.Text=new string('字',17);Click((Button)Field(dialog,"saveEntry"));Pump(10);
     Check(name.Focused&&name.SelectionLength==0&&name.TextLength==17&&((Label)Field(dialog,"nameError")).Text.Length>0,"invalid name keeps its contents without selecting all: "+sample);
    }
    using(var dialog=new SettingsDialog(WindowSettings.Defaults(),next=>null,null,"modes")) {
     dialog.Show();Pump(10);dialog.ApplyDpi(dpi,new Rectangle(dialog.Location,new Size((int)(460*dpi),(int)(660*dpi))));
     Click((Button)Field(dialog,"addMode"));var name=(TextBox)Field(dialog,"modeName");var body=(TextBox)Field(dialog,"modeBody");var away=(Control)Field(dialog,"close");
     verify(name,away,"mode name ");verify(body,away,"mode body ");
     typeof(SettingsDialog).GetMethod("EditBuiltinMode",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,new object[]{"summarize"});
     body.Text=new string('字',2001);Click((Button)Field(dialog,"saveModeEntry"));Pump(10);
     Check(body.Focused&&body.SelectionLength==0&&body.TextLength==2001,"invalid prompt retains contents without selecting all: "+sample);
     body.Select(2,3);var viewport=(Panel)Field(dialog,"body");viewport.ScrollControlIntoView(body.Parent);var scroll=viewport.AutoScrollPosition;
     dialog.SuspendForToggle();dialog.RestoreAfterToggle(null);Pump(50);
     Check(body.Focused&&body.SelectionStart==2&&body.SelectionLength==3&&viewport.AutoScrollPosition==scroll,"restoring settings retains selection and scroll: "+sample);
     Check(name.ReadOnly,"builtin name remains readonly: "+sample);name.Focus();name.SelectAll();Check(name.SelectedText==name.Text,"readonly name can still be selected for copying: "+sample);
    }
    using(var dialog=new SettingsDialog(WindowSettings.Defaults(),next=>null)) {
     dialog.Show();Pump(10);var toggle=(HotkeyBox)Field(dialog,"toggle");var capture=(HotkeyBox)Field(dialog,"capture");toggle.Focus();Pump(10);
     Check(toggle.Recording&&toggle.SelectionLength==0,"shortcut recording has no automatic text selection: "+sample);
     dialog.SelectNextControl(toggle,true,true,true,true);Pump(10);Check(!toggle.Recording&&capture.Focused&&capture.Recording&&capture.SelectionLength==0,"Tab transfers shortcut recording without selecting text: "+sample);
     Native.SendMessage(capture.Handle,0x100,new IntPtr(27),IntPtr.Zero);Pump(10);Check(!capture.Recording&&!dialog.ShortcutEntryFocused&&dialog.Visible,"Escape exits shortcut recording without closing settings: "+sample);
    }
    Preferences.Write("feedback-contact","fixture@example.invalid");
    using(var dialog=new FeedbackDialog(json=>"isolated")) {
     dialog.Show();Pump(10);dialog.ApplyDpi(dpi,new Rectangle(dialog.Location,new Size((int)(420*dpi),(int)(408*dpi))));
     var body=(TextBox)Field(dialog,"body");var contact=(TextBox)Field(dialog,"contact");var away=(Control)Field(dialog,"close");
     body.Text="feedback fixture";verify(contact,away,"remembered contact ");verify(body,away,"feedback body ");
     body.Focus();dialog.SelectNextControl(body,true,true,true,true);Check(contact.Focused,"Tab moves feedback body to contact: "+sample);
     body.Focus();body.Select(1,3);System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Click((Button)Field(dialog,"send"));
     for(int i=0;i<100&&!((LinkLabel)Field(dialog,"issueLink")).Visible;i++)Pump(10);
     Check(((LinkLabel)Field(dialog,"issueLink")).Visible&&body.Text=="feedback fixture"&&body.SelectionStart==1&&body.SelectionLength==3,"failed feedback retains draft and intentional selection: "+sample);
    }
   }
  }finally{PanelTheme.SetMode(previous);Preferences.Write("feedback-contact","");}
 }
 static void TestAuditUi(string directory) {
  var failures=new List<string>();
  Action<string,Action> run=delegate(string name,Action action){try{action();Check(true,name);}catch(Exception error){failures.Add(name);Console.WriteLine("FAIL "+name+": "+error.GetType().Name);}};
  run("editing builtin prompts places the caret at the end without selecting text",delegate{
   using(var dialog=new SettingsDialog(WindowSettings.Defaults(),next=>null,null,"modes")) {
    dialog.Show();Pump(20);
    foreach(var id in new[]{"summarize","translate","summarize"}) {
     typeof(SettingsDialog).GetMethod("EditBuiltinMode",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,new object[]{id});Pump(20);
     var editor=(TextBox)Field(dialog,"modeBody");
     if(!editor.Focused||editor.SelectionLength!=0||editor.SelectionStart!=editor.TextLength)throw new Exception("Unexpected caret/selection");
     editor.SelectedText="追加";if(!editor.Text.EndsWith("追加")||editor.TextLength<=2)throw new Exception("Typing replaced the prompt");
     editor.SelectAll();if(editor.SelectionLength!=editor.TextLength)throw new Exception("Explicit selection was disabled");
    }
   }
  });
  run("final command save includes the currently edited entry",delegate{
   File.Delete(Path.Combine(Preferences.Root,"commands.json"));
   using(var dialog=new SettingsDialog(WindowSettings.Defaults(),next=>null,null,"commands")){dialog.Show();Application.DoEvents();((TextBox)Field(dialog,"commandName")).Text="直接保存";((TextBox)Field(dialog,"commandBody")).Text="保留当前表单";Click((PanelButton)Field(dialog,"saveCommands"));if(!PromptCatalog.Load().Commands.Any(c=>c.Custom&&c.Name=="直接保存"))throw new Exception();}
  });
  run("final mode save includes the currently edited entry",delegate{
   File.Delete(Path.Combine(Preferences.Root,"modes.json"));
   using(var dialog=new SettingsDialog(WindowSettings.Defaults(),next=>null,null,"modes")){dialog.Show();Application.DoEvents();Click((PanelButton)Field(dialog,"addMode"));((TextBox)Field(dialog,"modeBody")).Text="保留本次提示词";Click((PanelButton)Field(dialog,"saveModes"));if(!ModeCatalog.Load().Custom.Any(c=>c.Instruction=="保留本次提示词"))throw new Exception();}
  });
  run("native close asks before discarding a mode draft",delegate{
   using(var dialog=new SettingsDialog(WindowSettings.Defaults(),next=>null,null,"modes")){CloseReason reason=CloseReason.None;dialog.FormClosing+=delegate(object sender,FormClosingEventArgs e){reason=e.CloseReason;};dialog.Show();Application.DoEvents();Click((PanelButton)Field(dialog,"addMode"));Native.SendMessage(dialog.Handle,0x10,IntPtr.Zero,IntPtr.Zero);Console.WriteLine("Native close reason="+reason+" visible="+dialog.Visible+" confirming="+((PanelButton)Field(dialog,"keepEditing")).Visible);if(!dialog.Visible||!((PanelButton)Field(dialog,"keepEditing")).Visible)throw new Exception();Click((PanelButton)Field(dialog,"discardEditing"));Pump(20);if(dialog.Visible)throw new Exception();}
  });
  run("late capture cannot fill or expose the selection bar while settings is open",delegate{
   GettingStarted.Acknowledge();using(var window=new ChatWindow(new[]{"--background"})){typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Show();Application.DoEvents();Com(window,"ShowSettings",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);var task=(System.Threading.Tasks.Task)Com(window,"PresentSelectionAt",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod,new SelectionResult{Text="延迟取词",Method="fixture"},WakeTarget.Capture());task.GetAwaiter().GetResult();if(Field(window,"selectedText")!=null||((Control)Field(window,"selectionBar")).Visible)throw new Exception();typeof(ChatWindow).GetField("quitting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Close();}
  });
  run("general settings save cannot resurrect a deleted active mode",delegate{
   var catalog=new ModeCatalog.Catalog();catalog.Custom.Add(new CustomChatMode{Id="m-audit",Name="临时模式",Instruction="临时指令"});if(ModeCatalog.Save(catalog)!=null)throw new Exception();ChatModes.Apply(catalog);
   var preferences=WindowSettings.Defaults();preferences.ChatMode="m-audit";preferences.ToggleKeys=K(Keys.F23);preferences.CaptureKeys=K(Keys.F24);preferences.Save();
   using(var window=new ChatWindow(new[]{"--background"})){
    typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Show();Application.DoEvents();
    Com(window,"ShowSettingsPage",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod,"modes");var dialog=window.OwnedForms.OfType<SettingsDialog>().Single();
    Console.WriteLine("Delete-mode state before="+window.Settings.ChatMode);if(ModeCatalog.Save(new ModeCatalog.Catalog())!=null)throw new Exception();
    ((System.Threading.Tasks.Task)Com(window,"ReloadChatModes",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod)).GetAwaiter().GetResult();
    Console.WriteLine("Delete-mode state after reload="+window.Settings.ChatMode);
    Com(dialog,"ShowGeneralPage",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);Click((PanelButton)Field(dialog,"save"));
    Console.WriteLine("Delete-mode state after save="+window.Settings.ChatMode+" error="+((Label)Field(dialog,"error")).Text);
    if(window.Settings.ChatMode!="normal"||File.ReadAllText(Path.Combine(Preferences.Root,"window-settings.json")).Contains("m-audit"))throw new Exception();typeof(ChatWindow).GetField("quitting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Close();
   }
   ChatModes.Reset();WindowSettings.Defaults().Save();
  });
  Check(failures.Count==0,"all audited UI boundaries: "+string.Join(",",failures));
 }
 static void SendSessionToggle(bool repeated=false) {
  var inputs=new List<Native.INPUT>{Native.Key(0x11,false),Native.Key((ushort)Keys.F20,false),Native.Key((ushort)Keys.F20,true),Native.Key(0x11,true)};
  if(repeated)inputs.Insert(2,Native.Key((ushort)Keys.F20,false));
  Check(Native.SendInput((uint)inputs.Count,inputs.ToArray(),System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.INPUT)))==inputs.Count,"real keyboard input reaches the registered global toggle");Pump(180);
 }
 static void TestSettingsSession(string directory) {
  var preferenceRoot=(string)typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  var preferencePath=Path.Combine(preferenceRoot,"window-settings.json");var priorPreferences=File.Exists(preferencePath)?File.ReadAllBytes(preferencePath):null;
  try {
  var preferences=WindowSettings.Defaults();preferences.ToggleKeys=SessionToggleKeys;preferences.CaptureKeys=K(Keys.F21);preferences.Theme="light";preferences.Save();GettingStarted.Acknowledge();
  foreach(bool toTray in new[]{false,true})using(var window=new ChatWindow(new[]{"--background"})) {
   typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
   window.Show();Application.DoEvents();var bindings=(HotkeyBindings)Field(window,"hotkeys");((WindowSettings)Field(window,"settings")).HideToTrayOnToggle=toTray;
   foreach(string pageName in new[]{"general","commands","modes"}) {
    Com(window,"ShowSettingsPage",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod,pageName=="general"?null:pageName);Pump(40);
    var dialog=window.OwnedForms.OfType<SettingsDialog>().Single();
    Check(bindings.ToggleRegistered&&!bindings.CaptureRegistered,"settings keeps only the real global toggle registered: "+pageName);
    Check(IsWindowEnabled(window.Handle)&&!((Control)Field(window,"browser")).Enabled&&!((Control)Field(window,"mode")).Enabled,"settings permits owner geometry and pauses competing input: "+pageName);
    Check(!dialog.ShortcutEntryFocused,"opening settings does not enter shortcut recording");
    TextBox draft=null;
    if(pageName!="general") {
     draft=(TextBox)Field(dialog,pageName=="commands"?"commandBody":"modeBody");draft.Text="尚未保存的测试草稿\r\n第二行";draft.Focus();draft.Select(2,3);
    }else{((PanelSwitch)Field(dialog,"focus")).Checked=false;((ChromeButton)Field(dialog,"close")).Focus();}
    dialog.ClientSize=new Size(dialog.Width,Math.Min(dialog.Height,400));Application.DoEvents();
    var viewport=(Panel)Field(dialog,"body");viewport.AutoScrollPosition=new Point(0,80);Application.DoEvents();var scroll=viewport.AutoScrollPosition;
    dialog.Activate();Native.SetForegroundWindow(dialog.Handle);Pump(30);
    if(draft==null){((ChromeButton)Field(dialog,"close")).Focus();Pump(30);Check(bindings.ToggleRegistered&&!dialog.ShortcutEntryFocused,"session toggle remains available after foreground activation");}
    if(draft!=null){viewport.ScrollControlIntoView(draft.Parent);draft.Focus();draft.Select(2,3);Pump(30);scroll=viewport.AutoScrollPosition;Check(draft.Focused&&draft.SelectionStart==2&&draft.SelectionLength==3,"draft editor is focused before group toggle: "+pageName);}
    for(int cycle=0;cycle<3;cycle++) {
     SendSessionToggle(cycle==0);Check(!dialog.Visible&&!dialog.IsDisposed&&(toTray?!window.Visible:window.WindowState==FormWindowState.Minimized),"toggle suspends the entire window group without repeating: "+pageName+" "+toTray);
     SendSessionToggle();Check(dialog.Visible&&window.Visible&&window.WindowState!=FormWindowState.Minimized&&Object.ReferenceEquals(dialog,window.OwnedForms.OfType<SettingsDialog>().Single()),"toggle restores the same settings instance: "+pageName);
     Check(viewport.AutoScrollPosition==scroll,"restoring settings preserves the scroll position: "+pageName);
     if(draft!=null){uint foregroundPid;Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out foregroundPid);Check(draft.Focused&&draft.Text.Contains("尚未保存")&&draft.SelectionStart==2&&draft.SelectionLength==3,"restoring settings preserves draft focus and text selection: "+pageName+" actual="+draft.Focused+"/"+draft.SelectionStart+"/"+draft.SelectionLength+" text="+draft.Text.Replace("\r\n","|")+" remembered="+Object.ReferenceEquals(Field(dialog,"rememberedFocus"),draft)+" active="+Object.ReferenceEquals(dialog.ActiveControl,draft)+" foreground="+Control.FromHandle(Native.GetForegroundWindow())+" process="+System.Diagnostics.Process.GetProcessById((int)foregroundPid).ProcessName);}
    }
    Com(window,"ShowSettings",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);Check(Object.ReferenceEquals(dialog,window.OwnedForms.OfType<SettingsDialog>().Single()),"reopening settings activates the existing editing session");
    if(pageName=="general") {
     var edge=window.PointToScreen(new Point(1,window.ClientSize.Height/2));var hit=Native.SendMessage(window.Handle,0x84,IntPtr.Zero,new IntPtr((edge.Y<<16)|(edge.X&0xffff))).ToInt32();
     Check(hit==10,"owner keeps its native resize edge while settings is open");
     bool wasPinned=(bool)Field(window,"pinned");Com(window,"SetPinned",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod,!wasPinned);Check(dialog.TopMost==window.TopMost,"owned settings follow changes to owner pinning");Com(window,"SetPinned",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod,wasPinned);
     var shortcut=(HotkeyBox)Field(dialog,"toggle");shortcut.Focus();Pump(30);
     Check(!bindings.ToggleRegistered&&!bindings.CaptureRegistered,"shortcut entry temporarily releases both global bindings");
     PressKey(shortcut,Keys.Escape);Pump(30);Check(!shortcut.Recording&&!dialog.ShortcutEntryFocused&&bindings.ToggleRegistered,"Escape leaves shortcut entry and restores the toggle");
     shortcut.Focus();PressKey(shortcut,(Keys)K(Keys.F22));PressKey(shortcut,Keys.Escape);Pump(30);Check(shortcut.Combination==SessionToggleKeys&&bindings.ToggleRegistered,"Escape cancels a completed but uncommitted shortcut entry");
     var capture=(HotkeyBox)Field(dialog,"capture");capture.Focus();Native.PostMessage(capture.Handle,0x100,new IntPtr((int)Keys.Tab),IntPtr.Zero);Pump(60);Check(!dialog.ShortcutEntryFocused&&bindings.ToggleRegistered,"Tab leaves shortcut entry and restores global toggling");
     shortcut.Focus();PressKey(shortcut,(Keys)K(Keys.F22));((ChromeButton)Field(dialog,"close")).Focus();Pump(30);
     Check(shortcut.Combination==K(Keys.F22)&&bindings.ToggleKeys==SessionToggleKeys&&bindings.ToggleRegistered,"edited shortcut stays pending while the saved toggle remains active");
     var root=(string)typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);var path=Path.Combine(root,"window-settings.json");var backup=path+".session-test";File.Move(path,backup);Directory.CreateDirectory(path);
     try{Click((Button)Field(dialog,"save"));Check(dialog.Visible&&((Label)Field(dialog,"error")).Text.Contains("未能保存")&&bindings.ToggleKeys==SessionToggleKeys&&bindings.ToggleRegistered&&!bindings.CaptureRegistered,"failed settings write preserves draft and restores the prior registration scope");}
     finally{Directory.Delete(path);File.Move(backup,path);}
     using(var blocker=new Form()) {
      Check(Native.RegisterHotKey(blocker.Handle,91,7,(uint)Keys.F22),"another owned window occupies the proposed shortcut");
      try{Click((Button)Field(dialog,"save"));Check(dialog.Visible&&((Label)Field(dialog,"error")).Text.Contains("占用")&&bindings.ToggleKeys==SessionToggleKeys&&bindings.ToggleRegistered,"shortcut conflict keeps settings open and the saved toggle working");}
      finally{Native.UnregisterHotKey(blocker.Handle,91);}
     }
    }
    SurfaceScreenshot(dialog,Path.Combine(directory,"session-"+pageName+"-"+toTray+".png"));dialog.Close();if(!dialog.IsDisposed&&((PanelButton)Field(dialog,"discardEditing")).Visible)Click((PanelButton)Field(dialog,"discardEditing"));Pump(30);
    Check(bindings.ToggleRegistered&&bindings.CaptureRegistered&&((Control)Field(window,"browser")).Enabled,"closing settings restores all bindings and owner controls");
   }
   Com(window,"ShowSettings",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);var saving=window.OwnedForms.OfType<SettingsDialog>().Single();
   var replacement=(HotkeyBox)Field(saving,"toggle");replacement.Focus();PressKey(replacement,(Keys)K(Keys.F22));((ChromeButton)Field(saving,"close")).Focus();Pump(30);Click((Button)Field(saving,"save"));Pump(30);
   Check(saving.IsDisposed&&bindings.ToggleKeys==K(Keys.F22)&&bindings.ToggleRegistered&&bindings.CaptureRegistered&&WindowSettings.Load().ToggleKeys==K(Keys.F22),"successful save commits and registers the new shortcut only after saving");
   Com(window,"ShowSettings",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);var exiting=window.OwnedForms.OfType<SettingsDialog>().Single();
   typeof(ChatWindow).GetField("quitting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Close();
   Check(exiting.IsDisposed&&!bindings.ToggleRegistered&&!bindings.CaptureRegistered,"exit disposes the retained settings session and releases its bindings");preferences.Save();
  }
  }finally{if(priorPreferences==null)File.Delete(preferencePath);else File.WriteAllBytes(preferencePath,priorPreferences);}
 }
 static void SurfaceScreenshot(Form dialog,string path) {
  dialog.Show();Application.DoEvents();
  using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,dialog.ClientRectangle);bitmap.Save(path);}
 }
 static void TestSurfaceScreens(string directory) {
  var previousTheme=PanelTheme.Mode;
  try {
   foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark}) {
    PanelTheme.SetMode(theme);string prefix=Path.Combine(directory,"surface-"+theme+"-");
    var preferences=WindowSettings.Defaults();preferences.Theme=theme==UiTheme.Dark?"dark":"light";preferences.Save();
    using(var input=new InputPanel {Size=new Size(240,60),BackColor=PanelTheme.Back})using(var bitmap=new Bitmap(240,60)) {
     var sides=new[]{new Point(0,30),new Point(235,30),new Point(120,0),new Point(120,55)};
     input.DrawToBitmap(bitmap,input.ClientRectangle);
     Check(sides.All(point=>Enumerable.Range(0,5).Any(offset=>bitmap.GetPixel(point.X+(point.X==0||point.X==235?offset:0),point.Y+(point.Y==0||point.Y==55?offset:0)).ToArgb()!=PanelTheme.Back.ToArgb())),"resting input has four visible boundaries: "+theme);
     input.Accent=true;input.DrawToBitmap(bitmap,input.ClientRectangle);
     Check(sides.All(point=>Enumerable.Range(0,5).Any(offset=>{var pixel=bitmap.GetPixel(point.X+(point.X==0||point.X==235?offset:0),point.Y+(point.Y==0||point.Y==55?offset:0));return pixel.B>pixel.R+40&&pixel.B>pixel.G+40;})),"focused input shows one blue outline on every edge: "+theme);
     bitmap.Save(prefix+"input-focused.png");
    }
    using(var dialog=new FeedbackDialog(json=>"离线测试")) {
     SurfaceScreenshot(dialog,prefix+"feedback.png");
     foreach(string name in new[]{"body","contact"}){var field=(TextBox)Field(dialog,name);Check(field.BackColor==dialog.BackColor&&field.Parent.BackColor==dialog.BackColor,"feedback inputs blend into the page: "+theme);}
     ((TextBox)Field(dialog,"body")).Text="这是界面验收的合成反馈，不会发送到服务器。";Click((Button)Field(dialog,"send"));Pump(200);SurfaceScreenshot(dialog,prefix+"feedback-error.png");
     dialog.Close();
    }
    using(var dialog=new GettingStartedDialog(WindowSettings.Defaults())) {
     for(int step=0;step<4;step++){dialog.SetStep(step);SurfaceScreenshot(dialog,prefix+"guide-"+step+".png");}
     var key=(Label)Field(dialog,"key");Check(key.BackColor==key.Parent.BackColor&&key.Parent.BackColor==dialog.BackColor,"help shortcut has no separate card surface: "+theme);
     dialog.Close();
    }
    using(var window=new QuickAnswerWindow("这是隔离检查的示例文字。",new Point(100,100))) {
     SurfaceScreenshot(window,prefix+"selection.png");
     Com(window,"Expand",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);SurfaceScreenshot(window,prefix+"selection-expanded.png");window.Close();
    }
    using(var popup=new PopupWindow(new Uri("https://example.com"),1f)) {
     popup.ShowError("离线预览：登录或链接内容由原网站提供。");SurfaceScreenshot(popup,prefix+"popup.png");popup.Close();
    }
    using(var dock=new EdgeDockTab(DockSide.Left,1f,SystemIcons.Application)){SurfaceScreenshot(dock,prefix+"dock.png");dock.Close();}
    using(var window=new ChatWindow(new[]{"--background"})) {
     typeof(ChatWindow).GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);typeof(ChatWindow).GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
     SurfaceScreenshot(window,prefix+"main.png");
     var bar=(Panel)Field(window,"selectionBar");bar.Show();Com(window,"LayoutContent",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);Application.DoEvents();
     using(var bitmap=new Bitmap(window.Width,bar.Bottom)){window.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(prefix+"selection-toolbar.png");}
     Com(window,"ShowChatModeMenu",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);Application.DoEvents();
     var modes=Field(window,"openMenu") as ContextMenuStrip;
     if(modes!=null){using(var bitmap=new Bitmap(modes.Width,modes.Height)){modes.DrawToBitmap(bitmap,modes.ClientRectangle);bitmap.Save(prefix+"actual-modes.png");}modes.Close();}
     var tray=(ContextMenuStrip)Com(window,"BuildMenu",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod);tray.Show(new Point(100,100));Application.DoEvents();
     using(var bitmap=new Bitmap(tray.Width,tray.Height)){tray.DrawToBitmap(bitmap,tray.ClientRectangle);bitmap.Save(prefix+"tray.png");}tray.Close();
     typeof(ChatWindow).GetField("quitting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Close();
    }
    using(var menu=new TrayMenu(1f)) {
     menu.AddAction("普通对话",delegate{}).Checked=true;menu.AddAction("翻译模式",delegate{});menu.AddDivider();menu.AddAction("自定义提示词…",delegate{});menu.Show(new Point(100,100));Application.DoEvents();
     using(var bitmap=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(bitmap,menu.ClientRectangle);bitmap.Save(prefix+"mode-menu.png");}menu.Close();
    }
   }
  }finally{PanelTheme.SetMode(previousTheme);}
 }
 static void TestGeneralLayout(string directory) {
  var previous=PanelTheme.Mode;
  try {
   foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})foreach(float targetScale in new[]{1f,1.5f,2f}) {
    PanelTheme.SetMode(theme);WindowSettings applied=null;
    using(var dialog=new SettingsDialog(WindowSettings.Defaults(),delegate(WindowSettings next){applied=next;return null;},delegate(string action){})) {
     dialog.Show();Application.DoEvents();float originalScale=(float)Field(dialog,"scale");
     dialog.ApplyDpi(targetScale,new Rectangle(dialog.Left,dialog.Top,(int)(460*targetScale),(int)(dialog.Height*targetScale/originalScale)));Application.DoEvents();
     var surface=(Panel)Field(dialog,"surface");string sample=theme+"-"+(int)(targetScale*100);
     using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,surface.ClientRectangle);bitmap.Save(Path.Combine(directory,"general-"+sample+".png"));}
     foreach(string fieldName in new[]{"toggle","capture"}) {
      var field=(HotkeyBox)Field(dialog,fieldName);var frame=field.Parent;
      Check(frame.Parent.Controls.Cast<Control>().Where(other=>other!=frame&&other.Visible).All(other=>!other.Bounds.IntersectsWith(frame.Bounds)),"shortcut surface has no overlapping labels: "+sample);
      Check(frame.ClientRectangle.Contains(field.Bounds),"shortcut text stays inside its field: "+sample);
     }
     foreach(string fieldName in new[]{"hideMode","windowMode","themeChoice"}) {
      var choice=(PanelChoices)Field(dialog,fieldName);Check(choice.Parent.ClientRectangle.Contains(choice.Bounds)&&choice.SelectedIndex==0,"setting retains its initial value and stays within its group: "+sample);
     }
     dialog.ClientSize=new Size((int)(340*targetScale),(int)(440*targetScale));Application.DoEvents();
     var viewport=(Panel)Field(dialog,"body");var help=Children(viewport).OfType<Button>().Single(button=>button.Text=="使用帮助");
     viewport.ScrollControlIntoView(help);Application.DoEvents();
     Check(!viewport.HorizontalScroll.Visible&&viewport.ClientRectangle.Contains(viewport.RectangleToClient(help.RectangleToScreen(help.ClientRectangle))),"utility actions are reachable on a short narrow window: "+sample);
     var restore=(Button)Field(dialog,"restoreDefault");Click(restore);
     Check(((HotkeyBox)Field(dialog,"capture")).Combination==(int)(Keys.Control|Keys.Shift|Keys.D),"shortcut reset still updates the draft: "+sample);
     viewport.ScrollControlIntoView(Children(viewport).OfType<Button>().Single(button=>button.Text=="快捷指令"));Application.DoEvents();
     Click(Children(viewport).OfType<Button>().Single(button=>button.Text=="快捷指令"));
     Check(((Label)Field(dialog,"heading")).Text=="快捷指令","management entry still opens its editor: "+sample);
     Click((Button)Field(dialog,"backToGeneral"));
     ((PanelChoices)Field(dialog,"hideMode")).SelectedIndex=1;((PanelChoices)Field(dialog,"windowMode")).SelectedIndex=1;((PanelChoices)Field(dialog,"themeChoice")).SelectedIndex=2;
     Click((Button)Field(dialog,"save"));Check(applied!=null&&applied.Theme=="dark"&&applied.Mode=="compact"&&applied.HideToTrayOnToggle,"save applies the selected theme size and hide behavior: "+sample);
    }
   }
  }finally{PanelTheme.SetMode(previous);}
 }
 static void TestEditorLayout(string directory) {
  var previousTheme=PanelTheme.Mode;
  try {
   foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark}) {
    PanelTheme.SetMode(theme);
    foreach(var pageName in new[]{"modes","commands"})foreach(float targetScale in new[]{1f,1.5f,2f}) {
     using(var dialog=new SettingsDialog(WindowSettings.Defaults(),delegate(WindowSettings next){return null;},null,pageName)) {
      dialog.Show();Application.DoEvents();
      float originalScale=(float)Field(dialog,"scale");
      dialog.ApplyDpi(targetScale,new Rectangle(dialog.Left,dialog.Top,(int)Math.Round(460*targetScale),(int)Math.Round(dialog.Height*targetScale/originalScale)));
      Application.DoEvents();
      var page=(Panel)Field(dialog,pageName=="modes"?"modesPage":"commandsPage");
      var name=(TextBox)Field(dialog,pageName=="modes"?"modeName":"commandName");
      var prompt=(TextBox)Field(dialog,pageName=="modes"?"modeBody":"commandBody");
      var surface=(Panel)Field(dialog,"surface");
      string sample=theme.ToString().ToLowerInvariant()+"-"+pageName+"-"+(int)(targetScale*100);
      using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,surface.ClientRectangle);bitmap.Save(Path.Combine(directory,"editor-"+sample+".png"));}
      foreach(var field in new[]{name,prompt}) {
       var input=field.Parent;
       Check(page.Controls.Cast<Control>().Where(sibling=>sibling!=input&&sibling.Visible).All(sibling=>!sibling.Bounds.IntersectsWith(input.Bounds)),"editor field is not covered by sibling labels or actions: "+sample+" "+field.AccessibleName);
       Check(input.ClientRectangle.Contains(field.Bounds)&&field.BackColor==input.BackColor,"editor text and padding form one complete surface: "+sample);
      }
      var actions=new[]{(PanelButton)Field(dialog,pageName=="modes"?"addMode":"addCommand"),(PanelButton)Field(dialog,pageName=="modes"?"saveModeEntry":"saveEntry")};
      if(pageName=="modes")actions=actions.Concat(new[]{(PanelButton)Field(dialog,"resetMode")}).ToArray();
      Check(actions.Select(button=>button.RectangleToScreen(button.ClientRectangle).Top).Distinct().Count()==1,"editor actions share a baseline: "+sample);
      Click(actions[0]);Application.DoEvents();prompt.Focus();Application.DoEvents();
      Check(name.Text.Length>0&&prompt.Text.Length>0,"example action fills both editor fields: "+sample);
      using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,surface.ClientRectangle);bitmap.Save(Path.Combine(directory,"editor-filled-"+sample+".png"));}
      dialog.ClientSize=new Size((int)Math.Round(340*targetScale),Math.Min(dialog.Height,(int)Math.Round(440*targetScale)));Application.DoEvents();
      var viewport=(Panel)Field(dialog,"body");
      viewport.ScrollControlIntoView(prompt);Application.DoEvents();prompt.Focus();Application.DoEvents();
      Check(prompt.Focused&&((InputPanel)prompt.Parent).Accent,"scrolling a short editor retains the input focus: "+sample);
      Check(!viewport.HorizontalScroll.Visible&&actions.All(button=>button.Parent.ClientRectangle.Contains(button.Bounds)),"narrow editor keeps all actions inside the viewport: "+sample);
      viewport.ScrollControlIntoView(actions.Last());Application.DoEvents();
      Check(actions.All(button=>viewport.ClientRectangle.Contains(viewport.RectangleToClient(button.RectangleToScreen(button.ClientRectangle)))),"all editor actions remain reachable by scrolling: "+sample);
      foreach(var field in new[]{name,prompt})Check(field.Parent.ClientRectangle.Contains(field.Bounds),"input remains inside its surface after resize and scroll: "+sample);
      dialog.Close();
     }
    }
   }
   using(var parent=new Panel {BackColor=PanelTheme.Back})using(var button=new PanelButton("恢复默认") {Ghost=true,Enabled=false,Size=new Size(100,30)}) {
    parent.Controls.Add(button);
    using(var bitmap=new Bitmap(button.Width,button.Height)){button.DrawToBitmap(bitmap,button.ClientRectangle);Check(bitmap.GetPixel(button.Width/2,3).ToArgb()==parent.BackColor.ToArgb(),"disabled ghost action blends into its parent surface");}
   }
  }finally{PanelTheme.SetMode(previousTheme);}
 }
 static void Click(Button button){typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});}
 [STAThread] static void Main(string[] args) {
  try {
   ShellIdentity.InitializeProcess();Check(ShellIdentity.CurrentId==ShellIdentity.AppId,"process publishes the unique DeepSeek-Reflex taskbar identity before UI creation");
   TestShortcutIdentity(args[0]);
   Application.EnableVisualStyles();
   var root=Path.Combine(args[0],"test-preferences-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   typeof(Preferences).GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,root);
   if(args.Contains("--input-focus-only")){GettingStarted.Acknowledge();TestInputFocus(args[0]);Console.WriteLine("TOTAL "+passed+" input focus checks");return;}
   if(args.Contains("--audit-ui-only")){GettingStarted.Acknowledge();TestAuditUi(args[0]);Console.WriteLine("TOTAL "+passed+" audit UI checks");return;}
   if(args.Contains("--feedback-only")){TestFeedback(args[0]);TestFeedbackLayout(args[0]);Console.WriteLine("TOTAL "+passed+" feedback checks");return;}
   if(args.Contains("--settings-session-only")){GettingStarted.Acknowledge();TestSettingsSession(args[0]);Console.WriteLine("TOTAL "+passed+" settings session checks");return;}
   if(args.Contains("--surfaces-only")){GettingStarted.Acknowledge();TestGeneralLayout(args[0]);TestEditorLayout(args[0]);TestEdgeDockAndCommands(args[0]);TestSurfaceScreens(args[0]);Console.WriteLine("TOTAL "+passed+" surface checks");return;}
   if(args.Contains("--general-layout-only")){GettingStarted.Acknowledge();TestGeneralLayout(args[0]);Console.WriteLine("TOTAL "+passed+" general settings checks");return;}
   if(args.Contains("--editor-layout-only")){GettingStarted.Acknowledge();TestEditorLayout(args[0]);TestModeCatalog(args[0]);Console.WriteLine("TOTAL "+passed+" editor checks");return;}
   if(args.Contains("--interaction-only")){GettingStarted.Acknowledge();TestInteractionPolish(args[0]);Console.WriteLine("TOTAL "+passed+" focused interaction checks");return;}
   if(args.Contains("--commands-only")){GettingStarted.Acknowledge();TestInteractionPolish(args[0]);TestEdgeDockAndCommands(args[0]);Console.WriteLine("TOTAL "+passed+" commands-only checks");return;}
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
   using(var guide=new GettingStartedDialog(guideSettings)){guide.Show();Application.DoEvents();Children(guide).OfType<ChromeButton>().Single(b=>b.Kind=="close").PerformClick();Check(!guide.Visible&&guide.DialogResult==DialogResult.Cancel,"introduction can be skipped without traversing all steps");}
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
    var modalBounds=window.Bounds;bool settingsOwnerInteractive=false,settingsCorners=true;int modalClosed=0;
    using(var closeSettings=new Timer {Interval=30}) {
     closeSettings.Tick+=delegate {
      foreach(var dialog in Application.OpenForms.OfType<SettingsDialog>().ToArray()) {
       settingsOwnerInteractive|=IsWindowEnabled(window.Handle)&&!((Control)Field(window,"browser")).Enabled&&Native.GetWindow(dialog.Handle,4)==window.Handle;
       settingsCorners&=ModalCornersAbove(dialog)&&((GetWindowLong(dialog.Handle,-20)&8)!=0)==window.TopMost;
       dialog.Location=new Point(dialog.Left+8,dialog.Top+6);settingsCorners&=ModalCornersAbove(dialog);modalClosed++;dialog.Close();
      }
     };
     closeSettings.Start();
     try {for(int cycle=0;cycle<3;cycle++) {
      Com(window,"SetPinned",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod,cycle%2==1);Pump(20);
      Check(((GetWindowLong(window.Handle,-20)&8)!=0)==window.TopMost&&smooth.Surfaces.All(c=>(GetWindowLong(c.Handle,-20)&8)==(GetWindowLong(window.Handle,-20)&8)),"actual chat pin transition preserves native owner and corner bands: cycle="+cycle);
      typeof(ChatWindow).GetMethod("ShowSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
      for(int tick=0;tick<100&&modalClosed<=cycle;tick++)Pump(10);
      Application.DoEvents();
      Console.WriteLine("Modal return: actual="+window.Bounds+" expected="+modalBounds+" enabled="+IsWindowEnabled(window.Handle)+" edges="+CleanStraightEdges(window)+" dpi="+Field(window,"dpiScale"));
      Check(IsWindowEnabled(window.Handle)&&window.Bounds==modalBounds&&CleanStraightEdges(window),"actual modal settings return preserves geometry and white margins: cycle="+cycle);
     }}finally {closeSettings.Stop();}
    }
    Check(settingsOwnerInteractive&&modalClosed==3&&((HotkeyBindings)Field(window,"hotkeys")).ToggleRegistered,"owned settings preserve owner geometry then close and restore bindings on every cycle");
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
      Com(window,"SetPinned",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.InvokeMethod,cycle%2==1);Pump(20);
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
   TestEdgeDockAndCommands(args[0]);
   TestModeCatalog(args[0]);
   TestEditorLayout(args[0]);
   TestGeneralLayout(args[0]);
   TestSettingsSession(args[0]);
   TestInputFocus(args[0]);
   TestAuditUi(args[0]);
   using(var window=new ChatWindow(new[]{"--background"})) {
    var method=typeof(ChatWindow).GetMethod("BuildMenu",BindingFlags.Instance|BindingFlags.NonPublic);var menu=(ContextMenuStrip)method.Invoke(window,null);
    bool same=true;for(int i=0;i<30;i++)same&=Object.ReferenceEquals(menu,method.Invoke(window,null));
    Check(same&&!menu.IsDisposed&&menu.Items[menu.Items.Count-1].Text=="退出","persistent menu lifecycle and deferred exit retained");
    Check(menu.Items.Cast<ToolStripItem>().Any(i=>i.Text=="设置")&&menu.Items.Cast<ToolStripItem>().Any(i=>i.Text=="反馈")&&menu.Items.Count==6,"compact tray actions plus settings and feedback entries wired");Check(Object.ReferenceEquals(((NotifyIcon)Field(window,"tray")).ContextMenuStrip,menu)&&menu.AutoClose,"real tray icon owns the automatically dismissible menu");
    var button=(ChromeButton)Field(window,"mode");Check(button.AccessibleName.Length>0&&button.Text==button.AccessibleName,"size button has accessible name");
    var settingsButton=(ChromeButton)Field(window,"more");Check(settingsButton.Kind=="settings"&&settingsButton.AccessibleName=="设置","titlebar settings icon replaces dropdown trigger");
    var label=(PanelButton)Field(window,"chatModeButton");var minimize=(ChromeButton)Field(window,"minimize");var pin=(ChromeButton)Field(window,"pin");var sizeButton=(ChromeButton)Field(window,"mode");
    var closeButton=(ChromeButton)Field(window,"close");
    Check(window.Text=="DeepSeek-Reflex"&&label.Text=="普通对话"&&label.DropDown&&label.AccessibleName=="选择持续生效的对话模式","window identity is retained while its title area provides an accessible mode selector");
    Check(pin.Left<sizeButton.Left&&sizeButton.Right<label.Left&&label.Right<=minimize.Left&&minimize.Left<settingsButton.Left&&settingsButton.Left<closeButton.Left,"left controls hold pin and size while minimize, settings and close order on the right");
    Check(window.Region!=null&&!window.Region.IsVisible(1,1)&&window.Region.IsVisible(window.Width/2,1)&&window.Region.IsVisible(window.Width/2,window.Height-2),"real window region clips corners while retaining top and bottom edges");
    var outline=(WindowFrame)Field(window,"frame");Check(!outline.Region.IsVisible(window.Width/2,window.Height/2)&&outline.Region.IsVisible(window.Width/2,1),"outline covers border only and leaves website interaction open");
        Check(WindowFrame.DefaultZoom==0.9,"default browser scale reduced one step");
    // Render only the owned toolbar; never inspect or print website content.
    var header=(Panel)Field(window,"chrome");window.Controls.Remove(header);header.BackColor=Color.White;header.PerformLayout();
    using(var image=new Bitmap(header.Width,header.Height)){header.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(args[0],"toolbar-preview.png"));}window.Controls.Add(header);
    var chrome=(Panel)Field(window,"chrome");Check(chrome.Top>0&&(window.GetChildAtPoint(new Point(window.Width/2,1))==null||window.GetChildAtPoint(new Point(window.Width/2,1)) is WindowFrame),"top resize strip is not covered by child controls");
    using(var owner=new Form())using(var bindings=new HotkeyBindings(owner.Handle,K(Keys.F20),K(Keys.F21))) {
     ((HotkeyBindings)Field(window,"hotkeys")).Dispose();
     Check(bindings.Apply(K(Keys.F20),K(Keys.F21))==null,"replacement binding fixture owns its keyboard combinations before exercising save");
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
   Check(WindowFrame.Radius==12,"radius reduced to the Windows 11 window corner scale");
   using(var form=new Form()){form.FormBorderStyle=FormBorderStyle.None;form.ClientSize=new Size(410,616);using(var outline=new WindowFrame()){form.Controls.Add(outline);outline.UpdateShape(form,1);Check(form.Region!=null&&!form.Region.IsVisible(1,1)&&form.Region.IsVisible(WindowFrame.Radius,1),"custom window radius applies to full window");var hostRegion=form.Region;var ringRegion=outline.Region;int updates=outline.ShapeUpdates;for(int i=0;i<30;i++)outline.UpdateShape(form,1);Check(outline.ShapeUpdates==updates&&Object.ReferenceEquals(form.Region,hostRegion)&&Object.ReferenceEquals(outline.Region,ringRegion),"unchanged layout does not allocate or reinstall native regions");form.WindowState=FormWindowState.Maximized;outline.UpdateShape(form,1);Check(form.Region==null,"maximized window restores square corners");form.WindowState=FormWindowState.Normal;outline.UpdateShape(form,1);Check(form.Region!=null&&!form.Region.IsVisible(1,1),"restored window reapplies rounded shape");}}
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
    var high=new TrayMenu(2);using(high){high.AddAction("打开 DeepSeek 小窗",delegate{});high.AddAction("取消置顶",delegate{});high.AddDivider();high.AddAction("设置",delegate{});high.AddAction("退出",delegate{});high.PerformLayout();Console.WriteLine("low="+menu.Size+" row="+menu.Items[0].Size+" high="+high.Size+" row="+high.Items[0].Size);Check(high.Height>menu.Height&&high.Items[0].Height==menu.Items[0].Height*2,"menu row geometry respects two scaling factors");using(var bitmap=new Bitmap(high.Width,high.Height)){high.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[0],"tray-menu-preview-high-dpi.png"));}}
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
    Check(solid&&image.GetPixel(middle,stroke).ToArgb()==PanelTheme.Back.ToArgb(),"outline has one logical pixel of solid contrast with no extra band at scale "+scale);
    Check(WindowFrame.Outline.R<=180&&WindowFrame.Outline.G<=190,"outline remains visible against white at scale "+scale);
    // Antialiased coverage scales with the Win11 corner radius; measured
    // 19 @1x / 37 @2x for the 12 DIP arc, so require one partial pixel
    // per logical radius pixel.
    int region=(int)(WindowFrame.Radius*scale),partial=0;for(int y=0;y<region;y++)for(int x=0;x<region;x++){int alpha=image.GetPixel(x,y).A;if(alpha>0&&alpha<255)partial++;}
    Check(partial>=region,"outer curve has actual partial alpha coverage at scale "+scale);
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
      // Patch crop catches the arc asymmetrically; measured 12..19 @1x /
      // 27..37 @2x, so require half a partial pixel per radius pixel.
      Check(partialCorner>=region/2,"cached corner "+index+" retains fractional alpha at scale "+scale);
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
