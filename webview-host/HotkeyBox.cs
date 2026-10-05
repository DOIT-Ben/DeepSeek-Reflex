using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class HotkeyBox : TextBox {
        private int previous;
        internal int Combination {get;private set;}
        internal bool Recording {get;private set;}
        internal string Feedback {get;private set;}
        internal bool Invalid {get;private set;}
        internal event EventHandler FeedbackChanged;
        internal HotkeyBox(int value) {
            ReadOnly=true;Combination=value;Text=HotkeyBindings.Format(value);
            BackColor=Color.White;ForeColor=PanelTheme.Ink;BorderStyle=BorderStyle.None;
            TextAlign=HorizontalAlignment.Center;HideSelection=true;
        }
        private void Report(string message,bool invalid) {
            Feedback=message;Invalid=invalid;AccessibleDescription=message;
            if(FeedbackChanged!=null)FeedbackChanged(this,EventArgs.Empty);
        }
        internal void BeginCapture() {
            if(Recording)return;
            previous=Combination;Recording=true;Text="请按组合键…";ForeColor=PanelTheme.Muted;
            Report("按下组合键；Esc 取消，Tab 切换。",false);
        }
        internal void CancelCapture() {
            if(!Recording)return;
            Combination=previous;Recording=false;Text=HotkeyBindings.Format(Combination);ForeColor=PanelTheme.Ink;
            Report("已取消录入，原组合键已保留。",false);
        }
        private void Record(Keys keys) {
            var key=keys&Keys.KeyCode;
            if(key==Keys.Tab)return;
            if(key==Keys.Escape){CancelCapture();return;}
            BeginCapture();
            if(key==Keys.ControlKey||key==Keys.ShiftKey||key==Keys.Menu||key==Keys.LControlKey||key==Keys.RControlKey||key==Keys.LShiftKey||key==Keys.RShiftKey||key==Keys.LMenu||key==Keys.RMenu)return;
            if(!HotkeyBindings.ValidKey((int)keys)) {
                Report("请用 Ctrl 或 Alt 搭配字母、数字、空格或功能键。",true);return;
            }
            Combination=(int)keys;Recording=false;Text=HotkeyBindings.Format(Combination);ForeColor=PanelTheme.Ink;
            Report("已录入 "+Text+"，保存后生效。",false);
        }
        protected override void OnEnter(EventArgs e){base.OnEnter(e);BeginCapture();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)BeginCapture();Select(Text.Length,0);}
        protected override void OnLeave(EventArgs e){CancelCapture();base.OnLeave(e);}
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.Tab){base.OnKeyDown(e);return;}
            Record(e.KeyData);e.Handled=true;e.SuppressKeyPress=true;base.OnKeyDown(e);
        }
        protected override bool ProcessCmdKey(ref Message message,Keys keyData) {
            var key=keyData&Keys.KeyCode;
            if(key==Keys.Tab||(key==Keys.Escape&&!Recording))return base.ProcessCmdKey(ref message,keyData);
            Record(keyData);return true;
        }
    }
}
