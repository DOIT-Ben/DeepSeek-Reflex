using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.CompilerServices;

namespace DeepSeekFloat
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if(args.Length==2&&args[0]=="--apply-update"){Environment.ExitCode=UpdateTransaction.Run(args[1],false);return;}
            if(args.Length==4&&args[0]=="--recover-update") {
                int pid;long ticks;if(!int.TryParse(args[2],out pid)||!long.TryParse(args[3],out ticks)){Environment.ExitCode=1;return;}
                Environment.ExitCode=UpdateTransaction.Run(args[1],true,pid,ticks);return;
            }
            if(args.Length==1&&args[0]=="--reflex-bridge"){Environment.ExitCode=ReflexBridge.RunHelper();return;}
            if(args.Length==1&&args[0].StartsWith("--register-shortcut=",StringComparison.Ordinal)) {
                try{ShellIdentity.RegisterShortcut(args[0].Substring("--register-shortcut=".Length));}
                catch(Exception){Environment.ExitCode=1;}
                return;
            }
            string install=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if(UpdateTransaction.Bootstrap(install))return;
            using (var mutex = new Mutex(false, UpdateTransaction.HostMutex))
            {
                bool acquired;try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                if (!acquired)
                {
                    var previous = Native.FindWindow(null, "DeepSeek-Reflex");
                    if(previous==IntPtr.Zero)previous=Native.FindWindow(null,"DeepSeek 小窗");
                    if (previous != IntPtr.Zero) Native.PostMessage(previous, Native.OpenMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                try{if(UpdateTransaction.Bootstrap(install))return;RunUi(args);}finally{mutex.ReleaseMutex();}
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunUi(string[] args)
        {
                ShellIdentity.InitializeProcess();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { ShowError(e.Exception); };
                // Background threads bypass ThreadException; record their fatal
                // errors too instead of exiting without a trace.
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject as Exception); };
                try { Application.Run(new ChatWindow(args)); }
                catch (Exception e) { ShowError(e); }
        }
        internal static void LogError(Exception error,string source=null)
        {
            if(error==null)return;
            try
            {
                Directory.CreateDirectory(Preferences.Root);
                // Record code locations only, never exception messages or website/account data.
                File.AppendAllText(Path.Combine(Preferences.Root,"host-errors.log"),DateTime.UtcNow.ToString("o")+" "+(source!=null?source+" ":"")+error.GetType().FullName+"\n"+error.StackTrace+"\n");
            }
            catch (IOException) { }
        }
        private static void ShowError(Exception error)
        {
            LogError(error);
            MessageBox.Show("小窗遇到问题，请重新打开。\n" + error.GetType().Name, "DeepSeek-Reflex", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
