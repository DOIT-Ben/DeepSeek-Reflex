using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace DeepSeekFloat
{
    // WebView2.OnGotFocus transfers native focus into Chromium. Doing that
    // inside the owner's activation sequence can bind TSF to its fallback
    // document, leaving an IME composition window at the screen origin.
    internal sealed class DeferredFocusWebView : WebView2
    {
        private int focusRequest;

        internal void SetOwnerVisibility(bool visible)
        {
            // A hidden parent can already make Control.Visible false without notifying
            // WebView2. Its WinForms handler propagates this value to the native controller.
            bool previous = Visible;
            Visible = visible;
            if (Visible == previous) base.OnVisibleChanged(EventArgs.Empty);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            int request=++focusRequest;
            BeginInvoke(new Action(delegate {
                var owner=FindForm();
                if(IsDisposed||Disposing||!IsHandleCreated||request!=focusRequest||!Focused||!Visible||
                    owner==null||owner.IsDisposed||!owner.Visible||owner.WindowState==FormWindowState.Minimized||
                    Native.GetForegroundWindow()!=owner.Handle)return;
                // A posted callback runs after WM_SETFOCUS/WM_ACTIVATE unwind.
                // No DOM blur, text replacement or selection changes are needed.
                base.OnGotFocus(e);
            }));
        }

        protected override void OnLostFocus(EventArgs e)
        {
            focusRequest++;
            base.OnLostFocus(e);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            focusRequest++;
            base.OnHandleDestroyed(e);
        }
    }
}
