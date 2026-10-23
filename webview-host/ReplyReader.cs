using System;
using System.Runtime.Serialization;
namespace DeepSeekFloat {
    internal sealed class WebsiteReply {
        internal string Status,Text;
    }
    [DataContract] internal sealed class ReplyPayload {
        [DataMember(Name="status")] internal string Status {get;set;}
        [DataMember(Name="text")] internal string Text {get;set;}
    }
    // Passive read of the last assistant reply: never focuses, sends, scrolls or writes.
    // Structural mismatch degrades to "unsupported" and the manual copy path stays.
    internal static class ReplyReader {
        internal const int MaxChars=20000;
        internal static readonly string Script=@"(location.origin==='https://chat.deepseek.com'?(function(){
const blocks=document.querySelectorAll('.ds-markdown.ds-markdown--block');
if(!blocks.length)return {status:'unsupported'};
const text=(blocks[blocks.length-1].innerText||'').trim();
if(!text)return {status:'empty'};
if(text.length>"+MaxChars+@")return {status:'overflow'};
return {status:'ok',text:text};})():{status:'wrong-host'})";
        internal static WebsiteReply Parse(string raw) {
            try {
                var payload=ReflexBridge.Parse<ReplyPayload>(System.Text.Encoding.UTF8.GetBytes(raw.Trim()));
                if(payload==null||string.IsNullOrEmpty(payload.Status))return new WebsiteReply{Status="unsupported"};
                return new WebsiteReply{Status=payload.Status,Text=payload.Text};
            }catch(Exception){return new WebsiteReply{Status="unsupported"};}
        }
    }
}
