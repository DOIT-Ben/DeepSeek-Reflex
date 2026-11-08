using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DeepSeekFloat {
    internal static class UpdatePackage {
        internal static readonly string[] Managed={"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","WebView2Loader.dll","DeepSeek-Reflex.exe.config","WebView2-LICENSE.txt","WebView2-NOTICE.txt","LICENSE","THIRD-PARTY-NOTICES.md","VERSION","icon.ico","DeepSeek-Reflex.exe"};
        internal static readonly string[] Expected={"DeepSeek-Reflex.exe","DeepSeek-Reflex.exe.config","Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","WebView2Loader.dll","icon.ico","LICENSE","THIRD-PARTY-NOTICES.md","WebView2-LICENSE.txt","WebView2-NOTICE.txt","VERSION","README.md","README.en.md","QUICKSTART.txt","SHA256SUMS.txt","docs/assets/logo.png","docs/assets/screenshots/compact.png","docs/assets/screenshots/reading.png","docs/assets/screenshots/selected-text.png","docs/assets/screenshots/settings.png","docs/assets/screenshots/getting-started.png"};
        internal static void NoLinks(string path) {
            var current=Path.GetFullPath(path);
            while(!string.IsNullOrEmpty(current)){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("更新路径含链接。");current=Path.GetDirectoryName(current);}
        }
        internal static void VerifyBrand(string exe,Version version) {
            var info=FileVersionInfo.GetVersionInfo(exe);
            if(info.ProductName!="DeepSeek-Reflex"||info.ProductVersion!=version.ToString()||info.FileVersion!=version+".0")throw new InvalidDataException("程序版本或品牌不匹配。");
        }
        // Extract only after validating the complete central directory. Destination
        // must be a fresh task-owned directory; never the installation root.
        internal static void Extract(string zip,string destination,Version version,string digest) {
            NoLinks(zip);NoLinks(destination);if(Directory.Exists(destination)||File.Exists(destination))throw new InvalidDataException("暂存目录已存在。");
            using(var file=new FileStream(zip,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(file.Length>UpdateFeed.MaxDownload||!string.Equals(UpdateFeed.Hash(file),digest,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包校验失败。");file.Position=0;
                using(var archive=new ZipArchive(file,ZipArchiveMode.Read)) {
                    var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var expected=new HashSet<string>(Expected,StringComparer.Ordinal);long total=0;int count=0;
                    foreach(var entry in archive.Entries) {
                        string full=entry.FullName;
                        if(!seen.Add(full)||(entry.ExternalAttributes&0x400)!=0||((entry.ExternalAttributes>>16)&0xf000)==0xa000)throw new InvalidDataException("更新包存在重复或链接项。");
                        if(full.EndsWith("/",StringComparison.Ordinal)) {if(entry.Length!=0||(full!="DeepSeek-Reflex/docs/"&&full!="DeepSeek-Reflex/docs/assets/"&&full!="DeepSeek-Reflex/docs/assets/screenshots/"))throw new InvalidDataException("更新包目录无效。");continue;}
                        if(!full.StartsWith("DeepSeek-Reflex/",StringComparison.Ordinal)||!expected.Contains(full.Substring(16)))throw new InvalidDataException("更新包包含未知文件。");
                        if(entry.Length<0||entry.Length>32L*1024*1024||(total+=entry.Length)>128L*1024*1024)throw new InvalidDataException("解压大小超出限制。");count++;
                    }
                    if(count!=Expected.Length)throw new InvalidDataException("更新包文件缺失。");
                    Directory.CreateDirectory(destination);
                    foreach(var entry in archive.Entries) {
                        if(entry.FullName.EndsWith("/",StringComparison.Ordinal))continue;
                        string path=Path.Combine(destination,entry.FullName.Substring(16).Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using(var input=entry.Open())using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                            byte[] bytes=new byte[32768];long written=0;int read;while((read=input.Read(bytes,0,bytes.Length))>0){written+=read;if(written>entry.Length)throw new InvalidDataException("解压大小不符。");output.Write(bytes,0,read);}if(written!=entry.Length)throw new InvalidDataException("解压不完整。");output.Flush(true);
                        }
                    }
                }
            }
            var hashes=new HashSet<string>(StringComparer.Ordinal);var utf8=new UTF8Encoding(false,true);
            var lines=File.ReadAllLines(Path.Combine(destination,"SHA256SUMS.txt"),utf8);
            if(lines.Length!=Expected.Length-1)throw new InvalidDataException("包内校验清单无效。");
            foreach(string line in lines) {
                if(line.Length<67||line.Substring(64,2)!="  "||!UpdateFeed.ValidHash(line.Substring(0,64)))throw new InvalidDataException("包内校验格式无效。");
                string name=line.Substring(66);if(name=="SHA256SUMS.txt"||Array.IndexOf(Expected,name)<0||!hashes.Add(name)||!string.Equals(UpdateFeed.Hash(Path.Combine(destination,name.Replace('/',Path.DirectorySeparatorChar))),line.Substring(0,64),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("包内文件校验失败。");
            }
            if(File.ReadAllText(Path.Combine(destination,"VERSION"),utf8).Trim()!=version.ToString())throw new InvalidDataException("包版本不匹配。");VerifyBrand(Path.Combine(destination,"DeepSeek-Reflex.exe"),version);
        }
    }
}
