using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
namespace DeepSeekFloat {
 internal static class BridgeTransportTests {
  static int passed,calls;
  static void Check(bool value,string label){if(!value)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
  static BridgeRequest Request(){return new BridgeRequest{id=Guid.NewGuid().ToString("D"),mode="polish",text="isolated transport fixture"};}
  static BridgeReply Helper(BridgeRequest request){
   using(var child=Process.Start(new ProcessStartInfo(ReflexBridge.Executable,"--reflex-bridge"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true})){
    child.StandardInput.Write(System.Text.Encoding.UTF8.GetString(ReflexBridge.Json(request)));child.StandardInput.Close();var output=child.StandardOutput.ReadToEndAsync();
    if(!child.WaitForExit(12000)){child.Kill();throw new Exception("helper timeout");}return ReflexBridge.Parse<BridgeReply>(System.Text.Encoding.UTF8.GetBytes(output.Result));
   }
  }
  [STAThread] static int Main(string[] args){
   if(args.Length==1&&args[0]=="--reflex-bridge")return ReflexBridge.RunHelper();
   if(args.Length==0)return 2; // isolated missing-server test must never open a personal app
   try{
    var sid=WindowsIdentity.GetCurrent().User;
    var rules=ReflexBridge.Security().GetAccessRules(true,false,typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToArray();
    Check(rules.Length==2&&rules.Any(r=>r.IdentityReference.Equals(sid)&&r.AccessControlType==AccessControlType.Allow)&&rules.Any(r=>r.IdentityReference.Equals(new SecurityIdentifier(WellKnownSidType.NetworkSid,null))&&r.AccessControlType==AccessControlType.Deny),"pipe ACL permits only current SID and denies network identity");
    var bridge=new ReflexBridge((request,cancel)=>{Interlocked.Increment(ref calls);return Task.FromResult("filled");});
    using(bridge){
     Thread.Sleep(200);var first=Request();var result=Helper(first);Check(result.id==first.id&&result.status=="filled"&&calls==1,"real fixed WinExe helper sends stdin across same-EXE authenticated pipe");
     using(var partial=new NamedPipeClientStream(".",ReflexBridge.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous)){
      partial.Connect(2000);Check(ReflexBridge.SamePeer(partial,true),"helper independently verifies server process identity");
      var header=BitConverter.GetBytes(100);partial.Write(header,0,4);partial.Flush();
      Thread.Sleep(3400);var watch=Stopwatch.StartNew();Check(Helper(Request()).status=="filled"&&watch.ElapsedMilliseconds<5000,"partial authenticated frame expires and next client succeeds");
     }
     using(var oversized=new NamedPipeClientStream(".",ReflexBridge.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous)){
      oversized.Connect(2000);var header=BitConverter.GetBytes(90001);oversized.Write(header,0,4);oversized.Flush();Thread.Sleep(150);
     }
     Check(Helper(Request()).status=="filled","oversized frame is rejected without poisoning the server");
     var node=new ProcessStartInfo("node","\""+args[0]+"\" \""+ReflexBridge.Executable+"\" \""+ReflexBridge.PipeName+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
     using(var child=Process.Start(node)){
      var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();if(!child.WaitForExit(18000)){child.Kill();throw new Exception("HTTP transport timeout");}
      Check(child.ExitCode==0&&output.Result.Contains("PASS real HTTP stdin pipe transport"),"real HTTP host uses helper and rejects unauthenticated Node pipe client: "+(child.ExitCode==0?"":error.Result));
     }
    }
    Check(Helper(Request()).status=="outcome-unknown","disposed or legacy missing server fails boundedly without mutating a window");
    Console.WriteLine("TOTAL "+passed+" real transport checks");return 0;
   }catch(Exception error){Console.WriteLine(error);return 1;}
  }
 }
}
