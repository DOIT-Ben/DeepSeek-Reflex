using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// Load the built application itself. This driver owns its native window and all profile data.
internal static class ClientSwitchTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Ids = {"deepseek","doubao","qianwen","kimi"};
    static readonly string[] Labels = {"DeepSeek","豆包","千问","Kimi"};
    static readonly string[] Hosts = {"chat.deepseek.com","www.doubao.com","www.qianwen.com","www.kimi.com"};
    static readonly string[] Editors = {"<textarea id='chat-input'></textarea>","<textarea data-testid='chat_input_input'></textarea>","<div contenteditable='true' role='textbox' data-slate-editor='true' data-placeholder='向千问提问'><p><span data-slate-zero-width='n'>&#xfeff;</span><span contenteditable='false' data-slate-placeholder='true'>向千问提问</span></p></div>","<div class='chat-input-editor' contenteditable='true' role='textbox'></div>"};
    static readonly string[] LiveEditors = {"#chat-input","textarea[data-testid=chat_input_input]","[role=textbox][contenteditable=true][data-slate-editor=true]",".chat-input-editor[role=textbox]"};
    static string root;
    static bool fixture;
    static Form host;
    static Assembly candidate;
    static int checks;
    static object Field(object obj,string key) { return obj.GetType().GetField(key,Fields).GetValue(obj); }
    static object Call(object obj,string key,params object[] args) { return obj.GetType().GetMethod(key,Fields).Invoke(obj,args); }
    static void Check(bool ok,string detail) { if(!ok) throw new InvalidOperationException(detail); Console.WriteLine("PASS "+detail);checks++; }
    static WebView2 Browser { get { return (WebView2)Field(host,"browser"); } }
    static int Views { get { return host.Controls.OfType<WebView2>().Count(); } }
    static void Pump(int ms) { var clock=Stopwatch.StartNew(); do{Application.DoEvents();Thread.Sleep(10);}while(clock.ElapsedMilliseconds<ms); }
    static void Until(Func<bool> condition,string name,int seconds=55)
    {
        var clock=Stopwatch.StartNew();
        while(!condition()){
            if(clock.Elapsed.TotalSeconds>seconds)throw new TimeoutException(name+" ("+((Label)Field(host,"status")).Text+")");
            Pump(35);
        }
    }
    static T Await<T>(Task<T> task) { Until(()=>task.IsCompleted,"WebView response");return task.GetAwaiter().GetResult(); }
    static string Eval(string script) { return Await(Browser.CoreWebView2.ExecuteScriptAsync(script)); }
    static void SecureFixture(WebView2 view)
    {
        view.CoreWebView2InitializationCompleted+=delegate {
            var core=view.CoreWebView2;
            if(core==null)return;
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            core.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs e){
                var uri=new Uri(e.Request.Uri);int index=Array.IndexOf(Hosts,uri.Host);
                string html="", extra="";
                if(index>=0){
                    string cookie="reflex_test_login="+Ids[index];
                    bool logged=e.Request.Headers.Contains("Cookie")&&e.Request.Headers.GetHeader("Cookie").Split(';').Any(v=>v.Trim()==cookie);
                    if(uri.AbsolutePath=="/reflex-login"){
                        extra="Set-Cookie: "+cookie+"; Path=/; Max-Age=3600; Secure; HttpOnly; SameSite=Lax\r\n";
                        html="<script>location.replace('"+(index==1?"/chat/":"/")+"')</script>";
                    }else html="<body data-auth='"+(logged?"yes":"no")+"'><button id='sign' onclick=\"location.assign('/reflex-login')\">登录</button>"+Editors[index]+"</body>";
                }
                e.Response=core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8>"+html)),200,"OK","Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\n"+extra);
            };
        };
    }
    static void ClickClient(int index)
    {
        var button=(Button)Field(host,"clientButton");
        // PerformClick opens this native menu synchronously. Inspect and invoke its own item
        // before yielding to unrelated activation messages on the non-input test desktop.
        button.PerformClick();
        var menu=(ContextMenuStrip)Field(host,"openMenu");
        bool listed=menu!=null&&menu.Visible&&menu.Items.Cast<ToolStripMenuItem>().Select(i=>i.Text).SequenceEqual(Labels);
        Check(listed,"native client menu lists all four choices"+(listed?"":" (visible="+host.Visible+", enabled="+button.Enabled+", selectable="+button.CanSelect+", items="+(menu==null?-1:menu.Items.Count)+")"));
        var item=(ToolStripMenuItem)menu.Items[index];
        // Activate the native menu item's real accessibility action, like a keyboard selection.
        item.PerformClick();if(!menu.IsDisposed)menu.Close();
        Until(()=>button.Text==Labels[index],"selected client "+Ids[index]);
        Ready(index);
    }
    static void Ready(int index)
    {
        Until(()=>{
            var core=Browser.CoreWebView2;
            Uri uri;
            return core!=null&&Uri.TryCreate(core.Source,UriKind.Absolute,out uri)&&uri.Host==Hosts[index]&&
                (bool)Field(Field(host,"health"),"navigationSucceeded")&&
                Eval("document.readyState==='complete' && !!document.body")=="true";
        },"official origin and completed document for "+Ids[index]);
    }
    static void Stats(string step,long elapsed)
    {
        var core=Browser.CoreWebView2;
        var info=core.Environment.GetProcessInfos();
        long webBytes=0;
        foreach(var item in info)try{using(var p=Process.GetProcessById(item.ProcessId))webBytes+=p.PrivateMemorySize64;}catch(ArgumentException){}
        using(var own=Process.GetCurrentProcess())Console.WriteLine("SAMPLE "+step+" browserRoots="+info.Count(p=>p.Kind==CoreWebView2ProcessKind.Browser)+" webProcesses="+info.Count+" views="+Views+" hostPrivateMiB="+(own.PrivateMemorySize64/1048576)+" webPrivateMiB="+(webBytes/1048576)+" readyMs="+elapsed);
    }
    static void LoginEntry(int index)
    {
        const string form=@"[...document.querySelectorAll('input[type=tel],input[type=email],input[autocomplete=one-time-code],iframe[src*=passport],iframe[src*=login]')].some(e=>e.getClientRects().length&&getComputedStyle(e).visibility==='visible') || /验证码|手机号登录|手机号码|使用豆包或飞书账号登录|扫码登录|扫描.*二维码|账号密码登录|邮箱登录|phone number|verification code|email address/i.test(document.body.innerText)";
        const string openLogin=@"(()=>{
const label=/^(登录|登录\s*[/／或]?\s*注册|立即登录|Log in|Sign in)$/i;
const match=e=>e.getClientRects().length&&getComputedStyle(e).visibility==='visible'&&
    [e.innerText,e.getAttribute('aria-label'),e.title].some(t=>label.test((t||'').trim()));
const entry=[...document.querySelectorAll('button,a,[role=button]')].find(match)||
    [...document.querySelectorAll('span,div')].find(e=>!e.childElementCount&&match(e));
if(!entry)return false;
(entry.closest('button,a,[role=button]')||entry).click();return true;
})()";
        // The guest editor can be ready before the provider mounts its sign-in control.
        bool clicked=false;
        Until(()=>{
            if(Eval(form)=="true")return true;
            clicked=Eval(openLogin)=="true";
            return clicked;
        },"official sign-in entry or form for "+Ids[index]);
        if(clicked)Check(true,"real sign-in entry opens for "+Ids[index]);
        Until(()=>Eval(form)=="true","visible official authentication UI for "+Ids[index]);
        Check(true,Ids[index]+" official sign-in form reached");
    }
    static void Live(int first)
    {
        var clock=Stopwatch.StartNew();Ready(first);var elapsed=clock.ElapsedMilliseconds;
        uint rootPid=Browser.CoreWebView2.BrowserProcessId;
        foreach(var index in new[]{first,1,2,3,0}){
            if(index!=first||checks>2){clock.Restart();ClickClient(index);elapsed=clock.ElapsedMilliseconds;}
            string selector=LiveEditors[index];
            Until(()=>Eval("!!document.querySelector('"+selector+"') || (/登录|Log in|Sign in/i.test(document.body.innerText)&&document.body.innerText.trim().length>5)")=="true","live login or chat UI for "+Ids[index]);
            var facts=Eval("({title:document.title,hasEditor:!!document.querySelector('"+selector+"'),login:/登录|Log in|Sign in/i.test(document.body.innerText)})");
            Check(facts.Contains("\"login\":true")||facts.Contains("\"hasEditor\":true"),Ids[index]+" real website "+facts);
            Check(rootPid==Browser.CoreWebView2.BrowserProcessId,"all live clients share one WebView2 browser process");
            Until(()=>Views==1,"release clean live page for "+Ids[index]);
            Stats(Ids[index],elapsed);
            LoginEntry(index);
            if(index!=0){
                var core=Browser.CoreWebView2;
                var navigation=new TaskCompletionSource<bool>();
                EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=null;
                completed=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e){
                    if(e.WebErrorStatus==CoreWebView2WebErrorStatus.OperationCanceled)return;
                    core.NavigationCompleted-=completed;navigation.TrySetResult(e.IsSuccess);
                };
                core.NavigationCompleted+=completed;
                core.Navigate("https://"+Hosts[index]+(index==1?"/chat/":"/"));
                Check(Await(navigation.Task),"official home opens again after login dialog for "+Ids[index]);
            }
        }
    }
    static void CheckMenuAndLayout()
    {
        var button=(Button)Field(host,"clientButton");
        var mode=(Control)Field(host,"chatModeButton");
        var small=(Control)Field(host,"mode");
        var minimize=(Control)Field(host,"minimize");
        var bounds=host.Bounds;
        foreach(float scale in new[]{1f,1.5f,2f})foreach(int width in new[]{360,410,752}){
            Call(host,"ApplyDpi",scale,new System.Drawing.Rectangle(bounds.X,bounds.Y,(int)(width*scale),(int)(616*scale)));
            Check(small.Right<=button.Left && button.Right<=mode.Left && mode.Right<=minimize.Left,"two native selectors fit width "+width+" at scale "+scale);
        }
        Call(host,"ApplyDpi",1f,bounds);
        button.PerformClick();Pump(25);
        var menu=(ContextMenuStrip)Field(host,"openMenu");
        Check(menu!=null&&menu.Visible,"client menu visible from its title button");
        Call(host,"OnOutsideMenuPress",button.PointToScreen(new System.Drawing.Point(4,4)));
        Pump(30);button.PerformClick();Pump(30);
        Check(Field(host,"openMenu")==null,"pressing the open client button dismisses it without reopening");
        Pump(360);
    }
    static void VerifyAuth(int i,bool expected)
    {
        string state=expected?"yes":"no";
        Until(()=>Eval("document.body.dataset.auth")=="\""+state+"\"","fixture session "+Ids[i]+" "+state);
        Check(true,Ids[i]+(expected?" authenticated fixture reopened":" independent first login"));
    }
    static void SeedLegacyProfile()
    {
        using(var shell=new Form())
        using(var view=new WebView2()){
            shell.Controls.Add(view);shell.Show();
            var environment=Await(CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"webview2-profile")));
            var ready=view.EnsureCoreWebView2Async(environment);
            Until(()=>ready.IsCompleted,"old default profile initializes");ready.GetAwaiter().GetResult();
            var cm=view.CoreWebView2.CookieManager;
            var c=cm.CreateCookie("reflex_legacy","from-v1","chat.deepseek.com","/");
            c.Expires=DateTime.UtcNow.AddHours(1);cm.AddOrUpdateCookie(c);
            Check(Await(cm.GetCookiesAsync("https://chat.deepseek.com")).Any(k=>k.Name=="reflex_legacy"),"original single-client profile seeded");
        }
    }
    static void Fixture(bool restart)
    {
        int first=restart?3:0;
        Ready(first);uint pid=Browser.CoreWebView2.BrowserProcessId;
        Check(((Button)Field(host,"clientButton")).Text==Labels[first],"startup restores expected client "+Ids[first]);
        if(!restart)CheckMenuAndLayout();
        var profiles=new HashSet<string>();
        foreach(var index in (restart?new[]{3,0,1,2}:new[]{0,1,2,3})){
            if(index!=first)ClickClient(index);
            var core=Browser.CoreWebView2;
            Check(core.BrowserProcessId==pid&&profiles.Add(core.Profile.ProfileName),"isolated persistent profile on shared browser for "+Ids[index]);
            VerifyAuth(index,restart);
            if(index==0)Check(Await(core.CookieManager.GetCookiesAsync("https://chat.deepseek.com")).Any(c=>c.Name=="reflex_legacy"&&c.Value=="from-v1"),"upgrade reuses existing DeepSeek sign-in data in place");
            var cm=core.CookieManager;
            var common=Await(cm.GetCookiesAsync("https://reflex-test.invalid")).Where(c=>c.Name=="reflex_fixture").ToArray();
            if(!restart){
                Check(common.Length==0,"shared test domain starts empty in "+Ids[index]);
                var c=cm.CreateCookie("reflex_fixture",Ids[index],"reflex-test.invalid","/");c.Expires=DateTime.UtcNow.AddHours(1);cm.AddOrUpdateCookie(c);
                Eval("document.querySelector('#sign').click()");VerifyAuth(index,true);
            }else Check(common.Length==1&&common[0].Value==Ids[index],"same-domain QA cookie belongs solely to "+Ids[index]);
        }
        if(restart)return;
        ClickClient(1);
        var popupSource=Browser;
        Eval("location.assign('https://reflex-test.invalid/authorize')");
        Until(()=>host.OwnedForms.Any(f=>f.GetType().Name=="PopupWindow"),"native external sign-in popup");
        var popup=host.OwnedForms.First(f=>f.GetType().Name=="PopupWindow");
        var popupView=(WebView2)Field(popup,"Browser");
        Until(()=>popupView.CoreWebView2!=null && popupView.CoreWebView2.Source.StartsWith("https://reflex-test.invalid/"),"popup reaches test identity origin");
        Check(popupView.CoreWebView2.Profile.ProfileName==Browser.CoreWebView2.Profile.ProfileName,"login popup uses selected client's profile");
        popupView.CoreWebView2.Navigate("https://www.doubao.com/chat/?reflex-fixture=1");
        Until(()=>popup.IsDisposed && popupSource.CoreWebView2.Source.Contains("reflex-fixture=1"),"sign-in callback returns to its own client");
        Until(()=>Eval("!!document.querySelector('textarea')")=="true","fixture callback loaded");
        Check(!File.ReadAllText(Path.Combine(root,"webview-last-page-doubao.txt")).Contains("?"),"authentication callback parameters stay out of saved paths");
        host.Activate();Pump(400); // Let native popup deactivation finish before invoking the next menu item.
        Eval("document.querySelector('textarea').value='QA 未发送草稿';document.querySelector('textarea').dispatchEvent(new Event('input',{bubbles:true}));");
        var draftView=Browser;
        ClickClient(2);
        Until(()=>draftView.CoreWebView2.IsSuspended,"draft renderer suspended");
        Check(Views==2,"only selected view and suspended draft remain");
        ClickClient(1);
        Check(Browser==draftView&&!Browser.CoreWebView2.IsSuspended&&Eval("document.querySelector('textarea').value")=="\"QA 未发送草稿\"","return resumes the original unsent draft");
        Eval("document.querySelector('textarea').value='';");
        ClickClient(3);
        Until(()=>Views==1,"release cleared draft");
        Check(File.ReadAllText(Path.Combine(root,"chat-client.txt"))=="kimi","last client persisted for next process");
        IdleChecks();
    }

    static void IdleChecks()
    {
        // Keep the production timer and window events. Only shorten its duration in the fixture.
        var timerField=host.GetType().GetField("idleTimer",Fields);
        if(timerField!=null){
            var timer=(System.Windows.Forms.Timer)timerField.GetValue(host);
            Check(timer.Interval==60000,"one minute of grace before discarding a clean home page");
            timer.Interval=550;
        }
        var original=Browser;
        var native=candidate.GetType("DeepSeekFloat.Native");
        var post=native.GetMethod("PostMessage",Static);
        Action toggle=delegate {post.Invoke(null,new object[]{host.Handle,0x312,new IntPtr(1),IntPtr.Zero});};
        toggle();Until(()=>!host.Visible||host.WindowState==FormWindowState.Minimized,"native hotkey message hides or minimizes the panel",2);
        Check(original.CoreWebView2.MemoryUsageTargetLevel==CoreWebView2MemoryUsageTargetLevel.Low,"hidden page runs with the low memory target");
        toggle();Until(()=>host.Visible&&host.WindowState!=FormWindowState.Minimized,"native hotkey message restores the panel",2);Pump(750);
        Check(Browser==original&&!original.IsDisposed,"quick wake cancels reclamation and preserves the active renderer");
        Call(host,"OnActivated",EventArgs.Empty);
        Check(Browser.CoreWebView2.MemoryUsageTargetLevel==CoreWebView2MemoryUsageTargetLevel.Normal,"activation restores normal rendering");

        Eval("sessionStorage.setItem('qa-tab','tab session');document.cookie='qa_session=current; path=/; Secure'");
        host.WindowState=FormWindowState.Minimized;
        Until(()=>original.CoreWebView2.Source=="https://idle.reflex.invalid/"&&original.CoreWebView2.IsSuspended,"minimized clean home released",5);
        Check(!original.Visible,"empty idle tab is hidden and suspended");
        host.WindowState=FormWindowState.Normal;Ready(3);VerifyAuth(3,true);
        Check(Browser==original&&Browser.CoreWebView2.Profile.ProfileName=="reflex-kimi","ordinary taskbar restore reopens the signed-in client");
        Check(Eval("sessionStorage.getItem('qa-tab')==='tab session'&&document.cookie.includes('qa_session=current')")=="true","idle reclaim preserves session-only cookies and per-tab sessionStorage");

        int depth=int.Parse(Eval("history.length"));
        EventHandler<CoreWebView2SourceChangedEventArgs> wakeDuringParking=null;
        wakeDuringParking=delegate {
            if (original.CoreWebView2.Source!="https://idle.reflex.invalid/") return;
            original.CoreWebView2.SourceChanged-=wakeDuringParking;
            host.BeginInvoke(new Action(delegate {Call(host,"ShowChat");}));
        };
        original.CoreWebView2.SourceChanged+=wakeDuringParking;
        host.Hide();Until(()=>host.Visible,"wake while the idle document is arriving",5);Ready(3);Pump(350);
        Check(!original.CoreWebView2.IsSuspended&&original.Visible&&int.Parse(Eval("history.length"))==depth,
            "racing wake restores the live client without growing history");

        var protectedView=Browser;
        Eval("document.querySelector('[contenteditable]').textContent='draft without an input event'");
        host.Hide();Pump(850);Call(host,"ShowChat");Ready(3);
        Check(Browser==protectedView&&Eval("document.querySelector('[contenteditable]').textContent")=="\"draft without an input event\"","background grace preserves a draft added by the website");
        Eval("document.querySelector('[contenteditable]').textContent='';const d=document.createElement('dialog');document.body.append(d);d.showModal()");
        host.Hide();Pump(850);Call(host,"ShowChat");Ready(3);
        Check(Browser==protectedView&&Eval("!!document.querySelector('dialog[open]')")=="true","an active sign-in dialog survives backgrounding");
        Eval("document.querySelector('dialog').remove();history.pushState({},'','/c/qa-conversation');window.__qaTicks=0;window.__qaInterval=setInterval(()=>window.__qaTicks++,100)");
        host.Hide();Pump(850);
        Console.WriteLine("BACKGROUND hostVisible="+host.Visible+" suspended="+protectedView.CoreWebView2.IsSuspended+" document="+Eval("({visibility:document.visibilityState,ticks:window.__qaTicks})"));
        Check(Eval("document.hidden")=="true","background chat receives Chromium's hidden-page throttling");
        Until(()=>!host.Visible&&!protectedView.CoreWebView2.IsSuspended&&Eval("window.__qaTicks>0")=="true","conversation work completes while the host is still hidden",8);
        Call(host,"ShowChat");Ready(3);
        Check(Eval("document.visibilityState==='visible'")=="true","wake restores Chromium's visible-page behavior");
        Check(Browser==protectedView&&Eval("clearInterval(window.__qaInterval);window.__qaTicks>0")=="true","existing conversation keeps its page and background work");
        Eval("history.replaceState({},'','/');document.dispatchEvent(new Event('input',{bubbles:true}))");
        host.Hide();Pump(850);Call(host,"ShowChat");Ready(3);
        Check(Browser==protectedView,"a previously used page keeps its in-progress session");
    }
    [STAThread] static int Main(string[] args)
    {
        try{
            root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);fixture=args[2]!="live";
            candidate=Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var type=candidate.GetType("DeepSeekFloat.ChatWindow",true);
            Check(type.GetField("clientButton",Fields)!=null,"candidate contains the native client selector");
            candidate.GetType("DeepSeekFloat.Preferences").GetField("Root",Static).SetValue(null,root);
            candidate.GetType("DeepSeekFloat.GettingStarted").GetMethod("Acknowledge",Static).Invoke(null,null);
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            host=(Form)Activator.CreateInstance(type,Fields,null,new object[]{new[]{"--background"}},null);
            var handle=host.Handle;Pump(120);
            Check(Browser.CoreWebView2==null,"tray-only startup creates no browser session");
            if(fixture){
                SecureFixture(Browser);
                host.ControlAdded+=delegate(object s,ControlEventArgs e){var view=e.Control as WebView2;if(view!=null)SecureFixture(view);};
            }
            if(args[2]=="login")SeedLegacyProfile();
            var wake=Stopwatch.StartNew();Call(host,"ShowChat");
            Console.WriteLine("NATIVE visible="+host.Visible+" wakeMs="+wake.ElapsedMilliseconds);
            if(fixture)Fixture(args[2]=="restart");else Live(0);
            Console.WriteLine("TOTAL "+checks+" "+args[2]+" desktop checks");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{if(host!=null){host.GetType().GetField("quitting",Fields).SetValue(host,true);host.Close();host.Dispose();}Pump(150);}
    }
}
