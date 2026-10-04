using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace DeepSeekFloat {
 internal static class LegacyLauncher {
  internal static string Quote(string value) {
   var result=new StringBuilder("\"");int slashes=0;
   foreach(char character in value) {
    if(character=='\\'){slashes++;continue;}
    result.Append('\\',character=='\"'?slashes*2+1:slashes).Append(character);slashes=0;
   }
   return result.Append('\\',slashes*2).Append('"').ToString();
  }
  [STAThread] static int Main(string[] args) {
   try {
    ShellIdentity.InitializeProcess();
    var directory=AppDomain.CurrentDomain.BaseDirectory;
    var target=Path.Combine(directory,"DeepSeekFloat.exe");
    if(!File.Exists(target))throw new IOException("Missing host");
    using(var process=Process.Start(new ProcessStartInfo(target,String.Join(" ",args.Select(Quote))) {WorkingDirectory=directory,UseShellExecute=false})) {
     if(process==null)throw new IOException("Host did not start");
    }
    return 0;
   }catch(Exception error) {
    if(!(error is IOException)&&!(error is System.ComponentModel.Win32Exception))throw;
    MessageBox.Show("无法打开 DeepSeek-Reflex，请重新安装完整版本。","DeepSeek-Reflex",MessageBoxButtons.OK,MessageBoxIcon.Information);
    return 1;
   }
  }
 }
}
