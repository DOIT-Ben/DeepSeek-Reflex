using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace DeepSeekFloat {
    [DataContract] internal sealed class UpdatePlan {
        [DataMember] public string Id,Target,Zip,ZipHash,Version,OldHash,Worker,WorkerHash;
        [DataMember] public int Parent;
        [DataMember] public long ParentStart;
    }
    [DataContract] internal sealed class UpdateItem {
        [DataMember] public string Name,OldHash,NewHash;
        [DataMember] public bool Intent;
    }
    [DataContract] internal sealed class UpdateJournal {
        [DataMember] public string State;
        [DataMember] public UpdateItem[] Items;
    }
    [DataContract] internal sealed class UpdatePointer {
        [DataMember] public string Plan,Hash;
    }
    internal static class UpdateTransaction {
        internal const string HostMutex=@"Local\DeepSeek-Reflex.Host";
        internal const string BarrierMutex=@"Local\DeepSeek-Reflex.Update";
        internal const string Exe="DeepSeek-Reflex.exe";
        internal static string TaskRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeepSeek-Reflex","updates");}}
        internal static Action<string> TestHook=null;
        internal static void Hook(string name){if(TestHook!=null)TestHook(name);}
        internal static bool BarrierActive() {
            try{using(var m=Mutex.OpenExisting(BarrierMutex))return true;}catch(WaitHandleCannotBeOpenedException){return false;}
        }
        internal static T Read<T>(string path) {
            UpdatePackage.NoLinks(path);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(stream.Length>128*1024)throw new InvalidDataException("更新记录过大。");return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }
        internal static void Write<T>(string path,T value) {
            UpdatePackage.NoLinks(path);string temp=path+".new-"+Guid.NewGuid().ToString("N");
            try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){new DataContractJsonSerializer(typeof(T)).WriteObject(f,value);f.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        internal static void CopyDurable(string from,string to) {
            using(var input=new FileStream(from,FileMode.Open,FileAccess.Read,FileShare.Read))using(var output=new FileStream(to,FileMode.CreateNew,FileAccess.Write,FileShare.None)){input.CopyTo(output);output.Flush(true);}
        }
        internal static string TransactionRoot(UpdatePlan p){return Path.Combine(p.Target,".update-"+p.Id);}
        internal static string Signal(string plan,string name){return Path.Combine(Path.GetDirectoryName(plan),name);}
        internal static string Active(string target){return Path.Combine(target,".reflex-update.json");}
        internal static UpdatePlan Validate(string planPath) {
            var p=Read<UpdatePlan>(planPath);Guid id;
            if(p==null||!Guid.TryParseExact(p.Id,"N",out id)||p.Target==null||p.Worker==null||p.Zip==null||!UpdateFeed.ValidHash(p.OldHash)||!UpdateFeed.ValidHash(p.WorkerHash)||!UpdateFeed.ValidHash(p.ZipHash))throw new InvalidDataException("更新计划无效。");
            UpdateFeed.ParseVersion(p.Version);
            string target=Path.GetFullPath(p.Target),task=Path.GetDirectoryName(Path.GetFullPath(planPath));
            if(target!=p.Target||target.TrimEnd(Path.DirectorySeparatorChar)==Path.GetPathRoot(target).TrimEnd(Path.DirectorySeparatorChar))throw new InvalidDataException("安装位置无效。");
            foreach(var special in new[]{Environment.SpecialFolder.UserProfile,Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.LocalApplicationData,Environment.SpecialFolder.Windows,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.DesktopDirectory}) {
                string protectedPath=Environment.GetFolderPath(special);if(protectedPath.Length>0&&string.Equals(target.TrimEnd(Path.DirectorySeparatorChar),protectedPath.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("不能更新到系统或用户根目录。");
            }
            string roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd(Path.DirectorySeparatorChar)+"\\";
            if(target.StartsWith(roaming,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("不能更新个人资料目录。");
            if(Path.GetFileName(task)!=p.Id||Path.GetFileName(planPath)!="plan.json"||p.Worker!=Path.Combine(task,Exe)||p.Zip!=Path.Combine(task,"package.zip")||task.StartsWith(target.TrimEnd(Path.DirectorySeparatorChar)+"\\",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("任务位置无效。");
            UpdatePackage.NoLinks(target);UpdatePackage.NoLinks(task);
            if(!string.Equals(UpdateFeed.Hash(p.Worker),p.WorkerHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新器校验失败。");
            foreach(var name in UpdatePackage.Managed) {string path=Path.Combine(target,name);UpdatePackage.NoLinks(path);if(Directory.Exists(path))throw new InvalidDataException("程序文件与目录冲突。");}
            return p;
        }
        internal static UpdatePlan CreatePlan(string zip,string version,string zipHash,string target,string workerSource) {
            string id=Guid.NewGuid().ToString("N"),dir=Path.Combine(TaskRoot,id);UpdatePackage.NoLinks(dir);Directory.CreateDirectory(dir);
            foreach(string name in new[]{Exe,Exe+".config","Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","WebView2Loader.dll"}) {
                string from=Path.Combine(workerSource,name);if(File.Exists(from))CopyDurable(from,Path.Combine(dir,name));
            }
            CopyDurable(zip,Path.Combine(dir,"package.zip"));
            using(var process=Process.GetCurrentProcess()) {
                var plan=new UpdatePlan{Id=id,Target=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar),Zip=Path.Combine(dir,"package.zip"),ZipHash=zipHash,Version=version,OldHash=UpdateFeed.Hash(Path.Combine(target,Exe)),Worker=Path.Combine(dir,Exe),Parent=process.Id,ParentStart=process.StartTime.ToUniversalTime().Ticks};
                plan.WorkerHash=UpdateFeed.Hash(plan.Worker);Write(Path.Combine(dir,"plan.json"),plan);return plan;
            }
        }
        internal static string PlanPath(UpdatePlan plan){return Path.Combine(Path.GetDirectoryName(plan.Worker),"plan.json");}
        internal static Process Launch(UpdatePlan plan) {return Start(plan.Worker,"--apply-update \""+PlanPath(plan)+"\"");}
        internal static Process Start(string exe,string args) {return Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe),CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});}
        private static Process Parent(int pid,long ticks,string exe) {
            var parent=Process.GetProcessById(pid);
            try{if(parent.StartTime.ToUniversalTime().Ticks!=ticks||!string.Equals(Path.GetFullPath(parent.MainModule.FileName),exe,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("父进程身份不匹配。");return parent;}catch{parent.Dispose();throw;}
        }
        private static bool WaitExit(Process parent,string plan,int milliseconds) {
            var timer=Stopwatch.StartNew();while(!parent.WaitForExit(100)){if(File.Exists(Signal(plan,"abort"))||timer.ElapsedMilliseconds>milliseconds)return false;}return !File.Exists(Signal(plan,"abort"));
        }
        private static void Publish(string source,string target) {
            string temp=target+".update-"+Guid.NewGuid().ToString("N");
            try{CopyDurable(source,temp);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private static string CurrentHash(string path){if(Directory.Exists(path))throw new InvalidDataException("目标变为目录。");UpdatePackage.NoLinks(path);return File.Exists(path)?UpdateFeed.Hash(path):null;}
        private static void SaveJournal(string root,UpdateJournal j){Write(Path.Combine(root,"journal.json"),j);}
        private static void ClearActive(UpdatePlan p,string planPath) {
            string active=Active(p.Target);if(!File.Exists(active))return;
            var pointer=Read<UpdatePointer>(active);if(pointer.Plan!=planPath||pointer.Hash!=UpdateFeed.Hash(planPath))throw new InvalidDataException("活动更新记录已改变。");File.Delete(active);
        }
        internal static bool Rollback(UpdatePlan p,UpdateJournal j) {
            string root=TransactionRoot(p);j.State="RollingBack";SaveJournal(root,j);
            bool ok=true;
            foreach(var item in j.Items.Reverse())if(item.Intent) {
                try {
                    string target=Path.Combine(p.Target,item.Name),current=CurrentHash(target);
                    if(current==item.OldHash)continue;
                    if(current!=item.NewHash)throw new InvalidDataException("文件由其它程序改变，已保留恢复副本。");
                    if(item.OldHash==null)File.Delete(target);
                    else {string backup=Path.Combine(root,"backup",item.Name);if(CurrentHash(backup)!=item.OldHash)throw new InvalidDataException("恢复副本校验失败。");Publish(backup,target);}
                    Hook("rollback:"+item.Name);
                }catch(Exception e){if(e is OutOfMemoryException)throw;ok=false;}
            }
            j.State=ok?"RolledBack":"RecoveryRequired";SaveJournal(root,j);return ok;
        }
        private static bool Restart(UpdatePlan p,string expected) {
            try{string exe=Path.Combine(p.Target,Exe);if(CurrentHash(exe)!=expected)return false;using(var process=Start(exe,"")){return process!=null;}}catch(Exception){return false;}
        }
        private static void SyncVersion(UpdatePlan p) {
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex",true)) {
                SyncVersionIfMatched(key,p.Target,p.Version);
            }
        }
        internal static void SyncVersionIfMatched(RegistryKey key,string target,string version) {
            if(key==null)return;var location=key.GetValue("InstallLocation") as string;
            if(location!=null&&string.Equals(Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar),target.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))key.SetValue("DisplayVersion",version,RegistryValueKind.String);
        }
        internal static int Run(string planPath,bool recover,int recoveryPid=0,long recoveryTicks=0) {
            UpdatePlan p=null;UpdateJournal journal=null;Mutex barrier=null,host=null;bool ownBarrier=false,ownHost=false,parentExited=false,activated=false;
            try {
                p=Validate(planPath);
                if(!string.Equals(Process.GetCurrentProcess().MainModule.FileName,p.Worker,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新器位置不匹配。");
                barrier=new Mutex(false,BarrierMutex);try{ownBarrier=barrier.WaitOne(0);}catch(AbandonedMutexException){ownBarrier=true;}if(!ownBarrier)throw new IOException("另一个更新正在进行。");
                string root=TransactionRoot(p),targetExe=Path.Combine(p.Target,Exe);
                using(var parent=Parent(recover?recoveryPid:p.Parent,recover?recoveryTicks:p.ParentStart,targetExe)) {
                    if(recover) {
                        var pointer=Read<UpdatePointer>(Active(p.Target));if(pointer.Plan!=planPath||pointer.Hash!=UpdateFeed.Hash(planPath))throw new InvalidDataException("恢复计划不匹配。");activated=true;
                        journal=Read<UpdateJournal>(Path.Combine(root,"journal.json"));ValidateJournal(journal);
                        Write(Signal(planPath,"recovery-ready"),recoveryPid.ToString()+":"+recoveryTicks);
                        if(!WaitExit(parent,planPath,30000))throw new IOException("恢复启动进程未退出。");parentExited=true;
                    } else {
                        if(File.Exists(Active(p.Target))||Directory.Exists(root)||CurrentHash(targetExe)!=p.OldHash)throw new InvalidDataException("安装已变化或有未完成的更新。");
                        var oldVersion=UpdateFeed.ParseVersion(FileVersionInfo.GetVersionInfo(targetExe).ProductVersion);UpdatePackage.VerifyBrand(targetExe,oldVersion);
                        if(UpdateFeed.ParseVersion(p.Version)<=oldVersion)throw new InvalidDataException("不能降级更新。");
                        Directory.CreateDirectory(root);string staging=Path.Combine(root,"staging"),backup=Path.Combine(root,"backup");Directory.CreateDirectory(backup);
                        UpdatePackage.Extract(p.Zip,staging,UpdateFeed.ParseVersion(p.Version),p.ZipHash);
                        journal=new UpdateJournal{State="Prepared",Items=UpdatePackage.Managed.Select(name=>new UpdateItem{Name=name,OldHash=CurrentHash(Path.Combine(p.Target,name)),NewHash=CurrentHash(Path.Combine(staging,name))}).ToArray()};
                        foreach(var item in journal.Items)if(item.OldHash!=null){CopyDurable(Path.Combine(p.Target,item.Name),Path.Combine(backup,item.Name));if(CurrentHash(Path.Combine(backup,item.Name))!=item.OldHash)throw new IOException("备份期间文件变化。");}
                        SaveJournal(root,journal);Write(Active(p.Target),new UpdatePointer{Plan=planPath,Hash=UpdateFeed.Hash(planPath)});activated=true;
                        Write(Signal(planPath,"ready"),"ready");Hook("ready");
                        if(!WaitExit(parent,planPath,30000)){journal.State="Aborted";SaveJournal(root,journal);ClearActive(p,planPath);activated=false;Write(Signal(planPath,"result"),"已取消安装，原程序仍在运行。");return 2;}parentExited=true;
                    }
                }
                host=new Mutex(false,HostMutex);try{ownHost=host.WaitOne(0);}catch(AbandonedMutexException){ownHost=true;}
                if(!ownHost)throw new IOException("另一个主程序正在运行，未替换文件。");
                if(recover) {
                    if(journal.State=="Committed"){ClearActive(p,planPath);activated=false;Write(Signal(planPath,"result"),"更新已安装。");}
                    else if(Rollback(p,journal)){ClearActive(p,planPath);activated=false;Write(Signal(planPath,"result"),"上次更新中断，已恢复旧版。");}
                    else throw new IOException("恢复未完成，备份已保留。");
                } else {
                    journal.State="Applying";SaveJournal(root,journal);
                    foreach(var item in journal.Items) {
                        if(CurrentHash(Path.Combine(p.Target,item.Name))!=item.OldHash)throw new IOException("准备后程序文件被修改。");
                        item.Intent=true;SaveJournal(root,journal);Hook("before:"+item.Name);
                        Publish(Path.Combine(root,"staging",item.Name),Path.Combine(p.Target,item.Name));
                        if(CurrentHash(Path.Combine(p.Target,item.Name))!=item.NewHash)throw new IOException("写入校验失败。");Hook("after:"+item.Name);
                    }
                    UpdatePackage.VerifyBrand(targetExe,UpdateFeed.ParseVersion(p.Version));journal.State="Committed";SaveJournal(root,journal);Hook("committed");
                    ClearActive(p,planPath);activated=false;string result="更新已安装。";
                    try{SyncVersion(p);}catch(Exception){result+="系统版本登记未同步。";}Write(Signal(planPath,"result"),result);
                }
                if(ownHost){host.ReleaseMutex();ownHost=false;}if(host!=null){host.Dispose();host=null;}
                if(ownBarrier){barrier.ReleaseMutex();ownBarrier=false;}if(barrier!=null){barrier.Dispose();barrier=null;}
                string expected=journal.State=="Committed"?journal.Items.Last().NewHash:p.OldHash;
                if(!Restart(p,expected)){Write(Signal(planPath,"result"),"文件已处理，但自动重启失败。请手动打开程序。");return 3;}return 0;
            } catch(Exception error) {
                if(error is OutOfMemoryException)throw;
                bool restored=false;
                if(p!=null&&activated&&!parentExited&&journal!=null&&!journal.Items.Any(x=>x.Intent)) {
                    try{journal.State="Aborted";SaveJournal(TransactionRoot(p),journal);ClearActive(p,planPath);activated=false;}catch(Exception){}
                }
                if(p!=null&&activated&&parentExited&&ownHost&&journal!=null&&journal.State!="Committed") {
                    try{restored=Rollback(p,journal);if(restored){ClearActive(p,planPath);activated=false;}}catch(Exception){}
                }
                try{Write(Signal(planPath,"result"),restored?"安装失败，已恢复旧版。":activated?"更新恢复未完成，备份已保留。请重新启动以恢复。":"未安装更新，原程序文件已保留。");}catch(Exception){}
                if(ownHost){host.ReleaseMutex();ownHost=false;}if(host!=null){host.Dispose();host=null;}
                if(ownBarrier){barrier.ReleaseMutex();ownBarrier=false;}if(barrier!=null){barrier.Dispose();barrier=null;}
                if(restored)Restart(p,p.OldHash);return 1;
            } finally {if(ownHost)host.ReleaseMutex();if(ownBarrier)barrier.ReleaseMutex();if(host!=null)host.Dispose();if(barrier!=null)barrier.Dispose();}
        }
        private static void ValidateJournal(UpdateJournal j) {
            if(j==null||j.Items==null||j.Items.Length!=UpdatePackage.Managed.Length)throw new InvalidDataException("恢复记录无效。");
            for(int i=0;i<j.Items.Length;i++){var item=j.Items[i];if(item==null||item.Name!=UpdatePackage.Managed[i]||!UpdateFeed.ValidHash(item.NewHash)||(item.OldHash!=null&&!UpdateFeed.ValidHash(item.OldHash)))throw new InvalidDataException("恢复文件记录无效。");}
        }
        internal static bool Bootstrap(string target) {
            if(BarrierActive())return true;string active=Active(target);if(!File.Exists(active))return false;
            try {
                var pointer=Read<UpdatePointer>(active);if(pointer==null||pointer.Plan==null||!UpdateFeed.ValidHash(pointer.Hash)||UpdateFeed.Hash(pointer.Plan)!=pointer.Hash)throw new InvalidDataException("恢复指针无效。");
                var p=Validate(pointer.Plan);if(!string.Equals(p.Target,Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("恢复目标不匹配。");
                using(var self=Process.GetCurrentProcess())using(var child=Start(p.Worker,"--recover-update \""+pointer.Plan+"\" "+self.Id+" "+self.StartTime.ToUniversalTime().Ticks)) {
                    if(child==null)throw new IOException("恢复程序无法启动。");
                    string ready=Signal(pointer.Plan,"recovery-ready"),identity=self.Id+":"+self.StartTime.ToUniversalTime().Ticks;
                    var timer=Stopwatch.StartNew();bool accepted=false;
                    while(timer.ElapsedMilliseconds<5000&&!child.HasExited){if(File.Exists(ready)&&Read<string>(ready)==identity){accepted=true;break;}Thread.Sleep(40);}
                    if(!accepted)throw new IOException("恢复程序未确认交接。");
                }
            }catch(Exception){System.Windows.Forms.MessageBox.Show("上次更新尚未恢复，已保留程序备份。请勿删除安装目录中的 .update 文件夹。","DeepSeek-Reflex 更新恢复");}return true;
        }
    }
}
