using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography;

namespace DeepSeekFloat {
    [DataContract] internal sealed class UpdateAsset {
        [DataMember] public string name {get;set;}
        [DataMember] public string browser_download_url {get;set;}
        [DataMember] public long size {get;set;}
        [DataMember] public string digest {get;set;}
    }
    [DataContract] internal sealed class UpdateRelease {
        [DataMember] public string tag_name {get;set;}
        [DataMember] public string body {get;set;}
        [DataMember] public bool draft {get;set;}
        [DataMember] public bool prerelease {get;set;}
        [DataMember] public UpdateAsset[] assets {get;set;}
        internal Version Version;
        internal UpdateAsset Asset;
        internal string MaintenanceWarning=null;
    }
    internal static class UpdateFeed {
        internal const string Latest="https://api.github.com/repos/DOIT-Ben/DeepSeek-Reflex/releases/latest";
        internal const long MaxDownload=64L*1024*1024;
        internal static Version ParseVersion(string value) {
            if(value==null||!Regex.IsMatch(value,@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$"))throw new InvalidDataException("版本格式无效。");
            Version result;if(!Version.TryParse(value,out result))throw new InvalidDataException("版本超出范围。");return result;
        }
        internal static string Hash(string path) {using(var f=File.OpenRead(path))return Hash(f);}
        internal static string Hash(Stream stream) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        internal static bool ValidHash(string value) {return value!=null&&Regex.IsMatch(value,"^[a-fA-F0-9]{64}$");}
        internal static void ValidateUri(Uri uri) {
            if(uri==null||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.Fragment.Length!=0||
                !(uri.Host=="github.com"||uri.Host=="api.github.com"||uri.Host=="release-assets.githubusercontent.com"||uri.Host=="objects.githubusercontent.com"||(uri.Host=="test.doitbenai.cloud"&&uri.AbsolutePath.StartsWith("/reflex/updates/",StringComparison.Ordinal))))throw new InvalidDataException("更新地址不可信。");
        }
        internal static UpdateRelease Parse(byte[] json) {
            if(json.Length>1024*1024)throw new InvalidDataException("更新信息过大。");
            UpdateRelease result;
            try{using(var s=new MemoryStream(json))result=(UpdateRelease)new DataContractJsonSerializer(typeof(UpdateRelease)).ReadObject(s);}
            catch(Exception e){if(!(e is SerializationException)&&!(e is System.Xml.XmlException))throw;throw new InvalidDataException("更新信息无效。",e);}
            return ValidateRelease(result,false);
        }
        internal static UpdateRelease ValidateRelease(UpdateRelease result,bool mirror) {
            if(result==null||result.draft||result.prerelease||result.tag_name==null||!result.tag_name.StartsWith("v",StringComparison.Ordinal))throw new InvalidDataException("不是正式版本。");
            result.Version=ParseVersion(result.tag_name.Substring(1));
            string expected="DeepSeek-Reflex-"+result.Version+"-Windows-x64.zip";
            if(result.assets==null)throw new InvalidDataException("缺少更新包。");
            foreach(var a in result.assets)if(a!=null&&a.name==expected){if(result.Asset!=null)throw new InvalidDataException("重复的更新包。");result.Asset=a;}
            var asset=result.Asset;
            if(asset==null||asset.size<=0||asset.size>MaxDownload||asset.digest==null||!asset.digest.StartsWith("sha256:",StringComparison.Ordinal)||!ValidHash(asset.digest.Substring(7)))throw new InvalidDataException("更新包缺少有效的大小或 SHA256 校验。");
            string url="https://github.com/DOIT-Ben/DeepSeek-Reflex/releases/download/"+result.tag_name+"/"+expected;
            if(mirror)url=SignedUpdateFeed.Root+"releases/"+result.Version+"/"+expected;
            if(asset.browser_download_url!=url)throw new InvalidDataException("更新包来源不匹配。");
            ValidateUri(new Uri(url));return result;
        }
        // The injected request factory is internal test instrumentation. Production
        // callers always use HTTPS and the fixed repository; no URL CLI override.
        internal static byte[] Read(string url,long max,CancellationToken token,Action<long> progress,Stream output,Func<Uri,HttpWebRequest> factory=null) {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            DateTime deadline=DateTime.UtcNow.AddMinutes(output==null?1:5);
            var uri=new Uri(url);
            for(int redirects=0;redirects<=5;redirects++) {
                if(factory==null)ValidateUri(uri);token.ThrowIfCancellationRequested();
                var request=factory==null?(HttpWebRequest)WebRequest.Create(uri):factory(uri);
                request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;request.UserAgent="DeepSeek-Reflex-Updater";
                request.Headers["X-GitHub-Api-Version"]="2026-03-10";request.Accept="application/vnd.github+json";
                using(token.Register(request.Abort)) {
                    try {
                        using(var response=(HttpWebResponse)request.GetResponse()) {
                            int code=(int)response.StatusCode;
                            if(code>=300&&code<400){if(redirects==5)throw new InvalidDataException("更新地址重定向过多。");uri=new Uri(uri,response.Headers["Location"]);continue;}
                            if(code!=200)throw new WebException("更新服务暂不可用。");
                            if(response.ContentLength>max)throw new InvalidDataException("下载大小超出限制。");
                            using(var memory=new MemoryStream())using(var input=response.GetResponseStream()) {
                                var target=output??memory;byte[] buffer=new byte[32768];long total=0;int read;
                                while((read=input.Read(buffer,0,buffer.Length))!=0){token.ThrowIfCancellationRequested();if(DateTime.UtcNow>deadline)throw new TimeoutException("下载超时。");total+=read;if(total>max)throw new InvalidDataException("下载大小超出限制。");target.Write(buffer,0,read);if(progress!=null)progress(total);}
                                if(response.ContentLength>=0&&response.ContentLength!=total)throw new InvalidDataException("下载未完成。");
                                return output==null?memory.ToArray():null;
                            }
                        }
                    }catch(WebException){token.ThrowIfCancellationRequested();throw;}
                }
            }
            throw new InvalidDataException("更新地址无效。");
        }
        internal static UpdateRelease Check(CancellationToken token) {return SignedUpdateFeed.Check(token);}
        internal static void Download(UpdateRelease release,string destination,CancellationToken token,Action<long> progress,Func<Uri,HttpWebRequest> factory=null) {
            string part=destination+".part";bool created=false;
            try {
                using(var output=new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    created=true;
                    Read(release.Asset.browser_download_url,release.Asset.size,token,progress,output,factory);output.Flush(true);
                }
                token.ThrowIfCancellationRequested();
                if(new FileInfo(part).Length!=release.Asset.size||!string.Equals(Hash(part),release.Asset.digest.Substring(7),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包校验失败，请重新下载。");
                File.Move(part,destination);
            } finally {if(created&&File.Exists(part))File.Delete(part);}
        }
    }
}
