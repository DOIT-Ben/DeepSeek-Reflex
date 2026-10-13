using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal static class WakeTests {
        private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Field(object value,string name){return value.GetType().GetField(name,Flags).GetValue(value);}
        private static void Set(object value,string name,object data){value.GetType().GetField(name,Flags).SetValue(value,data);}
        private static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Flags).Invoke(value,args);}
        private static void Pump(){var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<150){Application.DoEvents();System.Threading.Thread.Sleep(5);}}
        private static Point Anchor(Screen screen){return new Point(screen.WorkingArea.Left+screen.WorkingArea.Width/2,screen.WorkingArea.Top+screen.WorkingArea.Height/2);}
        private static void ShowAt(ChatWindow window,Screen screen){Call(window,"ShowChatAt",WakeTarget.At(Anchor(screen)));Pump();}
        private static bool Centered(ChatWindow window,Screen screen){var bounds=window.Bounds;var area=screen.WorkingArea;Console.WriteLine("Wake evidence: target="+screen.DeviceName+" work="+area+" actual="+bounds+" dpi="+Native.GetDpiForWindow(window.Handle));return area.Contains(bounds)&&Math.Abs(bounds.Left-(area.Left+(area.Width-bounds.Width)/2))<=2&&Math.Abs(bounds.Top-(area.Top+(area.Height-bounds.Height)/2))<=2;}
        internal static void Core(Action<bool,string> check){
            Rectangle left=new Rectangle(-1920,0,1920,1040),right=new Rectangle(0,40,2560,1400);
            check(WakePlacement.Center(new SizeF(410,616),1,left)==new Rectangle(-1165,212,410,616),"wake geometry centers on negative-coordinate work area");
            check(WakePlacement.Center(new SizeF(410,616),2,right)==new Rectangle(870,124,820,1232),"wake geometry preserves logical size at 200 percent");
            check(WakePlacement.Center(new SizeF(752,720),1.5f,left).Size==new Size(1128,1040),"reading size is clamped to available height before centering");
            var custom=new Rectangle(-1700,100,600,800);
            check(WakePlacement.Fit(custom,left)==custom,"same-screen fit preserves manual position and size");
            check(left.Contains(WakePlacement.Fit(new Rectangle(-2600,-300,3000,2400),left)),"disconnected or smaller monitor fit keeps the whole window visible");
            check(WakePlacement.Center(new SizeF(410,616),1.5f,new Rectangle(300,-900,900,600))==new Rectangle(442,-900,615,600),"vertical monitor work area is centered without covering its taskbar");
        }
        internal static void Desktop(Action<bool,string> check,string root){
            var screens=Screen.AllScreens.OrderBy(s=>s.Bounds.Left).ToArray();
            if(screens.Length<2){Console.WriteLine("SKIP physical dual-monitor wake checks: fewer than two connected displays");return;}
            Screen left=screens[0],right=screens[screens.Length-1];
            var settings=WindowSettings.Defaults();settings.ToggleKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F23);settings.CaptureKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F24);settings.ChatMode="translate";settings.Save();GettingStarted.Acknowledge();
            var previousForeground=Native.GetForegroundWindow();
            try{
             using(var window=new ChatWindow(new[]{"--background"})){
                Set(window,"initializing",true);Set(window,"allowShow",true);window.Text="DeepSeek-Reflex isolated wake acceptance";window.Show();Pump();
                Screen pointer=left;Set(window,"captureWakeTarget",new Func<WakeTarget>(delegate{return WakeTarget.At(Anchor(pointer));}));
                ShowAt(window,left);window.Bounds=WakePlacement.Fit(new Rectangle(left.WorkingArea.Left+40,left.WorkingArea.Top+40,window.Width,window.Height),left.WorkingArea);Pump();
                var original=window.Bounds;var browser=Field(window,"browser");float firstScale=WindowDpi.Scale(window);var logical=new SizeF(original.Width/firstScale,original.Height/firstScale);
                var missing=WakeTarget.At(Anchor(left));typeof(WakeTarget).GetField("device",Flags).SetValue(missing,"disconnected-fixture-display");check(missing.Resolve().DeviceName==left.DeviceName,"captured target with a missing display identity resolves to an available screen");
                pointer=right;Pump();check(window.Bounds==original,"moving the pointer target alone never moves the chat window");
                Call(window,"ToggleWindow");Pump();check(window.Visible&&window.WindowState==FormWindowState.Normal&&Centered(window,right),"visible chat on another screen relocates instead of toggling closed");
                var expected=WakePlacement.Center(logical,WindowDpi.Scale(window),right.WorkingArea);
                check(Math.Abs(window.Width-expected.Width)<=2&&Math.Abs(window.Height-expected.Height)<=2,"physical cross-DPI wake preserves logical window dimensions");
                check(Object.ReferenceEquals(browser,Field(window,"browser"))&&((WindowSettings)Field(window,"settings")).ChatMode=="translate","wake retains the existing WebView and chat mode");
                var smooth=(SmoothFrame)Field(window,"smoothFrame");check(smooth.Aligned&&smooth.Visible,"cross-screen wake retains aligned native rounded corners");
                var manual=WakePlacement.Fit(new Rectangle(right.WorkingArea.Left+26,right.WorkingArea.Top+32,window.Width,window.Height),right.WorkingArea);window.Bounds=manual;Pump();
                window.Hide();ShowAt(window,right);check(window.Bounds==manual,"same-screen tray-hidden wake preserves the last manual rectangle");
                Call(window,"ToggleWindow");Pump();check(window.WindowState==FormWindowState.Minimized,"visible same-screen toggle still minimizes by default");
                Call(window,"ToggleWindow");Pump();check(window.WindowState==FormWindowState.Normal&&window.Bounds==manual,"same-screen minimized wake restores the prior rectangle");
                pointer=left;Call(window,"ToggleWindow");Pump();check(Centered(window,left),"returning to a previously used screen recenters instead of restoring a per-screen history");
                Call(window,"SetPinned",true);pointer=right;Call(window,"ToggleWindow");Pump();check(window.TopMost&&Centered(window,right)&&smooth.Aligned,"pinned chat moves and remains pinned with aligned corners");Call(window,"SetPinned",false);
                Set(window,"settingsOpen",true);var modalBounds=window.Bounds;pointer=left;Call(window,"ToggleWindow");Pump();check(window.Bounds==modalBounds&&window.Visible,"settings modal routing does not relocate the owner");Set(window,"settingsOpen",false);
                Set(window,"guideOpen",true);Call(window,"ShowChat");Pump();check(window.Bounds==modalBounds,"help modal routing does not relocate the owner");Set(window,"guideOpen",false);
                var capture=WakeTarget.At(Anchor(left));pointer=right;
                ((Task)Call(window,"PresentSelectionAt",SelectionResult.Fail("隔离取词演示"),capture)).GetAwaiter().GetResult();Pump();check(Centered(window,left),"selection completion uses its captured target despite a later pointer change");
                window.Hide();pointer=right;var tray=(ContextMenuStrip)Call(window,"BuildMenu");((ToolStripMenuItem)tray.Items[0]).PerformClick();pointer=left;Pump();check(window.Visible&&Centered(window,right),"tray opening snapshots the target before queued UI work");
                window.Hide();pointer=left;Native.PostMessage(window.Handle,Native.OpenMessage,IntPtr.Zero,IntPtr.Zero);Pump();check(window.Visible&&Centered(window,left),"existing-instance open message uses unified wake positioning");
                window.WindowState=FormWindowState.Maximized;Pump();pointer=right;Call(window,"ToggleWindow");Pump();
                check(window.Visible&&window.WindowState==FormWindowState.Maximized&&Screen.FromHandle(window.Handle).DeviceName==right.DeviceName,"maximized chat moves to the target screen and stays maximized");
                window.WindowState=FormWindowState.Normal;Pump();check(Centered(window,right),"restoring a relocated maximized chat returns to its centered normal rectangle");
                var stored=File.ReadAllText(Path.Combine(Preferences.Root,"webview-bounds.txt"));check(stored.StartsWith(window.Left+","+window.Top+","),"wake stores the relocated normal position in the existing preference file");
                pointer=right;var liveSettings=(WindowSettings)Field(window,"settings");liveSettings.HideToTrayOnToggle=true;Call(window,"ToggleWindow");Pump();check(!window.Visible,"same-screen toggle retains the optional hide-to-tray setting");
                pointer=left;Call(window,"ToggleWindow");Pump();check(window.Visible&&Centered(window,left),"tray-hidden cross-screen toggle relocates and opens the chat");liveSettings.HideToTrayOnToggle=false;
                Call(window,"ShowChatAt",WakeTarget.At(Anchor(right)));
                typeof(Form).GetMethod("OnResizeBegin",Flags).Invoke(window,new object[]{EventArgs.Empty});
                var dragged=WakePlacement.Fit(new Rectangle(right.WorkingArea.Left+18,right.WorkingArea.Top+24,window.Width,window.Height),right.WorkingArea);window.Bounds=dragged;
                typeof(Form).GetMethod("OnResizeEnd",Flags).Invoke(window,new object[]{EventArgs.Empty});Pump();check(window.Bounds==dragged,"queued DPI centering never overwrites a subsequent manual drag");
                Call(window,"SwitchMode","compact");ShowAt(window,left);check(((WindowSettings)Field(window,"settings")).Mode=="compact"&&Centered(window,left),"compact mode survives an actual cross-screen wake");
                Call(window,"SwitchMode","reading");ShowAt(window,right);check(((WindowSettings)Field(window,"settings")).Mode=="reading"&&Centered(window,right),"reading mode survives an actual cross-screen wake");
                if(Native.GetForegroundWindow()==window.Handle)check(window.ContainsFocus,"wake activates the isolated chat window and gives it keyboard focus");else Console.WriteLine("SKIP physical foreground focus: Windows denied foreground activation");
                using(var image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,window.ClientRectangle);image.Save(Path.Combine(root,"wake-native-window.png"));}
                Set(window,"quitting",true);window.Close();Pump();
             }
             foreach(bool changeScreen in new[]{true,false})using(var startup=new ChatWindow(new string[0])){
                Set(startup,"initializing",true);Set(startup,"startupTarget",WakeTarget.At(Anchor(changeScreen?right:left)));
                var initial=new Rectangle(left.WorkingArea.Left+40,left.WorkingArea.Top+40,820,1232);startup.Bounds=WakePlacement.Fit(initial,left.WorkingArea);initial=startup.Bounds;
                startup.Show();Pump();
                check(changeScreen?Centered(startup,right):startup.Bounds==initial,changeScreen?"foreground startup centers on the captured different display":"foreground startup on the same display retains its last rectangle");
                Set(startup,"quitting",true);startup.Close();Pump();
             }
            }finally{if(previousForeground!=IntPtr.Zero)Native.SetForegroundWindow(previousForeground);}
        }
    }
}
