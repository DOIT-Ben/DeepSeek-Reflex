using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeepSeekFloat
{
    internal sealed class ChatClient
    {
        internal static readonly ChatClient DeepSeek = new ChatClient("deepseek", "DeepSeek", "https://chat.deepseek.com/", "deepseek.com", "textarea#chat-input,textarea[placeholder*=\"发送消息\"],#chat-input[contenteditable=true]", true);
        internal static readonly ChatClient Doubao = new ChatClient("doubao", "豆包", "https://www.doubao.com/chat/", "doubao.com", "textarea[data-testid=\"chat_input_input\"]", true);
        internal static readonly ChatClient Qianwen = new ChatClient("qianwen", "千问", "https://www.qianwen.com/", "qianwen.com", "[contenteditable=true][role=textbox][data-slate-editor=true]", false);
        internal static readonly ChatClient Kimi = new ChatClient("kimi", "Kimi", "https://www.kimi.com/", "kimi.com", ".chat-input-editor[contenteditable=true][role=textbox]", false);
        internal static readonly ChatClient[] All = { DeepSeek, Doubao, Qianwen, Kimi };

        internal readonly string Id, Name, Domain, EditorSelector;
        internal readonly Uri Home;
        internal readonly bool SupportsModes;

        private ChatClient(string id, string name, string home, string domain, string editor, bool supportsModes)
        {
            Id = id;
            Name = name;
            Home = new Uri(home);
            Domain = domain;
            EditorSelector = editor;
            SupportsModes = supportsModes;
        }

        internal static ChatClient Selected()
        {
            string id = Preferences.ReadText("chat-client.txt");
            foreach (var client in All) if (client.Id == id) return client;
            return DeepSeek;
        }

        internal static string UserDataDirectory
        {
            get { return Path.Combine(Preferences.Root, "webview2-profile"); }
        }

        internal Task InitializeView(WebView2 view, CoreWebView2Environment environment)
        {
            // The default profile is the one existing installations already use.
            if (this == DeepSeek) return view.EnsureCoreWebView2Async(environment);
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.ProfileName = "reflex-" + Id;
            return view.EnsureCoreWebView2Async(environment, options);
        }

        private string LastPageFile
        {
            get { return this == DeepSeek ? "webview-last-page.txt" : "webview-last-page-" + Id + ".txt"; }
        }

        internal bool IsChatOrigin(Uri uri)
        {
            return uri != null && uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
                uri.UserInfo.Length == 0 && string.Equals(uri.IdnHost, Home.IdnHost, StringComparison.OrdinalIgnoreCase);
        }

        // Keep conversation paths only. Login redirects and URL parameters can contain single-use state.
        private bool IsConversation(Uri uri)
        {
            if (!IsChatOrigin(uri)) return false;
            string path = uri.AbsolutePath;
            return path == Home.AbsolutePath || path.StartsWith("/a/chat/", StringComparison.Ordinal) ||
                path.StartsWith("/chat/", StringComparison.Ordinal) || path.StartsWith("/c/", StringComparison.Ordinal);
        }

        internal string LastPage()
        {
            Uri uri;
            return Uri.TryCreate(Preferences.ReadText(LastPageFile), UriKind.Absolute, out uri) && IsConversation(uri)
                ? uri.GetLeftPart(UriPartial.Path) : Home.AbsoluteUri;
        }

        internal void RememberPage(Uri uri)
        {
            if (IsConversation(uri)) Preferences.Write(LastPageFile, uri.GetLeftPart(UriPartial.Path));
        }

        // Slate renders its placeholder and zero-width caret sentinel inside the editable element.
        // An embedded attachment still counts as a draft even when it has no text.
        internal string HasDraftScript
        {
            get {
                if (this == Qianwen)
                    return "e=>{const copy=e.cloneNode(true);copy.querySelectorAll('[data-slate-placeholder],[data-slate-zero-width]').forEach(n=>n.remove());return !!(copy.textContent.length||copy.querySelector('img,[contenteditable=false],[data-slate-void]'));}";
                return "e=>e.value!==undefined ? e.value.length>0 : !!(e.textContent.length||e.querySelector('img,[contenteditable=false]'))";
            }
        }

        internal string OriginCheck
        {
            get { return "location.origin===" + PromptInserter.Json(Home.GetLeftPart(UriPartial.Authority)); }
        }
    }
}
