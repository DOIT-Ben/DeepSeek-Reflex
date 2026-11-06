using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace DeepSeekFloat {
    internal static class AuditBoundaryTests {
        private static int passed,failed;
        private static void Check(bool value){if(!value)throw new Exception("boundary assertion failed");}
        private static void Case(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception error){failed++;Console.WriteLine("FAIL "+name+": "+error.GetType().Name+" "+error.Message);}}
        private static string PathFor(string file){return Path.Combine(Preferences.Root,file);}
        private static void Put(string file,string json){File.WriteAllText(PathFor(file),json);}
        private static string Custom(string id,string name,string instruction){return "{\"Id\":"+PromptInserter.Json(id)+",\"Name\":"+PromptInserter.Json(name)+",\"Instruction\":"+PromptInserter.Json(instruction)+"}";}
        [STAThread] private static int Main() {
            Directory.CreateDirectory(Preferences.Root);
            Case("mode loading rejects reserved builtin identities",delegate{
                Put("modes.json","{\"Version\":1,\"Custom\":["+Custom("normal","新模式","内容")+"]}");Check(ModeCatalog.Load().Custom.Count==0);
            });
            Case("mode loading normalizes names before checking uniqueness",delegate{
                Put("modes.json","{\"Version\":1,\"Custom\":["+Custom("m-1","同名","内容")+","+Custom("m-2"," 同名 ","内容")+"]}");Check(ModeCatalog.Load().Custom.Count==1);
            });
            Case("mode loading enforces display-name bounds and control characters",delegate{
                Put("modes.json","{\"Version\":1,\"Custom\":["+Custom("m-1",new string('字',17),"内容")+","+Custom("m-2","换\n行","内容")+"]}");Check(ModeCatalog.Load().Custom.Count==0);
            });
            Case("command loading rejects duplicate identities names and oversize instructions",delegate{
                Put("commands.json","{\"Version\":1,\"Commands\":["+Custom("c-1","条目","内容")+","+Custom("c-1","条目2","内容")+","+Custom("c-3"," 条目 ","内容")+","+Custom("c-4","超长",new string('字',2001))+"]}");Check(PromptCatalog.Load().Commands.Count(c=>c.Custom)==1);
            });
            Case("mode saving rejects invalid drafts without replacing a valid file",delegate{
                Put("modes.json","{\"Version\":1,\"Custom\":[]}");var before=File.ReadAllText(PathFor("modes.json"));var draft=new ModeCatalog.Catalog();draft.Custom.Add(new CustomChatMode{Id="normal",Name="测试",Instruction="内容"});Check(ModeCatalog.Save(draft)!=null&&File.ReadAllText(PathFor("modes.json"))==before);
            });
            Case("command saving rejects null fields without throwing or replacing data",delegate{
                Put("commands.json","{\"Version\":1,\"Commands\":[]}");var before=File.ReadAllText(PathFor("commands.json"));Check(PromptCatalog.Save(new[]{new PromptCommand{Id="c-1",Name=null,Instruction="内容"}})!=null&&File.ReadAllText(PathFor("commands.json"))==before);
            });
            Case("unreadable catalog reports a load error instead of a fresh catalog",delegate{
                File.Delete(PathFor("modes.json"));Directory.CreateDirectory(PathFor("modes.json"));try{Check(ModeCatalog.Load().Error!=null);}finally{Directory.Delete(PathFor("modes.json"));}
            });
            Case("catalog input has a bounded file size",delegate{
                Put("commands.json",new string(' ',524289)+"{\"Version\":1}");Check(PromptCatalog.Load().Error!=null);Put("modes.json",new string(' ',524289)+"{\"Version\":1}");Check(ModeCatalog.Load().Error!=null);
            });
            Case("maximum catalogs round trip without losing entries or unicode",delegate{
                var modes=new ModeCatalog.Catalog();for(int i=0;i<10;i++)modes.Custom.Add(new CustomChatMode{Id="m-"+i,Name="模式"+i,Instruction=new string('字',1998)+"😀"});Check(ModeCatalog.Save(modes)==null&&ModeCatalog.Load().Custom.Count==10);
                var commands=Enumerable.Range(0,20).Select(i=>new PromptCommand{Id="c-"+i,Name="指令"+i,Instruction=new string('字',2000)}).ToArray();Check(PromptCatalog.Save(commands)==null&&PromptCatalog.Load().Commands.Count(c=>c.Custom)==20);
            });
            Case("parallel atomic preference writes leave one whole value and no temporary files",delegate{
                Parallel.For(0,60,i=>Preferences.Write("parallel.json","payload-"+i+new string('x',4096)));var value=File.ReadAllText(PathFor("parallel.json"));Check(value.StartsWith("payload-")&&value.EndsWith(new string('x',4096))&&!Directory.GetFiles(Preferences.Root,"*.new*").Any());
            });
            Case("failed preference replacement removes its own temporary file",delegate{
                Directory.CreateDirectory(PathFor("blocked.json"));try{try{Preferences.Write("blocked.json","value");}catch(Exception error){if(!Preferences.IsStorageFailure(error))throw;}Check(!Directory.GetFiles(Preferences.Root,"*.new*").Any());}finally{Directory.Delete(PathFor("blocked.json"));}
            });
            Console.WriteLine("TOTAL "+passed+" passed, "+failed+" failed; isolated catalog and storage boundaries");return failed==0?0:1;
        }
    }
}
