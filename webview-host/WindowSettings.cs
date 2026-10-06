using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
namespace DeepSeekFloat
{
    [DataContract]
    internal sealed class WindowSettings
    {
        [DataMember] internal int Version=1;
        [DataMember] internal int ToggleKeys;
        [DataMember] internal int CaptureKeys;
        [DataMember] internal bool FocusOnOpen=true;
        [DataMember] internal bool HideToTrayOnToggle;
        [DataMember] internal string Mode="custom";
        internal WindowSettings Clone() { return (WindowSettings)MemberwiseClone(); }
        internal static WindowSettings Defaults()
        {
            return new WindowSettings {
                ToggleKeys=(int)(Keys.Control|Keys.Space|(Preferences.ReadBool("shortcut.json")?Keys.Alt:Keys.None)),
                CaptureKeys=(int)(Keys.Control|Keys.D|(Preferences.ReadBool("capture-shortcut.json")?Keys.Alt:Keys.Shift))
            };
        }
        internal static WindowSettings Load()
        {
            try {
                using(var file=File.OpenRead(Path.Combine(Preferences.Root,"window-settings.json"))) {
                    var settings=(WindowSettings)new DataContractJsonSerializer(typeof(WindowSettings)).ReadObject(file);
                    if(settings!=null && settings.Version==1 && HotkeyBindings.Validate(settings.ToggleKeys,settings.CaptureKeys)==null && (settings.Mode=="custom"||settings.Mode=="compact"||settings.Mode=="reading"))return settings;
                }
            }catch(Exception e) { if(!(Preferences.IsStorageFailure(e) || e is SerializationException || e is ArgumentException))throw; }
            return Defaults();
        }
        internal void Save()
        {
            using(var memory=new MemoryStream()) {
                new DataContractJsonSerializer(typeof(WindowSettings)).WriteObject(memory,this);
                Preferences.Write("window-settings.json",Encoding.UTF8.GetString(memory.ToArray()));
            }
        }
    }
}
