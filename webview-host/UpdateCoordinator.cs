using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeekFloat {
    internal enum UpdateStatus { Idle,Checking,Current,Available,CheckFailed,Downloading,DownloadFailed,Ready,Preparing }
    [DataContract] internal sealed class UpdatePreferences {
        [DataMember] public bool Auto=true;
        [DataMember] public long Attempt,Success;
        [DataMember] public string Notified=null;
    }
    internal class UpdateHandoff:IDisposable {
        private readonly Process process;private readonly string plan;
        internal UpdateHandoff(Process process,string plan){this.process=process;this.plan=plan;}
        internal virtual bool Alive {get{return process!=null&&!process.HasExited;}}
        internal virtual async Task WaitReady(CancellationToken token) {
            var timer=Stopwatch.StartNew();
            while(timer.ElapsedMilliseconds<45000) {
                token.ThrowIfCancellationRequested();if(!Alive)throw new IOException("更新器准备失败，原程序仍在运行。");
                if(File.Exists(UpdateTransaction.Signal(plan,"ready")))return;await Task.Delay(80,token);
            }throw new TimeoutException("更新器准备超时，原程序仍在运行。");
        }
        internal virtual void Abort(){if(plan!=null)UpdateTransaction.Write(UpdateTransaction.Signal(plan,"abort"),"abort");}
        public virtual void Dispose(){if(process!=null)process.Dispose();}
    }
    internal class UpdateBackend {
        internal virtual UpdateRelease Check(CancellationToken token){return UpdateFeed.Check(token);}
        internal virtual void Download(UpdateRelease release,string path,CancellationToken token,Action<long> progress){UpdateFeed.Download(release,path,token,progress);}
        internal virtual void Verify(UpdateRelease release,string path){UpdatePackage.Extract(path,path+".verified",release.Version,release.Asset.digest.Substring(7));}
        internal virtual UpdateHandoff Prepare(UpdateRelease release,string path) {
            string install=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var plan=UpdateTransaction.CreatePlan(path,release.Version.ToString(),release.Asset.digest.Substring(7),install,install);
            string planPath=UpdateTransaction.PlanPath(plan);
            // One bounded local receipt, read at next launch. No user preferences
            // or browser profile is part of the worker protocol.
            UpdateTransaction.Write(Path.Combine(UpdateTransaction.TaskRoot,"last.json"),new UpdatePointer{Plan=planPath,Hash=UpdateFeed.Hash(planPath)});
            return new UpdateHandoff(UpdateTransaction.Launch(plan),planPath);
        }
    }
    internal sealed class UpdateCoordinator:IDisposable {
        private readonly Func<Task<string>> gate;
        private readonly Action<bool> freeze;
        private readonly Action exit;
        private readonly UpdateBackend backend;
        private readonly string stateFile,cacheRoot;
        private readonly SynchronizationContext ui;
        private CancellationTokenSource operation;
        private UpdateHandoff handoff;
        private UpdatePreferences prefs;
        private string downloaded,downloadDirectory;
        private bool disposed,exiting,busy;
        internal event Action Changed;
        internal event Action<string> NewVersion;
        internal Version Current {get;private set;}
        internal UpdateStatus State {get;private set;}
        internal UpdateRelease Release {get;private set;}
        internal string Message {get;private set;}
        internal long Downloaded {get;private set;}
        internal bool Auto {get{return prefs.Auto;}}
        internal bool Busy {get{return busy;}}
        internal UpdateCoordinator(Func<Task<string>> gate,Action<bool> freeze,Action exit,UpdateBackend backend=null,Version current=null,string stateFile=null,string cacheRoot=null) {
            this.gate=gate;this.freeze=freeze;this.exit=exit;this.backend=backend??new UpdateBackend();ui=SynchronizationContext.Current;
            var assemblyVersion=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Current=current??new Version(assemblyVersion.Major,assemblyVersion.Minor,assemblyVersion.Build);
            this.stateFile=stateFile??Path.Combine(UpdateTransaction.TaskRoot,"preferences.json");this.cacheRoot=cacheRoot??UpdateTransaction.TaskRoot;
            try{prefs=UpdateTransaction.Read<UpdatePreferences>(this.stateFile)??new UpdatePreferences();}catch(Exception){prefs=new UpdatePreferences();}
            State=UpdateStatus.Idle;Message="检查正式版本，选择何时更新。";
        }
        private void Notify(){if(!disposed&&Changed!=null)Changed();}
        private bool SavePreferences(){try{Directory.CreateDirectory(Path.GetDirectoryName(stateFile));UpdateTransaction.Write(stateFile,prefs);return true;}catch(Exception){return false;}}
        internal void SetAuto(bool value) {
            if(disposed||busy)return;bool old=prefs.Auto;prefs.Auto=value;
            if(!SavePreferences()){prefs.Auto=old;Message="无法保存自动检查选项，请检查磁盘空间和权限。";}Notify();
        }
        internal static bool Due(UpdatePreferences state,DateTime now) {
            long ticks=now.Ticks;return state.Auto&&(state.Success<=0||state.Success>ticks||ticks-state.Success>=TimeSpan.TicksPerDay)&&(state.Attempt<=0||state.Attempt>ticks||ticks-state.Attempt>=TimeSpan.TicksPerHour);
        }
        internal async Task CheckAsync(bool automatic=false) {
            if(disposed||busy||State==UpdateStatus.Ready||State==UpdateStatus.Preparing)return;
            if(automatic&&!Due(prefs,DateTime.UtcNow))return;
            prefs.Attempt=DateTime.UtcNow.Ticks;if(!SavePreferences()&&automatic)return;
            busy=true;operation=new CancellationTokenSource();State=UpdateStatus.Checking;Message="正在检查更新…";Notify();
            try {
                var found=await Task.Run(()=>backend.Check(operation.Token),operation.Token);operation.Token.ThrowIfCancellationRequested();if(disposed)return;
                Release=found;prefs.Success=DateTime.UtcNow.Ticks;State=found.Version>Current?UpdateStatus.Available:UpdateStatus.Current;
                Message=State==UpdateStatus.Available?"发现新版本 v"+found.Version:found.Version<Current?"当前版本高于最新正式版，无需更新。":"当前已是最新版本。";
                if(found.MaintenanceWarning!=null)Message+=" "+found.MaintenanceWarning;
                if(automatic&&State==UpdateStatus.Available&&prefs.Notified!=found.Version.ToString()) {
                    prefs.Notified=found.Version.ToString();if(NewVersion!=null)NewVersion(found.Version.ToString());
                }SavePreferences();
            }catch(OperationCanceledException){State=UpdateStatus.Idle;Message="已取消检查。";}
            catch(Exception error){State=UpdateStatus.CheckFailed;Message=Explain(error,"检查");}
            finally{busy=false;operation.Dispose();operation=null;Notify();}
        }
        internal async Task DownloadAsync() {
            if(disposed||busy||Release==null||Release.Version<=Current||(State!=UpdateStatus.Available&&State!=UpdateStatus.DownloadFailed))return;
            busy=true;operation=new CancellationTokenSource();State=UpdateStatus.Downloading;Downloaded=0;Message="正在下载更新…";Notify();
            string dir=Path.Combine(cacheRoot,"download-"+Guid.NewGuid().ToString("N"));
            try {
                UpdatePackage.NoLinks(dir);Directory.CreateDirectory(dir);string path=Path.Combine(dir,"package.zip");var token=operation.Token;
                await Task.Run(delegate{
                    backend.Download(Release,path,token,delegate(long count){if(ui!=null)ui.Post(delegate{if(!disposed&&State==UpdateStatus.Downloading){Downloaded=count;Notify();}},null);});
                    token.ThrowIfCancellationRequested();backend.Verify(Release,path);token.ThrowIfCancellationRequested();
                },token);
                token.ThrowIfCancellationRequested();if(disposed)return;
                downloaded=path;downloadDirectory=dir;dir=null;State=UpdateStatus.Ready;Message="下载与校验完成，可以安装并重启。";
            }catch(OperationCanceledException){State=UpdateStatus.Available;Message="下载已取消，可以重新下载。";}
            catch(Exception error){State=UpdateStatus.DownloadFailed;Message=Explain(error,"下载");}
            finally{if(dir!=null)DeleteCache(dir);busy=false;operation.Dispose();operation=null;Notify();}
        }
        internal async Task InstallAsync() {
            if(disposed||busy||State!=UpdateStatus.Ready||downloaded==null)return;
            busy=true;State=UpdateStatus.Preparing;Message="正在检查未保存内容…";Notify();bool frozen=false;
            operation=new CancellationTokenSource();
            try {
                string reason=await gate();if(disposed)return;if(reason!=null){Message=reason;return;}
                freeze(true);frozen=true;Message="正在准备安装，完成后将自动重启。";Notify();
                reason=await gate();if(reason!=null){Message=reason;return;}
                handoff=await Task.Run(()=>backend.Prepare(Release,downloaded));
                await handoff.WaitReady(operation.Token);
                reason=await gate();if(reason!=null){Message=reason;return;}
                if(!handoff.Alive)throw new IOException("更新器已停止，原程序仍在运行。");
                exiting=true;exit();
            }catch(OperationCanceledException){Message="安装已取消，原程序仍在运行。";}
            catch(Exception error){exiting=false;Message=Explain(error,"安装");}
            finally {
                if(!exiting){if(handoff!=null){try{handoff.Abort();}catch(Exception){}handoff.Dispose();handoff=null;}if(frozen)freeze(false);State=UpdateStatus.Ready;}
                busy=false;if(operation!=null){operation.Dispose();operation=null;}Notify();
            }
        }
        internal void Cancel(){if(!disposed&&State!=UpdateStatus.Preparing&&operation!=null)operation.Cancel();}
        internal static string Explain(Exception error,string action) {
            var web=error as WebException;var response=web==null?null:web.Response as HttpWebResponse;
            if(response!=null&&((int)response.StatusCode==403||(int)response.StatusCode==429)) {
                long reset;
                if(response.Headers["X-RateLimit-Remaining"]=="0"&&long.TryParse(response.Headers["X-RateLimit-Reset"],out reset)) {
                    try{return "GitHub 共享网络额度已用尽，预计 "+new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(reset).ToLocalTime().ToString("HH:mm")+" 后可重试。也可打开下方更新公告手动下载。";}catch(ArgumentOutOfRangeException){}
                }
                return "更新服务暂时拒绝检查，请稍后重试，或打开下方更新公告手动下载。";
            }
            if(error is InvalidDataException&&error.Message.StartsWith("更新清单已过期",StringComparison.Ordinal))return error.Message;
            if(error is InvalidDataException)return action+"校验失败，原程序和配置均未更改。请重试。";
            if(error is TimeoutException||(web!=null&&web.Status==WebExceptionStatus.Timeout))return action+"超时，请检查网络后重试。";
            if(error is IOException||error is UnauthorizedAccessException)return action+"失败，请检查磁盘空间、文件占用或权限后重试。";
            return action+"失败，请检查网络后重试。";
        }
        private void DeleteCache(string path) {
            try{string full=Path.GetFullPath(path);if(Path.GetDirectoryName(full)!=Path.GetFullPath(cacheRoot)||!Path.GetFileName(full).StartsWith("download-",StringComparison.Ordinal))return;UpdatePackage.NoLinks(full);if(Directory.Exists(full))Directory.Delete(full,true);}catch(Exception){}
        }
        internal static string LastResult(string target) {
            try {
                string receipt=Path.Combine(UpdateTransaction.TaskRoot,"last.json");if(!File.Exists(receipt))return null;
                var pointer=UpdateTransaction.Read<UpdatePointer>(receipt);if(pointer.Hash!=UpdateFeed.Hash(pointer.Plan))return null;
                var plan=UpdateTransaction.Validate(pointer.Plan);if(!string.Equals(plan.Target,target.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))return null;
                string result=UpdateTransaction.Signal(pointer.Plan,"result");if(!File.Exists(result))return null;string message=UpdateTransaction.Read<string>(result);File.Delete(receipt);return message;
            }catch(Exception){return null;}
        }
        public void Dispose(){if(disposed)return;disposed=true;if(operation!=null)operation.Cancel();if(handoff!=null){if(!exiting)try{handoff.Abort();}catch(Exception){}handoff.Dispose();handoff=null;}if(!exiting&&downloadDirectory!=null)DeleteCache(downloadDirectory);}
    }
}
