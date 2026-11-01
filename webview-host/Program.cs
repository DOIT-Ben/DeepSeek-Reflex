using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DeepSeekFloat
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if(args.Length==1&&args[0]=="--reflex-bridge"){Environment.ExitCode=ReflexBridge.RunHelper();return;}
            if(args.Length==1&&args[0].StartsWith("--register-shortcut=",StringComparison.Ordinal)) {
                try{ShellIdentity.RegisterShortcut(args[0].Substring("--register-shortcut=".Length));}
                catch(Exception){Environment.ExitCode=1;}
                return;
            }
            bool created;
            using (var mutex = new Mutex(true, @"Local\DeepSeek-Reflex.Host", out created))
            {
                if (!created)
                {
                    var previous = Native.FindWindow(null, "DeepSeek-Reflex");
                    if(previous==IntPtr.Zero)previous=Native.FindWindow(null,"DeepSeek 小窗");
                    if (previous != IntPtr.Zero) Native.PostMessage(previous, Native.OpenMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
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
