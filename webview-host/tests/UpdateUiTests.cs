using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace DeepSeekFloat {
    internal static class UpdateUiTests {
        private static string root;private static int passed;
        private static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        private static void Pump(int ms=40){var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
        private static void Await(Task task){var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){if(watch.ElapsedMilliseconds>15000)throw new Exception("UI task timeout");Pump();}task.GetAwaiter().GetResult();Pump();}
        private static object Field(object obj,string name){return obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);}
        private static void Invoke(object obj,string name,params object[] args){obj.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);}
        private static System.Collections.Generic.IEnumerable<Control> Children(Control p){foreach(Control c in p.Controls){yield return c;foreach(var n in Children(c))yield return n;}}
        private sealed class Handoff:UpdateHandoff {
            internal bool Aborted,Live=true;internal Func<Task> Waiter;
            internal Handoff():base(null,null){Waiter=()=>Task.FromResult(0);}
            internal override bool Alive{get{return Live;}}
            internal override Task WaitReady(CancellationToken token){return Waiter();}
            internal override void Abort(){Aborted=true;}
        }
        private sealed class Backend:UpdateBackend {
            internal int Checks,Downloads,Prepares;internal bool FailCheck,FailDownload,FailPrepare,BlockDownload,BlockCheck;
            internal Version Latest=new Version(1,0,20);internal Handoff Prepared=new Handoff();
            internal override UpdateRelease Check(CancellationToken token){Checks++;if(BlockCheck){token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();}if(FailCheck)throw new IOException("check failure");return new UpdateRelease{Version=Latest,body="更清楚的设置布局\r\n• 保留配置与登录状态\r\n• 修复窗口位置与输入体验",Asset=new UpdateAsset{size=100,digest="sha256:"+new string('a',64)}};}
            internal override void Download(UpdateRelease r,string path,CancellationToken token,Action<long> progress){Downloads++;if(BlockDownload){token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();}if(FailDownload)throw new IOException("download failure");File.WriteAllText(path,"fixture");progress(100);}
            internal override void Verify(UpdateRelease r,string path){}
            internal override UpdateHandoff Prepare(UpdateRelease r,string path){Prepares++;if(FailPrepare)throw new IOException("prepare failed");return Prepared;}
        }
        private static UpdateCoordinator Coordinator(string name,Backend backend,Func<Task<string>> gate=null,Action<bool> freeze=null,Action exit=null) {
            return new UpdateCoordinator(gate??(()=>Task.FromResult<string>(null)),freeze??(v=>{}),exit??(()=>{}),backend,new Version(1,0,19),Path.Combine(root,name,"state.json"),Path.Combine(root,name,"cache"));
        }
        private static void StateMachine() {
            var b=new Backend();using(var c=Coordinator("states",b)) {
                Await(c.CheckAsync());Check(c.State==UpdateStatus.Available,"new version offered");b.FailDownload=true;Await(c.DownloadAsync());Check(c.State==UpdateStatus.DownloadFailed&&Directory.GetDirectories(Path.Combine(root,"states","cache")).Length==0,"failed download leaves retry and cleans task files");
                b.FailDownload=false;Await(c.DownloadAsync());Check(c.State==UpdateStatus.Ready,"retry reaches verified ready");Await(c.DownloadAsync());Check(b.Downloads==2,"duplicate download prevented");
                b.FailPrepare=true;Await(c.InstallAsync());Check(c.State==UpdateStatus.Ready,"preparation failure preserves candidate");
            }
            var old=new Backend{Latest=new Version(1,0,18)};using(var c=Coordinator("no-downgrade",old)){Await(c.CheckAsync());Await(c.DownloadAsync());Check(c.State==UpdateStatus.Current&&old.Downloads==0,"older public feed never downgrades");}
            var failed=new Backend{FailCheck=true};using(var c=Coordinator("retry-check",failed)){Await(c.CheckAsync());Check(c.State==UpdateStatus.CheckFailed,"check failure visible");failed.FailCheck=false;Await(c.CheckAsync());Check(c.State==UpdateStatus.Available,"check retry works");}
        }
        private static void Cancellation() {
            var b=new Backend{BlockDownload=true};using(var c=Coordinator("cancel",b)) {
                Await(c.CheckAsync());var task=c.DownloadAsync();var duplicate=c.DownloadAsync();Check(duplicate.IsCompleted&&b.Downloads<=1,"busy double click is inert");Pump(100);c.Cancel();Await(task);Check(c.State==UpdateStatus.Available,"cancel preserves working app");b.BlockDownload=false;Await(c.DownloadAsync());Check(c.State==UpdateStatus.Ready,"cancel then retry succeeds");
            }
            var check=new Backend{BlockCheck=true};using(var c=Coordinator("cancel-check",check)){var task=c.CheckAsync();Pump(80);c.Cancel();Await(task);Check(c.State==UpdateStatus.Idle,"cancel aborts check without error");}
        }
        private static void UnsavedGates() {
            foreach(int dirtyAt in new[]{1,2,3}) {
                int calls=0,exits=0;bool frozen=false;var b=new Backend();using(var c=Coordinator("dirty-"+dirtyAt,b,()=>Task.FromResult(++calls==dirtyAt?"未保存草稿":null),value=>frozen=value,()=>exits++)) {
                    Await(c.CheckAsync());Await(c.DownloadAsync());Await(c.InstallAsync());Check(c.State==UpdateStatus.Ready&&exits==0&&!frozen&&c.Message=="未保存草稿","dirty gate "+dirtyAt+" blocks exit and restores interaction");Check(b.Prepares==(dirtyAt==3?1:0),"dirty gate "+dirtyAt+" starts worker only after clean checks");if(dirtyAt==3)Check(b.Prepared.Aborted,"new draft during prepare aborts worker");
                }
            }
            var success=new Backend();int quit=0;bool freeze=false;using(var c=Coordinator("install",success,null,v=>freeze=v,()=>quit++)){Await(c.CheckAsync());Await(c.DownloadAsync());Await(c.InstallAsync());Check(quit==1&&freeze&&!success.Prepared.Aborted,"ready handoff requests normal exit once");Await(c.InstallAsync());Check(quit==1,"duplicate install prevented");}
            var dead=new Backend();dead.Prepared.Live=false;bool enabled=true;using(var c=Coordinator("dead-helper",dead,null,v=>enabled=!v)){Await(c.CheckAsync());Await(c.DownloadAsync());Await(c.InstallAsync());Check(enabled&&dead.Prepared.Aborted&&c.State==UpdateStatus.Ready,"dead helper prevents exit and restores controls");}
        }
        private static void AutoPolicy() {
            var now=DateTime.UtcNow;Check(UpdateCoordinator.Due(new UpdatePreferences(),now),"fresh auto check due");Check(!UpdateCoordinator.Due(new UpdatePreferences{Success=now.Ticks,Attempt=now.Ticks},now.AddHours(23)),"successful auto check waits daily");Check(!UpdateCoordinator.Due(new UpdatePreferences{Attempt=now.Ticks},now.AddMinutes(59)),"failed check backs off hour");Check(!UpdateCoordinator.Due(new UpdatePreferences{Auto=false},now),"disabled auto policy inert");
            var b=new Backend();using(var c=Coordinator("auto",b)){int notices=0;c.NewVersion+=v=>notices++;Await(c.CheckAsync(true));Await(c.CheckAsync(true));Check(b.Checks==1&&notices==1,"auto throttles and notifies once");c.SetAuto(false);Await(c.CheckAsync());Check(b.Checks==2&&!c.Auto,"manual check available when auto disabled");}
            using(var c=Coordinator("auto",b))Check(!c.Auto,"auto choice persists separately");
            string stateDir=Path.Combine(root,"state-write-denied");Directory.CreateDirectory(stateDir);var failState=new Backend();using(var c=new UpdateCoordinator(()=>Task.FromResult<string>(null),v=>{},()=>{},failState,new Version(1,0,19),stateDir,Path.Combine(root,"fail-state-cache"))){Await(c.CheckAsync(true));Check(failState.Checks==0,"automatic request blocked when throttle cannot persist");Await(c.CheckAsync());Check(failState.Checks==1,"manual works despite update-state write failure");}
        }
        private static void SettingsDrafts() {
            using(var owner=new Form{Bounds=new Rectangle(350,180,520,400)})using(var d=new SettingsDialog(WindowSettings.Defaults(),s=>null,a=>{})) {
                owner.Show();d.Show(owner);Pump();Check(d.UpdateBlockReason==null,"clean settings do not block update");((PanelSwitch)Field(d,"focus")).Checked=false;Check(d.UpdateBlockReason!=null,"general setting dirty blocks update");((PanelSwitch)Field(d,"focus")).Checked=true;
                Invoke(d,"ShowCommandsPage",true);((TextBox)Field(d,"commandBody")).Text="unsaved command";Check(d.UpdateBlockReason.Contains("指令"),"command editor draft detected");Invoke(d,"DiscardCommandEdits");Invoke(d,"ShowModesPage",true);((TextBox)Field(d,"modeBody")).Text="unsaved mode";Check(d.UpdateBlockReason.Contains("模式"),"mode editor draft detected");Invoke(d,"DiscardModeEdits");Invoke(d,"ShowGeneralPage");
                ((HotkeyBox)Field(d,"toggle")).Focus();Pump();Check(d.UpdateBlockReason.Contains("录入"),"hotkey recording blocks update");Invoke(d,"AttemptClose");
            }
        }
        private static void Dialogs() {
            foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})foreach(float scale in new[]{1f,1.5f,2f}) {
                PanelTheme.SetMode(theme);var b=new Backend();using(var c=Coordinator("ui-"+theme+scale,b))using(var owner=new Form{Bounds=new Rectangle(350,160,600,600)})using(var d=new UpdateDialog(c)) {
                    owner.Show();d.Show(owner);Pump();d.ApplyDpi(scale,new Rectangle(d.Left,d.Top,(int)(440*scale),(int)(448*scale)));Pump();
                    Check(d.BackColor==PanelTheme.Back&&((TextBox)Field(d,"notes")).ReadOnly,"shared theme and readonly notes "+theme+scale);
                    Await(c.CheckAsync());Check(((Button)Field(d,"action")).Text=="下载更新","clear primary update action "+theme+scale);
                    var notes=(TextBox)Field(d,"notes");notes.Focus();Pump();Check(notes.SelectionLength==0,"notes do not auto-select "+theme+scale);
                    using(var bitmap=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(bitmap,d.ClientRectangle);bitmap.Save(Path.Combine(root,"updates-"+theme+"-"+(int)(scale*100)+".png"));}
                    d.ClientSize=new Size((int)(340*scale),(int)(330*scale));Pump();var surface=(Panel)Field(d,"surface");var button=(Button)Field(d,"action");surface.ScrollControlIntoView(button);Pump();Check(!surface.HorizontalScroll.Visible&&surface.ClientRectangle.Contains(surface.RectangleToClient(button.RectangleToScreen(button.ClientRectangle))),"action reachable on short narrow window "+theme+scale);
                    var completion=new TaskCompletionSource<int>();b.Prepared.Waiter=()=>completion.Task;Await(c.DownloadAsync());var install=c.InstallAsync();Pump(100);Check(c.State==UpdateStatus.Preparing,"preparing state "+theme+scale);d.Close();Check(!d.IsDisposed,"close cannot discard in-flight installation "+theme+scale);b.Prepared.Live=false;completion.SetResult(0);Await(install);d.Close();
                }
            }PanelTheme.SetMode(UiTheme.Light);
        }
        private static string Script(CoreWebView2 core,string script){var task=core.ExecuteScriptAsync(script);Await(task);return task.Result.Trim('"');}
        private static string Gate(ChatWindow host){var task=(Task<string>)host.GetType().GetMethod("UpdateBlockReason",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(host,new object[0]);Await(task);return task.Result;}
        private static void RealWebViewAndWindow() {
            string site=Path.Combine(root,"offline-site");Directory.CreateDirectory(site);File.WriteAllText(Path.Combine(site,"index.html"),"<!doctype html><meta charset=utf-8><textarea id=draft></textarea><div id=rich contenteditable=true style='display:none'></div><textarea id=hidden style='display:none'>hidden</textarea><textarea readonly>readonly</textarea><textarea disabled>disabled</textarea>");
            GettingStarted.Acknowledge();var settings=WindowSettings.Defaults();settings.ToggleKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F22);settings.CaptureKeys=(int)(Keys.Control|Keys.Alt|Keys.Shift|Keys.F23);settings.Save();
            using(var host=new ChatWindow(new[]{"--background"})) {
                host.GetType().GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,true);host.GetType().GetField("allowShow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,true);host.Show();Pump();
                var view=(WebView2)Field(host,"browser");var envTask=CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"offline-profile"));Await(envTask);Console.WriteLine("REAL WEBVIEW runtime="+envTask.Result.BrowserVersionString+" controller=v1 (test environment lacks controller2)");
                var controllerTask=envTask.Result.CreateCoreWebView2ControllerAsync(host.Handle);Await(controllerTask);var controller=controllerTask.Result;var core=controller.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping("chat.deepseek.com",site,CoreWebView2HostResourceAccessKind.DenyCors);
                var navigation=new TaskCompletionSource<int>();core.NavigationCompleted+=delegate(object s,CoreWebView2NavigationCompletedEventArgs e){if(e.IsSuccess)navigation.TrySetResult(0);else navigation.TrySetException(new Exception("offline fixture navigation failed"));};
                core.Navigate("https://chat.deepseek.com/index.html");Await(navigation.Task);host.GetType().GetField("initializing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,false);
                Check(Script(core,ChatWindow.UpdateDraftScript)=="clean","real WebView ignores readonly disabled hidden fields");var cleanProbe=ChatWindow.ProbeUpdateDraft(core);Await(cleanProbe);Check(cleanProbe.Result==null,"clean real WebView passes production draft probe");
                Script(core,"document.querySelector('#draft').value='unsent fixture';");Check(Script(core,ChatWindow.UpdateDraftScript)=="dirty","real WebView textarea draft detected");var dirtyProbe=ChatWindow.ProbeUpdateDraft(core);Await(dirtyProbe);Check(dirtyProbe.Result.Contains("未发送"),"production draft probe blocks real textarea draft");
                Script(core,"document.querySelector('#draft').value='  ';");Check(Script(core,ChatWindow.UpdateDraftScript)=="dirty","real WebView whitespace remains unsaved");
                Script(core,"document.querySelector('#draft').style.display='none';document.querySelector('#rich').style.display='block';document.querySelector('#rich').textContent='rich draft';");Check(Script(core,ChatWindow.UpdateDraftScript)=="dirty","real WebView contenteditable draft detected");
                Script(core,"document.querySelector('#rich').textContent='';");Check(Script(core,ChatWindow.UpdateDraftScript)=="clean","real WebView empty contenteditable is clean");
                Script(core,"document.querySelector('#rich').style.display='none';");Check(Script(core,ChatWindow.UpdateDraftScript)=="unknown","real WebView missing editor is unknown");var missing=ChatWindow.ProbeUpdateDraft(core);Await(missing);Check(missing.Result!=null,"production draft probe blocks missing editor");
                Script(core,"document.querySelector('#draft').style.display='block';document.querySelector('#draft').value='';");
                Invoke(host,"ShowSettings");Pump();var dialog=(SettingsDialog)Field(host,"activeSettings");Invoke(host,"ShowUpdates");Pump();var update=(UpdateDialog)Field(host,"updateDialog");Invoke(host,"ShowUpdates");Check(ReferenceEquals(update,Field(host,"updateDialog")),"host update entry reuses one window");Check(!dialog.IsDisposed,"opening updater retains settings session");Check(Gate(host).Contains("尚未就绪"),"settings update and corner surfaces excluded while uninitialized WinForms fails closed");
                using(var feedback=new FeedbackDialog()){feedback.Show(host);Pump();Check(Gate(host).Contains("其它窗口"),"open feedback blocks actual host update gate");feedback.Close();}
                ((PanelSwitch)Field(dialog,"focus")).Checked=!settings.FocusOnOpen;Check(Gate(host).Contains("未保存"),"actual host detects general settings draft");((PanelSwitch)Field(dialog,"focus")).Checked=settings.FocusOnOpen;
                bool enabled=view.Enabled;var hotkeys=(HotkeyBindings)Field(host,"hotkeys");var availability=hotkeys.Availability;
                Invoke(host,"FreezeForUpdate",true);Check(host.ModalOpen&&!view.Enabled&&!dialog.Enabled&&hotkeys.Availability==HotkeyAvailability.None,"actual preparation freezes chat settings selection and global hotkeys");var frozenProbe=ChatWindow.ProbeUpdateDraft(core);Await(frozenProbe);Check(frozenProbe.Result==null,"real WebView draft probe works during host freeze");Invoke(host,"FreezeForUpdate",false);Check(view.Enabled==enabled&&dialog.Enabled&&hotkeys.Availability==availability,"failed preparation restores exact prior interaction scope");
                controller.Close();update.Close();dialog.Close();host.GetType().GetField("quitting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,true);host.Close();
            }
        }
        [STAThread] private static int Main(string[] args) {
            try{root=args[0];Directory.CreateDirectory(root);Directory.CreateDirectory(Preferences.Root);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var dispatch=new Form()){dispatch.Show();Application.DoEvents();StateMachine();Cancellation();UnsavedGates();AutoPolicy();SettingsDrafts();Dialogs();RealWebViewAndWindow();}Console.WriteLine("TOTAL "+passed+" update UI / lifecycle checks");return 0;}
            catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}
        }
    }
}
