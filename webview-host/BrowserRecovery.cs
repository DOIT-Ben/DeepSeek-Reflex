using Microsoft.Web.WebView2.Core;
namespace DeepSeekFloat {
    internal enum RecoveryAction { Ignore,Reload,Recreate }
    internal static class BrowserRecovery {
        internal static RecoveryAction For(CoreWebView2ProcessFailedKind kind) {
            if(kind==CoreWebView2ProcessFailedKind.BrowserProcessExited)return RecoveryAction.Recreate;
            if(kind==CoreWebView2ProcessFailedKind.RenderProcessExited||kind==CoreWebView2ProcessFailedKind.RenderProcessUnresponsive||kind==CoreWebView2ProcessFailedKind.FrameRenderProcessExited)return RecoveryAction.Reload;
            return RecoveryAction.Ignore;
        }
    }
}
