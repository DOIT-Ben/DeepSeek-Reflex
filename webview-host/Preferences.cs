using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DeepSeekFloat
{
    internal static class Preferences
    {
        internal static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cloud.doitbenai.deepseekfloat");
        internal static bool ReadBool(string name)
        {
            try { return File.ReadAllText(Path.Combine(Root,name)).Trim() == "true"; } catch (IOException) { return false; }
        }
        internal static bool AutomaticSelectionEnabled()
        {
            // V1 never enables the experimental global mouse hook on a fresh install.
            // Existing explicit preference is retained for compatibility.
            string path = Path.Combine(Root,"selection-popup-disabled.json");
            try { return File.ReadAllText(path).Trim() == "false"; }
            catch (IOException) { return false; }
        }
        internal static void Write(string name, string value)
        {
            Directory.CreateDirectory(Root);
            string path = Path.Combine(Root,name);
            string temporary = path + ".new";
            File.WriteAllText(temporary,value);
            if (File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
        }
        internal static Rectangle Bounds(float scale)
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            var result = new Rectangle(area.Right-(int)(460*scale), area.Top+(int)(24*scale), (int)(420*scale), (int)(740*scale));
            try
            {
                var parts = File.ReadAllText(Path.Combine(Root,"webview-bounds.txt")).Split(',');
                int x,y,w,h;
                if (parts.Length==4 && int.TryParse(parts[0],out x) && int.TryParse(parts[1],out y) && int.TryParse(parts[2],out w) && int.TryParse(parts[3],out h))
                    result = new Rectangle(x,y,Math.Max((int)(360*scale),w),Math.Max((int)(480*scale),h));
            }
            catch (IOException) { }
            area = Screen.FromRectangle(result).WorkingArea;
            result.Width = Math.Min(result.Width,area.Width);
            result.Height = Math.Min(result.Height,area.Height);
            result.X = Math.Max(area.Left, Math.Min(result.X, area.Right-result.Width));
            result.Y = Math.Max(area.Top, Math.Min(result.Y, area.Bottom-result.Height));
            return result;
        }
        internal static string LastPage()
        {
            try
            {
                var uri = new Uri(File.ReadAllText(Path.Combine(Root,"webview-last-page.txt")));
                if (uri.Scheme == "https" && uri.Host == "chat.deepseek.com") return uri.GetLeftPart(UriPartial.Path);
            }
            catch (Exception e) { if (!(e is IOException || e is UriFormatException)) throw; }
            return "https://chat.deepseek.com/";
        }
    }
}
