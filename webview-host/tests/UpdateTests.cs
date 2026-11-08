using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
namespace DeepSeekFloat {
    internal static class UpdateTests {
        private static string root,oldDir,newDir,package;private static int passed,failed;
        private static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+" "+e);}}
        private static void Reject(Action action){bool rejected=false;try{action();}catch(Exception){rejected=true;}Assert(rejected,"invalid input accepted");}
        private static void Wait(Func<bool> condition,string message,int ms=12000){var sw=Stopwatch.StartNew();while(!condition()){if(sw.ElapsedMilliseconds>ms)throw new Exception(message);Thread.Sleep(40);}}
        private static string Metadata(string tag="v1.0.20",string extra="") {return "{\"tag_name\":\""+tag+"\",\"draft\":false,\"prerelease\":false,\"body\":\"notes\",\"assets\":[{\"name\":\"DeepSeek-Reflex-1.0.20-Windows-x64.zip\",\"browser_download_url\":\"https://github.com/DOIT-Ben/DeepSeek-Reflex/releases/download/"+tag+"/DeepSeek-Reflex-1.0.20-Windows-x64.zip\",\"size\":123,\"digest\":\"sha256:"+new string('a',64)+"\"}]"+extra+"}";}
        private static UpdateRelease Parse(string json){return UpdateFeed.Parse(Encoding.UTF8.GetBytes(json));}
        private static string Zip(string name,Action<Dictionary<string,byte[]>> mutate=null) {
            var files=new Dictionary<string,byte[]>();foreach(var n in UpdatePackage.Expected)if(n!="SHA256SUMS.txt")files[n]=n=="DeepSeek-Reflex.exe"?File.ReadAllBytes(Path.Combine(newDir,n)):Encoding.UTF8.GetBytes(n=="VERSION"?"1.0.20":n.EndsWith(".config")?"<configuration/>":"new:"+n);
            string hashes=string.Join("\n",files.Select(x=>{using(var s=new MemoryStream(x.Value))return UpdateFeed.Hash(s)+"  "+x.Key;}));files["SHA256SUMS.txt"]=Encoding.UTF8.GetBytes(hashes+"\n");if(mutate!=null)mutate(files);
            string path=Path.Combine(root,name+".zip");using(var stream=File.Create(path))using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))foreach(var file in files){var entry=zip.CreateEntry("DeepSeek-Reflex/"+file.Key);using(var s=entry.Open())s.Write(file.Value,0,file.Value.Length);}return path;
        }
        private static void Extract(string path,string name,Version version=null){UpdatePackage.Extract(path,Path.Combine(root,name),version??new Version(1,0,20),UpdateFeed.Hash(path));}
        private static byte[] Http(string suffix,Action<NetworkStream,CancellationToken> server,CancellationToken token,long max=1024) {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
            var task=Task.Run(delegate{try{using(var client=listener.AcceptTcpClient())using(var s=client.GetStream()){byte[] b=new byte[4096];s.Read(b,0,b.Length);server(s,token);}}catch(IOException){}catch(ObjectDisposedException){}finally{listener.Stop();}});
            try{return UpdateFeed.Read("http://127.0.0.1:"+port+suffix,max,token,null,null,uri=>(HttpWebRequest)WebRequest.Create(uri));}finally{listener.Stop();try{task.Wait(3000);}catch(AggregateException){}}
        }
        private static void Response(NetworkStream stream,string headers,string body){byte[] b=Encoding.ASCII.GetBytes("HTTP/1.1 "+headers+"\r\nConnection: close\r\n\r\n"+body);stream.Write(b,0,b.Length);}
        private static void DownloadBody(string name,string body,long size,string hash,CancellationToken token,Action<long> progress=null) {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
            var task=Task.Run(delegate{try{using(var client=listener.AcceptTcpClient())using(var s=client.GetStream()){byte[] b=new byte[4096];s.Read(b,0,b.Length);Response(s,"200 OK\r\nContent-Length: "+body.Length,body);}}catch(IOException){}finally{listener.Stop();}});
            try{var r=Parse(Metadata());r.Asset.size=size;r.Asset.digest="sha256:"+hash;UpdateFeed.Download(r,Path.Combine(root,name),token,progress,uri=>(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+"/asset"));}finally{listener.Stop();task.Wait(3000);}
        }
        private static string FreshTarget(string scenario) {
            string dir=Path.Combine(root,scenario);Directory.CreateDirectory(dir);foreach(string name in UpdatePackage.Managed){if(name=="DeepSeek-Reflex.exe")File.Copy(Path.Combine(oldDir,name),Path.Combine(dir,name));else File.WriteAllText(Path.Combine(dir,name),name.EndsWith(".config")?"<configuration/>":name=="VERSION"?"1.0.19":"old:"+name);}
            File.WriteAllText(Path.Combine(dir,"README.md"),"user-readme");Directory.CreateDirectory(Path.Combine(dir,"docs"));File.WriteAllText(Path.Combine(dir,"docs","notes.txt"),"user-doc");File.WriteAllText(Path.Combine(dir,"uninstall.exe"),"user-uninstall");return dir;
        }
        private static void UserFiles(string dir){Assert(File.ReadAllText(Path.Combine(dir,"README.md"))=="user-readme"&&File.ReadAllText(Path.Combine(dir,"docs","notes.txt"))=="user-doc"&&File.ReadAllText(Path.Combine(dir,"uninstall.exe"))=="user-uninstall","unmanaged files changed");}
        private sealed class Session:IDisposable {
            internal string Target,Path;internal UpdatePlan Plan;internal Process Parent,Worker;
            internal Session(string scenario,string hook=null) {
                Target=FreshTarget(scenario);Parent=UpdateTransaction.Start(System.IO.Path.Combine(Target,"DeepSeek-Reflex.exe"),"--parent");
                Wait(()=>File.Exists(System.IO.Path.Combine(Target,"parent-ready")),"parent not ready");
                Plan=UpdateTransaction.CreatePlan(package,"1.0.20",UpdateFeed.Hash(package),Target,oldDir);Plan.Parent=Parent.Id;Plan.ParentStart=Parent.StartTime.ToUniversalTime().Ticks;Path=UpdateTransaction.PlanPath(Plan);UpdateTransaction.Write(Path,Plan);
                if(hook!=null)File.WriteAllText(UpdateTransaction.Signal(Path,"hook.txt"),hook);
            }
            internal void Begin(){Worker=UpdateTransaction.Launch(Plan);}
            internal void Ready(){Wait(()=>File.Exists(UpdateTransaction.Signal(Path,"ready"))||Worker.HasExited,"worker readiness timeout");Assert(!Worker.HasExited,"worker failed preparation: "+(File.Exists(UpdateTransaction.Signal(Path,"result"))?UpdateTransaction.Read<string>(UpdateTransaction.Signal(Path,"result")):"no result"));}
            internal void Exit(){File.WriteAllText(System.IO.Path.Combine(Target,"exit-parent"),"exit");Assert(Parent.WaitForExit(5000),"parent exit");}
            internal void Done(){Assert(Worker.WaitForExit(15000),"worker timeout");}
            internal void Old(){foreach(string name in UpdatePackage.Managed)Assert(UpdateFeed.Hash(System.IO.Path.Combine(Target,name))==UpdateFeed.Hash(name=="DeepSeek-Reflex.exe"?System.IO.Path.Combine(oldDir,name):System.IO.Path.Combine(UpdateTransaction.TransactionRoot(Plan),"backup",name)),"old file not restored: "+name);UserFiles(Target);}
            public void Dispose(){if(Parent!=null){if(!Parent.HasExited){File.WriteAllText(System.IO.Path.Combine(Target,"exit-parent"),"exit");Parent.WaitForExit(2000);}Parent.Dispose();}if(Worker!=null){if(!Worker.HasExited){Worker.Kill();Worker.WaitForExit();}Worker.Dispose();}}
        }
        private static int Main(string[] args) {
            root=args[0];oldDir=args[1];newDir=args[2];Directory.CreateDirectory(root);
            if(args.Length>3&&args[3]=="--live") {
                Test("live GitHub latest + asset digest",delegate{var r=args.Length>4?UpdateFeed.Parse(File.ReadAllBytes(args[4])):UpdateFeed.Check(CancellationToken.None);Console.WriteLine("PUBLIC "+r.tag_name+" bytes="+r.Asset.size+" metadata="+(args.Length>4?"official CLI snapshot (authenticated QA only)":"unauthenticated app request"));string zip=Path.Combine(root,"official.zip");UpdateFeed.Download(r,zip,CancellationToken.None,null);Extract(zip,"official",r.Version);});return failed==0?0:1;
            }
            package=Zip("valid");
            if(args.Length>3&&args[3]=="--race-only"){Test("startup crosses first check then worker crashes",StartupRace);return failed==0?0:1;}
            Test("stable feed",delegate{Assert(Parse(Metadata()).Version==new Version(1,0,20),"version");});
            Test("old feed parsed without manufacturing update",delegate{Assert(Parse(Metadata()).Version<=new Version(1,0,21),"compare");});
            Test("strict versions",delegate{foreach(string value in new[]{"v1.2.3","1.2","01.2.3","1.2.3.4","1.2.3-beta","99999999999.2.3"})Reject(()=>UpdateFeed.ParseVersion(value));});
            Test("malformed / oversize / prerelease metadata",delegate{Reject(()=>Parse("null"));Reject(()=>Parse("{"));Reject(()=>UpdateFeed.Parse(new byte[1024*1024+1]));Reject(()=>Parse(Metadata().Replace("\"prerelease\":false","\"prerelease\":true")));Reject(()=>Parse(Metadata().Replace("\"draft\":false","\"draft\":true")));});
            Test("missing duplicate digest size source",delegate{foreach(var text in new[]{Metadata().Replace("sha256:","md5:"),Metadata().Replace("\"size\":123","\"size\":0"),Metadata().Replace("\"size\":123","\"size\":67108865"),Metadata().Replace("DOIT-Ben","evil"),Metadata().Replace("Windows-x64.zip","Windows-arm64.zip")})Reject(()=>Parse(text));string json=Metadata();string asset=json.Substring(json.IndexOf("[{",StringComparison.Ordinal)+1);asset=asset.Substring(0,asset.Length-2);Reject(()=>Parse(json.Replace("["+asset+"]","["+asset+","+asset+"]")));});
            Test("URL allowlist",delegate{foreach(var url in new[]{"http://github.com/a","https://github.com.evil.test/a","https://u@github.com/a","https://github.com:444/a","https://example.com/a","https://github.com/a#fragment"})Reject(()=>UpdateFeed.ValidateUri(new Uri(url)));UpdateFeed.ValidateUri(new Uri("https://release-assets.githubusercontent.com/a"));});
            Test("HTTP actual bytes",delegate{Assert(Encoding.ASCII.GetString(Http("/ok",(s,t)=>Response(s,"200 OK\r\nContent-Length: 3","abc"),CancellationToken.None))=="abc","HTTP bytes");});
            Test("HTTP 404 / 429",delegate{foreach(var code in new[]{"404 Not Found","429 Too Many Requests"})Reject(()=>Http("/fail",(s,t)=>Response(s,code+"\r\nContent-Length: 0",""),CancellationToken.None));});
            Test("HTTP size / truncated",delegate{Reject(()=>Http("/large",(s,t)=>Response(s,"200 OK\r\nContent-Length: 4","abcd"),CancellationToken.None,3));Reject(()=>Http("/short",(s,t)=>Response(s,"200 OK\r\nContent-Length: 9","abc"),CancellationToken.None));});
            Test("HTTP cancel aborts active request",delegate{using(var c=new CancellationTokenSource(150)){var sw=Stopwatch.StartNew();Reject(()=>Http("/slow",delegate(NetworkStream s,CancellationToken t){t.WaitHandle.WaitOne(1000);},c.Token));Assert(sw.ElapsedMilliseconds<2000,"cancellation blocked");}});
            Test("download digest size cancellation cleanup retry",delegate{
                string hash;using(var f=new MemoryStream(Encoding.ASCII.GetBytes("abc")))hash=UpdateFeed.Hash(f);
                DownloadBody("download-ok","abc",3,hash,CancellationToken.None);Assert(File.ReadAllText(Path.Combine(root,"download-ok"))=="abc","bytes mismatch");
                Reject(()=>DownloadBody("download-retry","abc",3,new string('0',64),CancellationToken.None));Assert(!File.Exists(Path.Combine(root,"download-retry.part")),"hash failure partial");
                DownloadBody("download-retry","abc",3,hash,CancellationToken.None);
                Reject(()=>DownloadBody("download-short","abc",4,hash,CancellationToken.None));Assert(!File.Exists(Path.Combine(root,"download-short.part")),"size failure partial");
                using(var c=new CancellationTokenSource()){Reject(()=>DownloadBody("download-cancel","abc",3,hash,c.Token,n=>c.Cancel()));Assert(!File.Exists(Path.Combine(root,"download-cancel.part")),"cancel partial");}
                Reject(()=>DownloadBody("disk-error","abc",3,hash,CancellationToken.None,n=>{throw new IOException("disk full fixture");}));Assert(!File.Exists(Path.Combine(root,"disk-error.part")),"IO failure partial");
            });
            Test("strict ZIP complete + hashes + branding",()=>Extract(package,"extracted"));
            Test("registry portable mismatch preserves registered version",delegate{
                string sub=@"Software\DeepSeek-Reflex-Tests\"+Guid.NewGuid().ToString("N");
                try{using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(sub)){key.SetValue("InstallLocation",Path.Combine(root,"personal"));key.SetValue("DisplayVersion","sentinel");UpdateTransaction.SyncVersionIfMatched(key,Path.Combine(root,"portable"),"1.0.20");Assert((string)key.GetValue("DisplayVersion")=="sentinel","portable changed registered version");UpdateTransaction.SyncVersionIfMatched(key,Path.Combine(root,"personal"),"1.0.20");Assert((string)key.GetValue("DisplayVersion")=="1.0.20","matched registration not updated");}}finally{Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(sub,false);}
            });
            Test("ZIP duplicate / single-file limit / branding mismatch",delegate{
                string dup=Zip("duplicate");using(var stream=File.Open(dup,FileMode.Open,FileAccess.ReadWrite))using(var z=new ZipArchive(stream,ZipArchiveMode.Update)){z.CreateEntry("DeepSeek-Reflex/LICENSE");}Reject(()=>Extract(dup,"duplicate"));
                ExtractReject("zip-limit",d=>d["LICENSE"]=new byte[32*1024*1024+1]);
                ExtractReject("zip-brand",delegate(Dictionary<string,byte[]> d){d["DeepSeek-Reflex.exe"]=Encoding.UTF8.GetBytes("wrong brand");string lines=string.Join("\n",d.Where(x=>x.Key!="SHA256SUMS.txt").Select(x=>{using(var f=new MemoryStream(x.Value))return UpdateFeed.Hash(f)+"  "+x.Key;}));d["SHA256SUMS.txt"]=Encoding.UTF8.GetBytes(lines+"\n");});
            });
            Test("ZIP traversal / extra / missing / case",delegate{ExtractReject("traversal",d=>d.Add("../evil",new byte[0]));ExtractReject("missing",d=>d.Remove("LICENSE"));ExtractReject("case",d=>d.Add("license",new byte[0]));ExtractReject("extra",d=>d.Add("evil.dll",new byte[0]));});
            Test("ZIP internal hash / version / brand",delegate{ExtractReject("bad-hash",d=>d["LICENSE"]=new byte[]{1});ExtractReject("bad-ver",d=>d["VERSION"]=Encoding.UTF8.GetBytes("9.9.9"));Reject(()=>UpdatePackage.Extract(package,Path.Combine(root,"bad-digest"),new Version(1,0,20),new string('0',64)));});
            Test("target directory conflict before readiness",delegate{using(var s=new Session("dir-conflict")){File.Delete(Path.Combine(s.Target,"LICENSE"));Directory.CreateDirectory(Path.Combine(s.Target,"LICENSE"));s.Begin();s.Done();Assert(s.Worker.ExitCode!=0&&!File.Exists(UpdateTransaction.Signal(s.Path,"ready")),"conflict accepted");UserFiles(s.Target);}});
            Test("parent identity mismatch",delegate{using(var s=new Session("identity")){s.Plan.ParentStart--;UpdateTransaction.Write(s.Path,s.Plan);s.Begin();s.Done();Assert(s.Worker.ExitCode!=0&&!File.Exists(UpdateTransaction.Signal(s.Path,"ready")),"identity accepted");}});
            Test("preparation backup lock leaves parent active",delegate{using(var s=new Session("prepare-lock")){using(var held=new FileStream(Path.Combine(s.Target,"LICENSE"),FileMode.Open,FileAccess.Read,FileShare.None)){s.Begin();s.Done();}Assert(!s.Parent.HasExited&&!File.Exists(UpdateTransaction.Signal(s.Path,"ready")),"preparation failure closed parent");UserFiles(s.Target);}});
            Test("ready / abort preserves original",delegate{using(var s=new Session("abort")){s.Begin();s.Ready();File.WriteAllText(UpdateTransaction.Signal(s.Path,"abort"),"abort");s.Done();s.Old();Assert(!File.Exists(UpdateTransaction.Active(s.Target))&&!s.Parent.HasExited,"abort damaged parent");}});
            Test("exit timeout preserves original",delegate{using(var s=new Session("timeout")){s.Begin();s.Ready();s.Done();s.Old();Assert(!s.Parent.HasExited,"parent killed");}});
            Test("success / automatic new restart / other files",delegate{using(var s=new Session("success")){s.Begin();s.Ready();s.Exit();s.Done();Assert(s.Worker.ExitCode==0,"worker failed");Wait(()=>File.ReadAllText(Path.Combine(s.Target,"boot.log")).Contains("1.0.20"),"new restart missing");UpdatePackage.VerifyBrand(Path.Combine(s.Target,"DeepSeek-Reflex.exe"),new Version(1,0,20));UserFiles(s.Target);Assert(!File.Exists(UpdateTransaction.Active(s.Target)),"active leftover");}});
            Test("mid-apply file lock rollback + old restart",delegate{using(var s=new Session("lock")){s.Begin();s.Ready();using(var held=new FileStream(Path.Combine(s.Target,"icon.ico"),FileMode.Open,FileAccess.Read,FileShare.Read)){s.Exit();s.Done();}Assert(s.Worker.ExitCode==1,"failure falsely successful");s.Old();Wait(()=>File.ReadAllLines(Path.Combine(s.Target,"boot.log")).Length==2,"old restart missing");}});
            Test("external change after prepare retained",delegate{using(var s=new Session("changed")){s.Begin();s.Ready();File.WriteAllText(Path.Combine(s.Target,"LICENSE"),"external");s.Exit();s.Done();Assert(File.ReadAllText(Path.Combine(s.Target,"LICENSE"))=="external","unknown content overwritten");Assert(s.Worker.ExitCode==1,"change unnoticed");}});
            Test("concurrent startup blocked until complete",delegate{using(var s=new Session("competing","pause:after:Microsoft.Web.WebView2.Core.dll")){s.Begin();s.Ready();s.Exit();Wait(()=>File.Exists(UpdateTransaction.Signal(s.Path,"paused")),"pause missing");using(var other=UpdateTransaction.Start(Path.Combine(s.Target,"DeepSeek-Reflex.exe"),"")){Assert(other.WaitForExit(3000),"competing process blocked");}Assert(File.ReadAllLines(Path.Combine(s.Target,"boot.log")).Length==1,"mixed UI started");s.Worker.Kill();s.Worker.WaitForExit();File.Delete(UpdateTransaction.Signal(s.Path,"hook.txt"));using(var boot=UpdateTransaction.Start(Path.Combine(s.Target,"DeepSeek-Reflex.exe"),"")){Assert(boot.WaitForExit(5000),"bootstrap failed");}Wait(()=>!File.Exists(UpdateTransaction.Active(s.Target)),"recovery incomplete");s.Old();Wait(()=>File.ReadAllLines(Path.Combine(s.Target,"boot.log")).Length==2,"recovery restart missing");}});
            Test("crash + unknown recovery content remains blocked",delegate{using(var s=new Session("recovery-unknown","pause:after:Microsoft.Web.WebView2.Core.dll")){s.Begin();s.Ready();s.Exit();Wait(()=>File.Exists(UpdateTransaction.Signal(s.Path,"paused")),"pause missing");s.Worker.Kill();s.Worker.WaitForExit();File.Delete(UpdateTransaction.Signal(s.Path,"hook.txt"));File.WriteAllText(Path.Combine(s.Target,"Microsoft.Web.WebView2.Core.dll"),"unknown");using(var boot=UpdateTransaction.Start(Path.Combine(s.Target,"DeepSeek-Reflex.exe"),"")){boot.WaitForExit(5000);}Wait(()=>UpdateTransaction.Read<UpdateJournal>(Path.Combine(UpdateTransaction.TransactionRoot(s.Plan),"journal.json")).State=="RecoveryRequired","recovery state missing");Assert(File.ReadAllText(Path.Combine(s.Target,"Microsoft.Web.WebView2.Core.dll"))=="unknown"&&File.Exists(UpdateTransaction.Active(s.Target)),"unknown overwritten");Assert(File.ReadAllLines(Path.Combine(s.Target,"boot.log")).Length==1,"unsafe UI started");}});
            Test("startup crosses first check then worker crashes",StartupRace);
            Test("crash after executable / after commit",delegate{
                foreach(string stage in new[]{"after:DeepSeek-Reflex.exe","committed"})using(var s=new Session(stage=="committed"?"crash-committed":"crash-exe","pause:"+stage)) {
                    s.Begin();s.Ready();s.Exit();Wait(()=>File.Exists(UpdateTransaction.Signal(s.Path,"paused")),"pause missing");s.Worker.Kill();s.Worker.WaitForExit();File.Delete(UpdateTransaction.Signal(s.Path,"hook.txt"));
                    using(var boot=UpdateTransaction.Start(Path.Combine(s.Target,"DeepSeek-Reflex.exe"),"")){Assert(boot.WaitForExit(5000),"bootstrap exit");}
                    Wait(()=>!File.Exists(UpdateTransaction.Active(s.Target)),"last file crash not recovered");Wait(()=>File.ReadAllLines(Path.Combine(s.Target,"boot.log")).Length==2,"restart missing");
                    Assert(File.ReadAllLines(Path.Combine(s.Target,"boot.log"))[1]==(stage=="committed"?"1.0.20":"1.0.19"),"wrong recovery version");UserFiles(s.Target);
                }
            });
            Console.WriteLine(passed+" passed, "+failed+" failed");return failed==0?0:1;
        }
        private static void ExtractReject(string name,Action<Dictionary<string,byte[]>> mutate){string path=Zip(name,mutate);Reject(()=>Extract(path,name));}
        private static void StartupRace() {
            using(var s=new Session("startup-race","pause:after:Microsoft.Web.WebView2.Core.dll"))using(var competitor=UpdateTransaction.Start(Path.Combine(s.Target,"DeepSeek-Reflex.exe"),"--race")) {
                try{
                    Wait(()=>File.Exists(Path.Combine(s.Target,"startup-paused")),"first check not crossed");s.Begin();s.Ready();s.Exit();Wait(()=>File.Exists(UpdateTransaction.Signal(s.Path,"paused")),"apply not paused");
                    s.Worker.Kill();s.Worker.WaitForExit();File.Delete(UpdateTransaction.Signal(s.Path,"hook.txt"));File.WriteAllText(Path.Combine(s.Target,"release-startup"),"resume");Assert(competitor.WaitForExit(5000),"competitor not released");
                    Wait(()=>!File.Exists(UpdateTransaction.Active(s.Target)),"startup crossed recovery gate into mixed UI");s.Old();Wait(()=>File.ReadAllLines(Path.Combine(s.Target,"boot.log")).Length==2,"recovered old restart missing");
                }finally{File.WriteAllText(Path.Combine(s.Target,"release-startup"),"release");if(!competitor.HasExited){competitor.Kill();competitor.WaitForExit();}}
            }
        }
    }
}
