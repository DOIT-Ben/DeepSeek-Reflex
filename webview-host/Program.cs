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
            bool created;
            using (var mutex = new Mutex(true, @"Local\DeepSeekFloat.Host", out created))
            {
                if (!created)
                {
                    var previous = Native.FindWindow(null, "DeepSeek 小窗");
                    if (previous != IntPtr.Zero) Native.PostMessage(previous, Native.OpenMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { ShowError(e.Exception); };
                try { Application.Run(new ChatWindow(args)); }
                catch (Exception e) { ShowError(e); }
            }
        }
        private static void ShowError(Exception error)
        {
            try
            {
                Directory.CreateDirectory(Preferences.Root);
                // Record code locations only, never exception messages or website/account data.
                File.AppendAllText(Path.Combine(Preferences.Root,"host-errors.log"),DateTime.UtcNow.ToString("o")+" "+error.GetType().FullName+"\n"+error.StackTrace+"\n");
            }
            catch (IOException) { }
            MessageBox.Show("小窗遇到问题，请重新打开。\n" + error.GetType().Name, "DeepSeek 小窗", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
