using System;
using System.IO;
using System.Windows.Forms;
namespace DeepSeekFloat {
 // Compile the unchanged production capture source with in-memory adapters.
 internal static class Clipboard {
  internal static string Text;
  internal static IDataObject GetDataObject(){var value=new DataObject();value.SetData(DataFormats.UnicodeText,false,Text);return value;}
  internal static bool ContainsText(){return Text!=null;}
  internal static string GetText(){return Text;}
  internal static void SetDataObject(DataObject value,bool persist){Text=(string)value.GetData(DataFormats.UnicodeText,false);Native.Sequence++;}
  internal static void Clear(){Text=null;Native.Sequence++;}
 }
 internal static class Native {
  internal struct INPUT{internal int Marker;}
  internal static uint Sequence;
  internal static int Scenario;
  internal static IntPtr Foreground,Owner;
  internal static uint GetClipboardSequenceNumber(){return Sequence;}
  internal static IntPtr GetClipboardOwner(){return Owner;}
  internal static IntPtr GetForegroundWindow(){return Foreground;}
  internal static short GetAsyncKeyState(int key){return 0;}
  internal static uint GetWindowThreadProcessId(IntPtr window,out uint pid){pid=window==new IntPtr(3)?333333u:424242u;return 1;}
  internal static INPUT Key(int key,bool up){return new INPUT{Marker=key};}
  internal static uint SendInput(uint count,INPUT[] inputs,int size){
   Clipboard.Text=Scenario==2?"OTHER APP TEXT":"SELECTED TEXT";Sequence++;Owner=new IntPtr(Scenario==2?3:1);
   if(Scenario!=0)Foreground=new IntPtr(2);
   return count;
  }
 }
 internal static class ClipboardBoundary {
  static void Run(int scenario) {
   Clipboard.Text="ORIGINAL";Native.Sequence=100;Native.Foreground=new IntPtr(1);Native.Owner=new IntPtr(3);Native.Scenario=scenario;
   var result=SelectionCapture.CopySelectionAsync(new IntPtr(1)).GetAwaiter().GetResult();
   string expected=scenario==2?"OTHER APP TEXT":"ORIGINAL";
   if(Clipboard.Text!=expected)throw new Exception("Clipboard preservation failed: "+scenario);
   if(scenario==0&&result.Text!="SELECTED TEXT")throw new Exception("Successful copy text missing");
   if(scenario!=0&&result.Text!=null)throw new Exception("Cancelled capture returned text");
   Console.WriteLine("PASS in-memory clipboard case "+scenario+": "+(scenario==2?"third-party update preserved":"original restored"));
  }
  [STAThread] static int Main(){try{Run(0);Run(1);Run(2);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
