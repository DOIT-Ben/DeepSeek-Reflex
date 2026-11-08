using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
namespace DeepSeekFloat {
    internal static class UpdateFixture {
        private static int Main(string[] args) {
            string dir=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if(args.Length>=2&&(args[0]=="--apply-update"||args[0]=="--recover-update")) {
                string hookFile=Path.Combine(dir,"hook.txt");
                if(File.Exists(hookFile))UpdateTransaction.TestHook=delegate(string step){
                    string hook=File.ReadAllText(hookFile);
                    if(hook=="throw:"+step)throw new IOException("injected failure");
                    if(hook=="pause:"+step){File.WriteAllText(Path.Combine(dir,"paused"),step);while(true)Thread.Sleep(100);}
                };
                return args[0]=="--apply-update"?UpdateTransaction.Run(args[1],false):UpdateTransaction.Run(args[1],true,int.Parse(args[2]),long.Parse(args[3]));
            }
            if(UpdateTransaction.Bootstrap(dir))return 0;
            if(args.Length==1&&args[0]=="--race") {
                File.WriteAllText(Path.Combine(dir,"startup-paused"),"paused");
                while(!File.Exists(Path.Combine(dir,"release-startup")))Thread.Sleep(40);
            }
            using(var host=new Mutex(false,UpdateTransaction.HostMutex)) {
                bool acquired;try{acquired=host.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired)return 0;
                try {
                    if(UpdateTransaction.Bootstrap(dir))return 0;
                    File.AppendAllText(Path.Combine(dir,"boot.log"),System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(dir,"DeepSeek-Reflex.exe")).ProductVersion+"\n");
                    if(args.Length==1&&args[0]=="--parent"){File.WriteAllText(Path.Combine(dir,"parent-ready"),"ready");while(!File.Exists(Path.Combine(dir,"exit-parent")))Thread.Sleep(40);}
                }finally{host.ReleaseMutex();}
            }return 0;
        }
    }
}
