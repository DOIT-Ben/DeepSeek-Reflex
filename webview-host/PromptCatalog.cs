using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
namespace DeepSeekFloat {
    internal sealed class PromptCommand {
        internal string Id,Name,Instruction;
        internal int BuiltinIndex=-1; // negative for local custom entries
        internal bool Custom {get{return BuiltinIndex<0;}}
    }
    // commands.json stores local custom capture instructions only; builtin
    // wording stays in PromptInserter and is never edited or deleted here.
    internal static class PromptCatalog {
        internal const int MaxCommands=20,MaxName=16,MaxInstruction=2000;
        private const string FileName="commands.json";
        internal static readonly PromptCommand[] Builtins={
            new PromptCommand{Id="translate",Name="翻译",BuiltinIndex=0},
            new PromptCommand{Id="explain",Name="解释",BuiltinIndex=1},
            new PromptCommand{Id="question",Name="提问",BuiltinIndex=2}
        };
        internal sealed class Catalog {
            internal PromptCommand[] Commands=Builtins.ToArray();
            internal string Error; // non-null when commands.json exists but is unusable
        }
        internal static Catalog Load() {
            var result=new Catalog();
            string text;
            try { text=File.ReadAllText(Path.Combine(Preferences.Root,FileName)); }
            catch(Exception error) { if(!Preferences.IsStorageFailure(error))throw; return result; }
            try {
                using(var memory=new MemoryStream(Encoding.UTF8.GetBytes(text))) {
                    var data=(CatalogData)new DataContractJsonSerializer(typeof(CatalogData)).ReadObject(memory);
                    if(data==null||data.Version!=1) { result.Error="指令文件版本不受支持，已只加载内置指令，原文件保持不变。"; return result; }
                    var list=new List<PromptCommand>();
                    if(data.Commands!=null)foreach(var item in data.Commands) {
                        if(item==null||string.IsNullOrWhiteSpace(item.Id)||string.IsNullOrWhiteSpace(item.Name)||string.IsNullOrWhiteSpace(item.Instruction))continue;
                        if(list.Count>=MaxCommands)break;
                        list.Add(new PromptCommand{Id=item.Id,Name=item.Name.Trim(),Instruction=item.Instruction.Trim(),BuiltinIndex=-1});
                    }
                    result.Commands=Builtins.Concat(list).ToArray();return result;
                }
            }
            catch(Exception error) {
                if(!(Preferences.IsStorageFailure(error)||error is SerializationException||error is ArgumentException))throw;
                result.Error="指令文件无法读取，已只加载内置指令，原文件保持不变。";return result;
            }
        }
        // Field-scoped validation; errors surface next to the editor field.
        internal static string ValidateName(string name,IEnumerable<PromptCommand> others,PromptCommand self) {
            if(name==null||(name=name.Trim()).Length==0)return "请输入指令名称。";
            if(name.Length>MaxName)return "名称不超过 16 个字。";
            foreach(var other in others) {
                if(self!=null&&Object.ReferenceEquals(other,self))continue;
                if(string.Equals(other.Name,name,StringComparison.OrdinalIgnoreCase))return "已有同名指令。";
            }
            return null;
        }
        internal static string ValidateInstruction(string instruction) {
            if(instruction==null||(instruction=instruction.Trim()).Length==0)return "请输入指令内容。";
            if(instruction.Length>MaxInstruction)return "指令不超过 2000 字。";
            return null;
        }
        internal static string Save(IEnumerable<PromptCommand> customs) {
            var list=customs.ToArray();
            if(list.Length>MaxCommands)return "自定义指令最多 20 条，当前 "+list.Length+" 条。";
            var data=new CatalogData{Version=1,Commands=list.Select(c=>new CommandData{Id=c.Id,Name=c.Name.Trim(),Instruction=c.Instruction.Trim()}).ToArray()};
            using(var memory=new MemoryStream()) {
                new DataContractJsonSerializer(typeof(CatalogData)).WriteObject(memory,data);
                try { Preferences.Write(FileName,Encoding.UTF8.GetString(memory.ToArray())); }
                catch(Exception error) { if(Preferences.IsStorageFailure(error))return "指令未能保存，请重试。"; throw; }
            }
            return null;
        }
        internal static string NewId(){return "c-"+Guid.NewGuid().ToString("N").Substring(0,12);}
    }
    [DataContract] internal sealed class CatalogData {
        [DataMember] internal int Version;
        [DataMember] internal CommandData[] Commands;
    }
    [DataContract] internal sealed class CommandData {
        [DataMember] internal string Id;
        [DataMember] internal string Name;
        [DataMember] internal string Instruction;
    }
}
