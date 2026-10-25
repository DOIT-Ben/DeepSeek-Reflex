using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace DeepSeekFloat {
 internal static class IssueRegressionTests {
  static string root;static int passed;
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
  static void RunDesktop(Action checks){
   Exception failure=null;
   using(var dispatcher=new Control()){
    var handle=dispatcher.Handle;
    dispatcher.BeginInvoke(new Action(delegate{try{checks();}catch(Exception error){failure=error;}finally{Application.ExitThread();}}));
    Application.Run();
   }
   if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
  }
  static bool EqualPixels(Bitmap a,Bitmap b,Rectangle area){
   if(area.Width<=0||area.Height<=0||!new Rectangle(Point.Empty,a.Size).Contains(area)||!new Rectangle(Point.Empty,b.Size).Contains(area))return false;
   for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;
   return true;
  }
  static bool HasInk(Bitmap image,Rectangle area){for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(image.GetPixel(x,y).ToArgb()!=PanelTheme.Back.ToArgb())return true;return false;}
  static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,Private).GetValue(value);}
  static void Call(object value,string name,params object[] args){value.GetType().GetMethod(name,Private).Invoke(value,args);}
  static void Pump(int milliseconds){var clock=System.Diagnostics.Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(5);}}
  static void Until(Func<bool> ready){var clock=System.Diagnostics.Stopwatch.StartNew();while(!ready()){if(clock.ElapsedMilliseconds>5000)throw new Exception("Isolated feedback timeout");Pump(5);}}
  static void SendFeedback(FeedbackDialog dialog,string contact){
   dialog.Show();Pump(10);Field<TextBox>(dialog,"body").Text="QA synthetic feedback";Field<TextBox>(dialog,"contact").Text=contact;Field<PanelButton>(dialog,"send").PerformClick();
  }
  static void Feedback(){
   string captured=null;
   using(var dialog=new FeedbackDialog(delegate(string json){captured=json;return null;})){
    SendFeedback(dialog,"  QA remembered  ");Until(delegate{return Field<Label>(dialog,"thanksText").Visible;});
    Check(Preferences.ReadText("feedback-contact")=="QA remembered","successful nonempty contact is trimmed and remembered");
    Check(captured.Contains("\"contact\":\"QA remembered\""),"request contains the submitted trimmed contact");
   }
   using(var again=new FeedbackDialog(delegate{return null;})){Check(Field<TextBox>(again,"contact").Text=="QA remembered","successful contact prefills on reopening");}
   using(var dialog=new FeedbackDialog(delegate(string json){captured=json;return null;})){
    SendFeedback(dialog,"");Until(delegate{return Field<Label>(dialog,"thanksText").Visible;});
    Check(!captured.Contains("\"contact\""),"anonymous request omits contact");
    Check(Preferences.ReadText("feedback-contact")=="","successful clearing removes remembered contact");
   }
   using(var again=new FeedbackDialog(delegate{return null;})){Check(Field<TextBox>(again,"contact").Text=="","reopening after clearing stays empty");}
   foreach(string attempted in new[]{"","QA rejected"}){
    Preferences.Write("feedback-contact","QA previous");
    using(var dialog=new FeedbackDialog(delegate{return "QA unavailable";})){
     SendFeedback(dialog,attempted);Until(delegate{return Field<Label>(dialog,"status").Text.Contains("失败");});
     Check(Preferences.ReadText("feedback-contact")=="QA previous","failed send preserves previous memory for empty and nonempty inputs");
     Check(Field<PanelButton>(dialog,"send").Enabled,"failed send remains retryable by explicit user action");
    }
   }
   using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var finished=new ManualResetEventSlim()){
    using(var dialog=new FeedbackDialog(delegate(string json){captured=json;entered.Set();if(!release.Wait(3000))return "QA gate timeout";finished.Set();return null;})){
     try{
      SendFeedback(dialog,"  QA submitted  ");Until(delegate{return entered.IsSet;});Field<TextBox>(dialog,"contact").Text="QA unsent edit";release.Set();Until(delegate{return Field<Label>(dialog,"thanksText").Visible;});
      Check(captured.Contains("\"contact\":\"QA submitted\""),"request retains its submitted contact snapshot");
      Check(Preferences.ReadText("feedback-contact")=="QA submitted","memory records submitted contact rather than an edit during await");
     }finally{release.Set();Until(delegate{return finished.IsSet;});}
    }
   }
   Preferences.Write("feedback-contact","QA before local failure");
   string blocked=Path.Combine(Preferences.Root,"feedback-contact.new");Directory.CreateDirectory(blocked);int posts=0;
   try{
    using(var dialog=new FeedbackDialog(delegate{posts++;return null;})){
     SendFeedback(dialog,"");Until(delegate{return Field<Label>(dialog,"thanksText").Visible;});
     Check(posts==1,"preference write failure never replays a successful post");
     Check(Preferences.ReadText("feedback-contact")=="QA before local failure","failed local memory write preserves previous file");
     Check(Field<Label>(dialog,"thanksText").Visible,"successful post remains successful when local memory write fails");
    }
   }finally{Directory.Delete(blocked);}
   using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var finished=new ManualResetEventSlim()){
    Exception asyncError=null;ThreadExceptionEventHandler handler=delegate(object sender,ThreadExceptionEventArgs e){asyncError=e.Exception;};Application.ThreadException+=handler;
    try{
     using(var dialog=new FeedbackDialog(delegate{entered.Set();if(!release.Wait(3000))return "QA gate timeout";finished.Set();return null;})){
      try{SendFeedback(dialog,"QA closed window");Until(delegate{return entered.IsSet;});dialog.Close();release.Set();Until(delegate{return finished.IsSet;});Pump(50);Check(asyncError==null,"closing during a pending mock response does not throw on the UI thread");Check(Preferences.ReadText("feedback-contact")=="QA before local failure","closed window follows the existing no-mutation response guard");}
      finally{release.Set();}
     }
    }finally{Application.ThreadException-=handler;}
   }
  }
  static void Settings(){
   foreach(float scale in new[]{1f,1.5f,2f}){
    var area=new Rectangle(-1536,-80,1200,600);
    var fit=SettingsDialog.FitBounds(new Rectangle(-1700,-140,(int)(460*scale),(int)(578*scale)),area,scale);
    Check(area.Contains(fit),"negative-coordinate short work area fits at "+scale);
   }
   using(var dialog=new SettingsDialog(WindowSettings.Defaults(),delegate{return null;})){
    Check(!dialog.IsHandleCreated,"settings construction leaves native handle creation to modal owner assignment");
    dialog.Show();Pump(30);
    var area=Screen.FromControl(dialog).WorkingArea;
    float scale=Math.Max(2f,area.Height/500f);
    // Only this test window is scaled. No Windows display preference is changed.
    dialog.ApplyDpi(scale,new Rectangle(area.Left+20,area.Top+20,(int)(460*scale),(int)(578*scale)));Pump(30);
    Check(area.Contains(dialog.Bounds),"initial constrained settings fit the actual work area");
    var focus=Field<PanelSwitch>(dialog,"focus");focus.Checked=!focus.Checked;bool edited=focus.Checked;
    Rectangle stable=Rectangle.Empty;
    for(int cycle=0;cycle<3;cycle++){
     Call(dialog,"ShowCommandsPage",true);Pump(20);
     Check(area.Contains(dialog.Bounds),"commands page remains inside the work area");
     var commandsSave=Field<PanelButton>(dialog,"saveCommands");
     Check(area.Contains(commandsSave.RectangleToScreen(commandsSave.ClientRectangle)),"commands save remains on screen");
     Call(dialog,"ShowGeneralPage");Pump(20);
     Console.WriteLine("Settings bounds="+dialog.Bounds+" work area="+area+" scale="+scale);
     Check(area.Contains(dialog.Bounds),"returning to general settings remains inside the work area");
     foreach(string name in new[]{"save","cancel"}){var button=Field<PanelButton>(dialog,name);Check(area.Contains(button.RectangleToScreen(button.ClientRectangle)),name+" remains on screen");}
     Check(Field<Panel>(dialog,"body").VerticalScroll.Visible,"constrained general content scrolls");
     Check(focus.Checked==edited,"page navigation preserves the unsaved ordinary setting");
     if(cycle==0)stable=dialog.Bounds;else Check(dialog.Bounds==stable,"repeated navigation does not drift");
    }
    Call(dialog,"ShowCommandsPage",true);Call(dialog,"SaveCommands",(Action<string>)null);Pump(20);
    Check(!Field<bool>(dialog,"onCommandsPage")&&area.Contains(dialog.Bounds),"saving commands returns to a fitted general page");
    using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,dialog.ClientRectangle);image.Save(Path.Combine(root,"settings-constrained.png"));}
    Field<PanelButton>(dialog,"cancel").PerformClick();Check(dialog.DialogResult==DialogResult.Cancel,"cancel still closes without applying ordinary settings");
   }
   using(var commands=new SettingsDialog(WindowSettings.Defaults(),delegate{return null;},null,"commands")){
    commands.Show();Pump(20);Check(Field<bool>(commands,"onCommandsPage")&&Screen.FromControl(commands).WorkingArea.Contains(commands.Bounds),"direct commands startup remains fitted");commands.Close();
   }
  }
  static void Menu(){
   int index=0;
   using(var a=new Bitmap(2,2))using(var b=new Bitmap(2,2)){
    Check(!EqualPixels(a,b,Rectangle.Empty),"empty pixel regions cannot pass");
    Check(!EqualPixels(a,b,new Rectangle(0,0,-1,1)),"negative pixel regions cannot pass");
    Check(!EqualPixels(a,b,new Rectangle(1,1,2,2)),"out-of-bounds pixel regions cannot pass");
   }
   foreach(float scale in new[]{1f,1.5f,2f})foreach(string name in new[]{"普通对话","翻译模式","解释模式","理解提问","润色模式","总结模式","A&B","超长名称"+new string('字',32)})foreach(bool selected in new[]{false,true}){
    using(var menu=new TrayMenu(scale)){
     var item=menu.AddAction(name,delegate{});item.Checked=selected;
     var rect=new Rectangle(menu.Side+menu.S(20),0,item.Width-menu.Side*2-menu.S(20),item.Height);
     using(var actual=new Bitmap(item.Width,item.Height))using(var reference=new Bitmap(item.Width,item.Height)){
      using(var g=Graphics.FromImage(actual)){
       g.Clear(PanelTheme.Back);
       var args=new ToolStripItemTextRenderEventArgs(g,item,name,rect,PanelTheme.Ink,menu.Font,TextFormatFlags.Left);
       typeof(TrayRenderer).GetMethod("OnRenderItemText",Private).Invoke(menu.Renderer,new object[]{args});
      }
      using(var g=Graphics.FromImage(reference)){
       g.Clear(PanelTheme.Back);
       var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix;
       if(name.StartsWith("超长"))flags|=TextFormatFlags.EndEllipsis;
       TextRenderer.DrawText(g,name,menu.Font,rect,PanelTheme.Ink,flags);
      }
      string stem="menu-"+(index++);
      actual.Save(Path.Combine(root,stem+"-actual.png"));reference.Save(Path.Combine(root,stem+"-reference.png"));
      Check(rect.Width>0&&HasInk(actual,rect)&&HasInk(reference,rect),"actual and reference both draw visible text");
      Check(EqualPixels(actual,reference,rect),"complete rendered menu text at "+scale+" checked="+selected+" name="+name.Substring(0,Math.Min(8,name.Length)));
      bool mark=false;
      for(int y=0;y<item.Height;y++)for(int x=menu.Side;x<rect.Left;x++)if(actual.GetPixel(x,y).ToArgb()!=PanelTheme.Back.ToArgb())mark=true;
      Check(mark==selected,"check gutter remains separate from text");
      if(name.StartsWith("超长"))Check(menu.Width==menu.S(300)&&TextRenderer.MeasureText(name,menu.Font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width>rect.Width,"long names actually exceed the positive width cap");
     }
    }
   }
  }
  [STAThread] static int Main(string[] args){
   try{
    Console.OutputEncoding=new System.Text.UTF8Encoding(false);
    root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
    Check(Path.GetFullPath(Preferences.Root).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"preferences are isolated before any production static initialization");
    Application.EnableVisualStyles();
    RunDesktop(delegate{if(args[1]=="Menu")Menu();else if(args[1]=="Settings")Settings();else if(args[1]=="Feedback")Feedback();else throw new Exception("Unknown issue case");});
    Console.WriteLine("TOTAL "+passed+" issue checks");return 0;
   }catch(Exception error){Console.WriteLine(error);return 1;}
  }
 }
}
