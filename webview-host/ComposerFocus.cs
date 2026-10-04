using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
namespace DeepSeekFloat
{
    internal static class ComposerFocus
    {
        internal const string EditorScript=@"(()=>{
if(!document.hasFocus())return 'not-active';
const editors=Array.from(document.querySelectorAll('textarea,[contenteditable=true]')).filter(e=>!e.disabled&&!e.readOnly&&e.getClientRects().length&&getComputedStyle(e).visibility!=='hidden');
if(editors.length!==1)return 'no-editor';
editors[0].focus({preventScroll:true});return document.activeElement===editors[0]?'focused':'not-focused';})()";
        internal const string Script="(location.origin==='https://chat.deepseek.com'?"+EditorScript+":'wrong-host')";
        internal static async Task<string> TryFocusAsync(CoreWebView2 core,Func<bool> current)
        {
            if(core==null)return "loading";
            try {
                for(int i=0;i<8;i++) {
                    await Task.Delay(i==0?80:200);if(!current())return "cancelled";
                    string result=(await core.ExecuteScriptAsync(Script)).Trim('"');
                    if(result=="focused"||result=="wrong-host"||result=="not-active")return result;
                }
                return "no-editor";
            }catch(Exception) { return "unavailable"; }
        }
    }
}
