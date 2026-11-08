using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
namespace DeepSeekFloat {
    [DataContract] internal sealed class UpdateEnvelope {
        [DataMember] public string payload {get;set;}
        [DataMember] public string signature {get;set;}
    }
    [DataContract] internal sealed class SignedUpdateDocument {
        [DataMember] public int schema {get;set;}
        [DataMember] public long issued {get;set;}
        [DataMember] public long expires {get;set;}
        [DataMember] public UpdateRelease release {get;set;}
    }
    [DataContract] internal sealed class UpdateHighWater {
        [DataMember] public string version {get;set;}
        [DataMember] public string digest {get;set;}
    }
    internal static class SignedUpdateFeed {
        internal const string Root="https://test.doitbenai.cloud/reflex/updates/";
        internal const string Latest=Root+"stable.json";
        private sealed class Frame {internal HashSet<string> Names;internal Frame(bool obj){Names=obj?new HashSet<string>(StringComparer.Ordinal):null;}}
        private static void JsonShape(byte[] json) {
            new UTF8Encoding(false,true).GetString(json);
            var quota=new XmlDictionaryReaderQuotas{MaxDepth=32,MaxStringContentLength=1024*1024,MaxArrayLength=1024*1024};
            using(var reader=JsonReaderWriterFactory.CreateJsonReader(json,quota)) {
                var stack=new Stack<Frame>();
                while(reader.Read()) {
                    if(reader.NodeType==XmlNodeType.Element) {
                        if(stack.Count>0&&stack.Peek().Names!=null&&!stack.Peek().Names.Add(reader.GetAttribute("item")??reader.LocalName))throw new InvalidDataException("更新清单包含重复字段。");
                        var frame=new Frame(reader.GetAttribute("type")=="object");if(!reader.IsEmptyElement)stack.Push(frame);
                    } else if(reader.NodeType==XmlNodeType.EndElement&&stack.Count>0)stack.Pop();
                }
            }
        }
        private static T Deserialize<T>(byte[] json) {JsonShape(json);using(var memory=new MemoryStream(json))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(memory);}
        internal static SignedUpdateDocument Parse(byte[] envelope,string publicKey,DateTime now) {
            try {
                if(envelope==null||envelope.Length>1024*1024)throw new InvalidDataException("更新清单过大。");
                var outer=Deserialize<UpdateEnvelope>(envelope);
                if(outer==null||outer.payload==null||outer.signature==null||outer.payload.Length>700000||outer.signature.Length>400)throw new InvalidDataException("更新清单格式错误。");
                byte[] data=Convert.FromBase64String(outer.payload),signature=Convert.FromBase64String(outer.signature);
                if(data.Length>512*1024||signature.Length!=256)throw new InvalidDataException("更新签名格式错误。");
                using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKey);if(rsa.KeySize!=2048||!rsa.VerifyData(data,CryptoConfig.MapNameToOID("SHA256"),signature))throw new InvalidDataException("更新签名验证失败。");}
                var doc=Deserialize<SignedUpdateDocument>(data);long timestamp=(long)(now.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
                if(doc==null||doc.schema!=1||doc.issued<0||doc.expires<=doc.issued||doc.expires-doc.issued>30L*86400)throw new InvalidDataException("更新清单协议或有效期无效。");
                if(doc.issued>timestamp+300||doc.expires<=timestamp)throw new InvalidDataException("更新清单已过期或尚未生效，请校对系统时间并联系发布者续期。");
                UpdateFeed.ValidateRelease(doc.release,true);return doc;
            }catch(Exception e){if(e is InvalidDataException)throw;if(e is FormatException||e is SerializationException||e is XmlException||e is CryptographicException||e is DecoderFallbackException)throw new InvalidDataException("更新清单或签名无效。",e);throw;}
        }
        internal static void Remember(UpdateRelease release,string statePath) {
            // Fail closed on corrupt/unwritable anti-rollback state. No browser or
            // existing application preference is read or modified here.
            lock(typeof(SignedUpdateFeed)) {
                if(File.Exists(statePath)) {
                    var old=UpdateTransaction.Read<UpdateHighWater>(statePath);
                    if(old==null||old.version==null||old.digest==null||!old.digest.StartsWith("sha256:",StringComparison.Ordinal)||!UpdateFeed.ValidHash(old.digest.Substring(7)))throw new InvalidDataException("更新验证状态损坏。");
                    Version previous=UpdateFeed.ParseVersion(old.version);
                    if(previous>release.Version||(previous==release.Version&&!string.Equals(old.digest,release.Asset.digest,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("拒绝旧版或同版不同内容的更新清单。");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(statePath));UpdateTransaction.Write(statePath,new UpdateHighWater{version=release.Version.ToString(),digest=release.Asset.digest});
            }
        }
        internal static UpdateRelease Check(CancellationToken token) {
            var doc=Parse(UpdateFeed.Read(Latest,1024*1024,token,null,null),UpdateSigningKey.PublicXml,DateTime.UtcNow);
            token.ThrowIfCancellationRequested();Remember(doc.release,Path.Combine(UpdateTransaction.TaskRoot,"signed-feed-state.json"));
            if(doc.expires-(long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds<7L*86400)doc.release.MaintenanceWarning="更新清单将在7日内到期，请提醒发布者续期。";
            return doc.release;
        }
    }
}
