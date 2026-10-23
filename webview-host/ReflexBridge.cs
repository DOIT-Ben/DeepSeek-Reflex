using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace DeepSeekFloat {
    [DataContract] internal sealed class BridgeRequest {
        [DataMember] public string id {get;set;}
        [DataMember] public string op {get;set;}
        [DataMember] public string mode {get;set;}
        [DataMember] public string text {get;set;}
        internal bool IsReply {get{return op=="reply";}}
        internal bool Valid {
            get {
                Guid parsed;
                if(!Guid.TryParseExact(id,"D",out parsed))return false;
                if(op==null)return ChatModes.All.Any(m=>m.Id==mode)&&!string.IsNullOrWhiteSpace(text)&&text.Length<=20000;
                // Reply reads carry no content; mode stays reserved for staging.
                return op=="reply"&&text==null;
            }
        }
    }
    [DataContract] internal sealed class BridgeReply {
        [DataMember] public string id;
        [DataMember] public string status;
        [DataMember(EmitDefaultValue=false)] public string text;
        internal BridgeReply(string requestId,string result):this(requestId,result,null){}
        internal BridgeReply(string requestId,string result,string payload){id=requestId;status=result;text=payload;}
    }
    internal sealed class BridgeResult {
        internal readonly string Status,Text;
        internal BridgeResult(string status,string text=null){Status=status;Text=text;}
    }
    // No disk state, network listener, arbitrary commands, URLs, or page scripts.
    internal sealed class ReflexBridge : IDisposable {
        internal const int MaxBytes=90000;
        private readonly CancellationTokenSource stopping=new CancellationTokenSource();
        private readonly Func<BridgeRequest,CancellationToken,Task<BridgeResult>> stage;
        private readonly object sync=new object();
        private NamedPipeServerStream current;
        private bool disposed;
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint pid);
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
        internal static string Executable {get{return Process.GetCurrentProcess().MainModule.FileName;}}
        internal static string PipeName {
            get {var identity=WindowsIdentity.GetCurrent().User.Value+"|"+Process.GetCurrentProcess().SessionId+"|"+Path.GetFullPath(Executable).ToLowerInvariant();using(var sha=SHA256.Create())return "DeepSeekReflex.v1."+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","");}
        }
        internal static PipeSecurity Security() {
            var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
            var user=WindowsIdentity.GetCurrent().User;security.SetOwner(user);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
            security.AddAccessRule(new PipeAccessRule(user,PipeAccessRights.FullControl,AccessControlType.Allow));return security;
        }
        internal static bool SamePeer(PipeStream pipe,bool server) {
            uint pid;
            try {
                if(!(server?GetNamedPipeServerProcessId(pipe.SafePipeHandle,out pid):GetNamedPipeClientProcessId(pipe.SafePipeHandle,out pid)))return false;
                using(var peer=Process.GetProcessById((int)pid))return peer.SessionId==Process.GetCurrentProcess().SessionId&&string.Equals(Path.GetFullPath(peer.MainModule.FileName),Path.GetFullPath(Executable),StringComparison.OrdinalIgnoreCase);
            }catch(Exception error){if(error is InvalidOperationException||error is System.ComponentModel.Win32Exception||error is ArgumentException)return false;throw;}
        }
        internal static byte[] Json(object value){using(var stream=new MemoryStream()){new DataContractJsonSerializer(value.GetType()).WriteObject(stream,value);return stream.ToArray();}}
        internal static T Parse<T>(byte[] value){using(var stream=new MemoryStream(value))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
        private static async Task Exact(Stream stream,byte[] data,CancellationToken cancel) {
            int offset=0;while(offset<data.Length){int count=await stream.ReadAsync(data,offset,data.Length-offset,cancel).ConfigureAwait(false);if(count==0)throw new EndOfStreamException();offset+=count;}
        }
        internal static async Task<byte[]> Read(Stream stream,int limit,CancellationToken cancel) {
            var header=new byte[4];await Exact(stream,header,cancel).ConfigureAwait(false);int size=BitConverter.ToInt32(header,0);
            if(size<1||size>limit)throw new InvalidDataException();var body=new byte[size];await Exact(stream,body,cancel).ConfigureAwait(false);return body;
        }
        internal static async Task Write(Stream stream,byte[] body,CancellationToken cancel) {
            var header=BitConverter.GetBytes(body.Length);await stream.WriteAsync(header,0,4,cancel).ConfigureAwait(false);await stream.WriteAsync(body,0,body.Length,cancel).ConfigureAwait(false);await stream.FlushAsync(cancel).ConfigureAwait(false);
        }
        internal ReflexBridge(Func<BridgeRequest,CancellationToken,Task<BridgeResult>> callback){stage=callback;Task.Run((Func<Task>)Serve);}
        private async Task Serve() {
            while(!stopping.IsCancellationRequested) {
                try {
                    using(var pipe=new NamedPipeServerStream(PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,Security())) {
                        lock(sync){if(disposed)return;current=pipe;}
                        await pipe.WaitForConnectionAsync(stopping.Token).ConfigureAwait(false);
                        if(!SamePeer(pipe,false))continue;
                        BridgeRequest request;
                        using(var read=CancellationTokenSource.CreateLinkedTokenSource(stopping.Token))using(read.Token.Register(delegate{pipe.Dispose();})) {read.CancelAfter(3000);request=Parse<BridgeRequest>(await Read(pipe,MaxBytes,read.Token).ConfigureAwait(false));}
                        string requestId=request==null?null:request.id;
                        BridgeResult result=new BridgeResult("invalid");
                        if(request!=null&&request.Valid)using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stopping.Token)) {
                            deadline.CancelAfter(7000);
                            var action=stage(request,deadline.Token);
                            if(await Task.WhenAny(action,Task.Delay(7000,stopping.Token)).ConfigureAwait(false)==action)result=await action.ConfigureAwait(false);
                            else{deadline.Cancel();result=new BridgeResult("outcome-unknown");}
                        }
                        using(var write=CancellationTokenSource.CreateLinkedTokenSource(stopping.Token))using(write.Token.Register(delegate{pipe.Dispose();})){write.CancelAfter(1000);await Write(pipe,Json(new BridgeReply(requestId,result.Status,result.Text)),write.Token).ConfigureAwait(false);}
                    }
                }catch(Exception error){
                    if(!(error is IOException||error is InvalidDataException||error is OperationCanceledException||error is ObjectDisposedException||error is SerializationException||error is UnauthorizedAccessException||error is ArgumentException))return;
                }finally{lock(sync){current=null;}}
                if(!stopping.IsCancellationRequested)await Task.Delay(50).ConfigureAwait(false);
            }
        }
        public void Dispose(){lock(sync){if(disposed)return;disposed=true;stopping.Cancel();if(current!=null)current.Dispose();}}
        // The fixed helper reads stdin before connecting; material never becomes argv.
        internal static int RunHelper() {
            string id=null;
            try {
                using(var deadline=new CancellationTokenSource(10500)) {
                    var read=Task.Run(async delegate {
                        using(var input=Console.OpenStandardInput())using(var buffer=new MemoryStream()) {
                            var chunk=new byte[4096];int count;
                            while((count=await input.ReadAsync(chunk,0,chunk.Length,deadline.Token).ConfigureAwait(false))>0){if(buffer.Length+count>MaxBytes)throw new InvalidDataException();buffer.Write(chunk,0,count);}
                            return Parse<BridgeRequest>(buffer.ToArray());
                        }
                    });
                    if(!read.Wait(3000))throw new TimeoutException();var request=read.GetAwaiter().GetResult();
                    if(request==null||!request.Valid)throw new InvalidDataException();id=request.id;
                    using(var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification)) {
                        using(deadline.Token.Register(delegate{pipe.Dispose();})) {
                        try{pipe.Connect(200);}catch(TimeoutException){using(var started=Process.Start(new ProcessStartInfo(Executable){UseShellExecute=false})){}pipe.Connect(2500);}
                        if(!SamePeer(pipe,true))throw new UnauthorizedAccessException();
                        Write(pipe,Json(request),deadline.Token).GetAwaiter().GetResult();
                        var response=Parse<BridgeReply>(Read(pipe,MaxBytes,deadline.Token).GetAwaiter().GetResult());
                        if(response==null||response.id!=id)throw new InvalidDataException();
                        Output(response);return 0;
                        }
                    }
                }
            }catch(Exception){Output(new BridgeReply(id,"outcome-unknown"));return 1;}
        }
        private static void Output(BridgeReply reply){try{var bytes=Json(reply);using(var output=Console.OpenStandardOutput())output.Write(bytes,0,bytes.Length);}catch(IOException){}}
    }
}
