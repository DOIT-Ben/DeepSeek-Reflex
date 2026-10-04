using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
namespace DeepSeekFloat {
 internal static class ShellIdentity {
  // One stable native-host identity, separate from previous cached Shell entries.
  internal const string AppId="DOITBen.DeepSeekReflex.Windows";
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
  [DllImport("shell32.dll")] static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr id);
  [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
  [StructLayout(LayoutKind.Sequential)] struct PropertyKey {internal Guid format;internal uint id;}
  [StructLayout(LayoutKind.Explicit,Size=24)] struct Variant {[FieldOffset(0)] internal ushort type;[FieldOffset(8)] internal IntPtr text;}
  [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface PropertyStore {
   [PreserveSig] int GetCount(out uint count);
   [PreserveSig] int GetAt(uint index,out PropertyKey key);
   [PreserveSig] int GetValue(ref PropertyKey key,out Variant value);
   [PreserveSig] int SetValue(ref PropertyKey key,ref Variant value);
   [PreserveSig] int Commit();
  }
  // Only the first IShellLinkW slot is needed to verify the target before writing.
  [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface LinkTarget {
   [PreserveSig] int GetPath([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int count,IntPtr findData,uint flags);
  }
  static PropertyKey IdKey {get{return new PropertyKey{format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),id=5};}}
  internal static void InitializeProcess(){Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppId));}
  internal static string CurrentId {
   get{IntPtr text;int result=GetCurrentProcessExplicitAppUserModelID(out text);if(result<0)return null;try{return Marshal.PtrToStringUni(text);}finally{Marshal.FreeCoTaskMem(text);}}
  }
  internal static string ShortcutId(string path){return WithShortcut(path,false);}
  internal static void RegisterShortcut(string path){WithShortcut(path,true);}
  static string WithShortcut(string path,bool write) {
   path=Path.GetFullPath(path);
   if(!path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new IOException("Expected an existing shortcut");
   object link=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));
   try {
    var file=(IPersistFile)link;file.Load(path,write?2:0);
    if(write) {
     var target=new StringBuilder(1024);Marshal.ThrowExceptionForHR(((LinkTarget)link).GetPath(target,target.Capacity,IntPtr.Zero,0));
     if(!String.Equals(Path.GetFullPath(target.ToString()),Path.GetFullPath(Assembly.GetEntryAssembly().Location),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Shortcut targets another application");
    }
    var store=(PropertyStore)link;var key=IdKey;
    if(write) {
     var value=new Variant{type=31,text=Marshal.StringToCoTaskMemUni(AppId)};
     try{Marshal.ThrowExceptionForHR(store.SetValue(ref key,ref value));Marshal.ThrowExceptionForHR(store.Commit());file.Save(path,true);}finally{Marshal.FreeCoTaskMem(value.text);}
    }
    Variant read;Marshal.ThrowExceptionForHR(store.GetValue(ref key,out read));
    try{return read.type==31?Marshal.PtrToStringUni(read.text):null;}finally{PropVariantClear(ref read);}
   } finally {Marshal.FinalReleaseComObject(link);}
  }
 }
}
