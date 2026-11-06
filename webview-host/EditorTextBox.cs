using System;
using System.Windows.Forms;
namespace DeepSeekFloat {
    // Native first-focus select-all is unsafe for prefilled forms. Preserve
    // the user's caret/selection; mouse placement and Ctrl+A remain native.
    internal class EditorTextBox : TextBox {
        protected override void OnGotFocus(EventArgs e) {
            int start=SelectionStart,length=SelectionLength;
            base.OnGotFocus(e);
            if(Focused&&!IsDisposed)Select(start,length);
        }
    }
}
