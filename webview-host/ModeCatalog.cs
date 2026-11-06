using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
namespace DeepSeekFloat {
    internal sealed class CustomChatMode {
        internal string Id,Name,Instruction;
    }
    // modes.json stores two things: instruction overrides for builtin modes
    // and fully custom modes. Builtin ids and names are never rewritten (the
    // DSH bundle snapshots ChatModes.All at build time), "normal" carries no
    // instruction by definition, and an unusable file falls back to builtins
    // only, leaving the original file untouched — the same contract as
    // commands.json.
    internal static class ModeCatalog {
        internal const int MaxCustomModes=10,MaxName=16,MaxInstruction=2000;
        private const string FileName="modes.json";
        internal sealed class Catalog {
            internal readonly Dictionary<string,string> Overrides=new Dictionary<string,string>();
            internal readonly List<CustomChatMode> Custom=new List<CustomChatMode>();
            internal string Error; // non-null when modes.json exists but is unusable
        }
        internal static bool Overridable(string id){return id!="normal"&&ChatModes.All.Any(m=>m.Id==id);}
        internal static Catalog Load() {
            var result=new Catalog();
            string text;
            try { text=Preferences.ReadCatalog(FileName); }
            catch(FileNotFoundException){return result;}
            catch(DirectoryNotFoundException){return result;}
            catch(Exception error) { if(!Preferences.IsStorageFailure(error)&&!(error is InvalidDataException))throw;result.Error="模式文件无法读取，已只加载内置模式，原文件保持不变。";return result; }
            try {
                using(var memory=new MemoryStream(Encoding.UTF8.GetBytes(text))) {
                    var data=(ModesData)new DataContractJsonSerializer(typeof(ModesData)).ReadObject(memory);
                    if(data==null||data.Version!=1) { result.Error="模式文件版本不受支持，已只加载内置模式，原文件保持不变。"; return result; }
                    if(data.Overrides!=null)foreach(var item in data.Overrides) {
                        if(item==null||!Overridable(item.Id)||!ValidInstruction(item.Instruction)||result.Overrides.ContainsKey(item.Id))continue;
                        result.Overrides[item.Id]=item.Instruction.Trim();
                    }
                    if(data.Custom!=null)foreach(var item in data.Custom) {
                        if(item==null||!ValidCustomId(item.Id)||ValidateName(item.Name,result.Custom,null)!=null||!ValidInstruction(item.Instruction))continue;
                        if(result.Custom.Count>=MaxCustomModes)break;
                        if(result.Custom.Any(c=>c.Id==item.Id.Trim()))continue;
                        result.Custom.Add(new CustomChatMode{Id=item.Id.Trim(),Name=item.Name.Trim(),Instruction=item.Instruction.Trim()});
                    }
                    return result;
                }
            }
            catch(Exception error) {
                if(!(Preferences.IsStorageFailure(error)||error is SerializationException||error is ArgumentException))throw;
                result.Error="模式文件无法读取，已只加载内置模式，原文件保持不变。";return result;
            }
        }
        // Field-scoped validation; errors surface next to the editor field.
        internal static string ValidateName(string name,IEnumerable<CustomChatMode> others,CustomChatMode self) {
            if(name==null||(name=name.Trim()).Length==0)return "请输入模式名称。";
            if(name.Length>MaxName)return "名称不超过 16 个字。";
            if(name.Any(char.IsControl))return "名称不能包含换行或控制字符。";
            if(ChatModes.All.Any(m=>string.Equals(m.Name,name,StringComparison.OrdinalIgnoreCase)))return "与内置模式同名。";
            foreach(var other in others) {
                if(self!=null&&Object.ReferenceEquals(other,self))continue;
                if(string.Equals(other.Name==null?null:other.Name.Trim(),name,StringComparison.OrdinalIgnoreCase))return "已有同名模式。";
            }
            return null;
        }
        internal static string ValidateInstruction(string instruction) {
            var trimmed=instruction==null?"":instruction.Trim();
            if(trimmed.Length==0)return "请输入提示词。";
            return trimmed.Length>MaxInstruction?"提示词不超过 "+MaxInstruction+" 字。":null;
        }
        private static bool ValidInstruction(string instruction){return ValidateInstruction(instruction)==null;}
        private static bool ValidCustomId(string id){return !string.IsNullOrWhiteSpace(id)&&id.Trim().Length<=64&&!id.Any(char.IsControl)&&!ChatModes.IsBuiltin(id.Trim());}
        internal static string Save(Catalog catalog) {
            if(catalog==null)return "模式列表无法保存。";
            if(catalog.Custom.Count>MaxCustomModes)return "自定义模式最多 "+MaxCustomModes+" 个，当前 "+catalog.Custom.Count+" 个。";
            var seen=new List<CustomChatMode>();
            foreach(var item in catalog.Custom) {
                if(item==null||!ValidCustomId(item.Id)||seen.Any(c=>c.Id.Trim()==item.Id.Trim()))return "模式标识无效或重复。";
                string error=ValidateName(item.Name,seen,null)??ValidateInstruction(item.Instruction);if(error!=null)return error;
                seen.Add(item);
            }
            if(catalog.Overrides.Any(p=>Overridable(p.Key)&&!ValidInstruction(p.Value)))return "内置模式提示词无效。";
            var data=new ModesData {
                Version=1,
                Overrides=catalog.Overrides.Where(p=>Overridable(p.Key)&&ValidInstruction(p.Value)).Select(p=>new ModeOverrideData{Id=p.Key,Instruction=p.Value.Trim()}).ToArray(),
                Custom=catalog.Custom.Select(c=>new CustomModeData{Id=c.Id.Trim(),Name=c.Name.Trim(),Instruction=c.Instruction.Trim()}).ToArray()
            };
            using(var memory=new MemoryStream()) {
                new DataContractJsonSerializer(typeof(ModesData)).WriteObject(memory,data);
                try { Preferences.Write(FileName,Encoding.UTF8.GetString(memory.ToArray())); }
                catch(Exception error) { if(Preferences.IsStorageFailure(error))return "模式未能保存，请重试。"; throw; }
            }
            return null;
        }
        internal static string NewId(){return "m-"+Guid.NewGuid().ToString("N").Substring(0,12);}
    }
    [DataContract] internal sealed class ModesData {
        [DataMember] internal int Version;
        [DataMember] internal ModeOverrideData[] Overrides;
        [DataMember] internal CustomModeData[] Custom;
    }
    [DataContract] internal sealed class ModeOverrideData {
        [DataMember] internal string Id;
        [DataMember] internal string Instruction;
    }
    [DataContract] internal sealed class CustomModeData {
        [DataMember] internal string Id;
        [DataMember] internal string Name;
        [DataMember] internal string Instruction;
    }
}
