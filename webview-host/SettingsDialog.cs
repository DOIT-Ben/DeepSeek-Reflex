using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class SettingsDialog : Form {
        private readonly HotkeyBox toggle,capture;
        private readonly PanelSwitch focus=new PanelSwitch("唤出后直接在输入框打字");
        private readonly PanelSwitch dock=new PanelSwitch("拖到屏幕边缘时收纳为标签");
        private readonly PanelChoices hideMode=new PanelChoices("最小化到任务栏","收进托盘");
        private readonly PanelChoices windowMode=new PanelChoices("当前尺寸","小窗模式","阅读模式");
        private readonly Label error=new Label {AutoSize=false,ForeColor=PanelTheme.Error};
        private readonly Label hint=new Label {AutoSize=false,ForeColor=PanelTheme.Muted};
        private readonly Panel surface=new Panel {Name="settings-surface",Dock=DockStyle.Fill,BackColor=Color.White};
        private readonly Panel body=new Panel {Name="settings-body",AutoScroll=true,BackColor=Color.White,TabStop=false};
        private readonly Panel content=new Panel {BackColor=Color.White,TabStop=false};
        private readonly Panel commandsPage=new Panel {Name="settings-commands",BackColor=Color.White,TabStop=false};
        private readonly Panel footer=new Panel {Name="settings-footer",BackColor=Color.White};
        private readonly Panel commandList=new Panel {AutoScroll=true,BackColor=Color.White,TabStop=false};
        private readonly PanelButton save=new PanelButton("保存") {Primary=true};
        private readonly PanelButton cancel=new PanelButton("取消") {DialogResult=DialogResult.Cancel};
        private readonly PanelButton saveCommands=new PanelButton("保存指令") {Primary=true};
        private readonly PanelButton backToGeneral=new PanelButton("返回");
        private readonly PanelButton keepEditing=new PanelButton("继续编辑");
        private readonly PanelButton discardEditing=new PanelButton("放弃并返回") {Ghost=true,Outline=true};
        private readonly PanelButton addCommand=new PanelButton("新增指令") {Ghost=true,Outline=true};
        private readonly PanelButton saveEntry=new PanelButton("保存条目") {Ghost=true,Outline=true};
        private readonly TextBox commandName=new TextBox {BorderStyle=BorderStyle.None};
        private readonly TextBox commandBody=new TextBox {BorderStyle=BorderStyle.None,Multiline=true};
        private readonly Label nameError=new Label {AutoSize=false,ForeColor=PanelTheme.Error};
        private readonly Label instructionError=new Label {AutoSize=false,ForeColor=PanelTheme.Error};
        private readonly ChromeButton close=new ChromeButton("close","关闭设置");
        private readonly Label heading,subtitle;
        private readonly WindowSettings original;
        private static readonly string[][] Examples={
            new[]{"提炼要点","请从下面的材料提炼 3–5 个要点，用简短列表输出，不添加材料中没有的事实。"},
            new[]{"润色表达","请润色下面的文字，保持原意和语言，让表达更清楚、自然、简洁。"},
            new[]{"解释报错","请解释下面这段报错的含义和常见原因，并给出排查建议。"}
        };
        private List<PromptCommand> draft=new List<PromptCommand>();
        private string loadError;
        private bool commandsDirty,editorDirty,confirming,confirmCloses,onCommandsPage;
        private PromptCommand editing;
        private int exampleIndex;
        private float scale;
        private readonly WindowFrame frame=new WindowFrame();
        private SmoothFrame smoothFrame;
        private bool arranging;
        private const int GeneralContentHeight=426,CommandsContentHeight=344;
        internal SettingsDialog(WindowSettings settings,Func<WindowSettings,string> apply,Action<string> quickAction=null,string openPage=null) {
            original=settings.Clone();toggle=new HotkeyBox(settings.ToggleKeys);capture=new HotkeyBox(settings.CaptureKeys);
            toggle.AccessibleName="唤出 / 收起快捷键";capture.AccessibleName="选中文字取词快捷键";
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek-Reflex 设置";Font=PanelTheme.CompactFont();BackColor=Color.White;ForeColor=PanelTheme.Ink;DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(S(380),S(536));Padding=Padding.Empty;
            Controls.Add(surface);
            heading=LabelAt(surface,"小窗设置",20,16,292,28,PanelTheme.HeadingSize,true);
            subtitle=LabelAt(surface,"顺手唤起，随时问答"+VersionSuffix(),20,48,340,18,PanelTheme.CaptionSize,false);subtitle.ForeColor=PanelTheme.Muted;
            surface.Controls.Add(close);close.Click+=delegate{AttemptClose();};
            heading.MouseDown+=Drag;subtitle.MouseDown+=Drag;surface.MouseDown+=Drag;
            surface.Controls.Add(body);body.Controls.Add(content);body.Controls.Add(commandsPage);surface.Controls.Add(footer);
            content.Size=new Size(S(340),S(GeneralContentHeight));
            LabelAt(content,"快捷键",0,0,340,18,PanelTheme.BodySize,true);
            var card=new PanelCard();Place(content,card,0,24,340,80);card.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            KeyRow(card,"唤出 / 收起",toggle,4);KeyRow(card,"选中文字取词",capture,42);
            hint.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);hint.Text="点击按键框开始录入，Esc 取消，保存后生效。";Place(content,hint,0,112,340,28);hint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            toggle.FeedbackChanged+=RecordingFeedback;capture.FeedbackChanged+=RecordingFeedback;
            focus.Checked=settings.FocusOnOpen;var focusCard=new PanelCard();Place(content,focusCard,0,148,340,36);focusCard.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Place(focusCard,focus,12,3,316,30);focus.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            dock.Checked=settings.EdgeDockEnabled;var dockCard=new PanelCard();Place(content,dockCard,0,192,340,36);dockCard.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Place(dockCard,dock,12,3,316,30);dock.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            tips.SetToolTip(dock,"开启后，把窗口拖到屏幕左右边缘释放即收纳为标签；点击标签或按唤起键恢复。");
            LabelAt(content,"快捷键收起方式",0,244,340,18,PanelTheme.BodySize,true);
            hideMode.SelectedIndex=settings.HideToTrayOnToggle?1:0;Place(content,hideMode,0,268,340,32);hideMode.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            LabelAt(content,"窗口尺寸",0,316,340,18,PanelTheme.BodySize,true);
            windowMode.SelectedIndex=settings.Mode=="reading"?2:settings.Mode=="compact"?1:0;Place(content,windowMode,0,340,340,32);windowMode.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var actions=new Panel {Margin=Padding.Empty,Padding=Padding.Empty,BackColor=Color.White};
            Place(content,actions,0,388,340,PanelTheme.CompactControlHeight);actions.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var refresh=new PanelButton("刷新页面") {Ghost=true,Outline=true};var import=new PanelButton("粘贴导入") {Ghost=true,Outline=true};var help=new PanelButton("使用帮助") {Ghost=true,Outline=true};
            tips.SetToolTip(import,"导入剪贴板中的文字");
            var actionButtons=new[]{refresh,import,help};
            foreach(var button in actionButtons){button.Margin=Padding.Empty;button.Enabled=quickAction!=null;actions.Controls.Add(button);}
            // In-place navigation, unlike the three actions above that close the
            // window; always available so the commands page is discoverable.
            var commands=new PanelButton("快捷指令") {Ghost=true,Outline=true};commands.Margin=Padding.Empty;commands.AccessibleName="快捷指令管理";
            tips.SetToolTip(commands,"自定义取词栏「更多指令」中的条目");
            commands.Click+=delegate{ShowCommandsPage(true);};
            actions.Controls.Add(commands);
            var allActions=new[]{refresh,import,help,commands};
            Action arrangeActions=delegate{for(int i=0;i<allActions.Length;i++){int left=actions.Width*i/allActions.Length,right=actions.Width*(i+1)/allActions.Length;allActions[i].SetBounds(left,0,right-left,actions.Height);}};
            actions.Resize+=delegate{arrangeActions();};arrangeActions();
            refresh.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("refresh");};
            import.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("import");};
            help.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("help");};
            BuildCommandsPage(quickAction);
            error.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);footer.Controls.Add(error);
            footer.Controls.Add(keepEditing);footer.Controls.Add(discardEditing);
            footer.Controls.Add(cancel);footer.Controls.Add(save);
            footer.Controls.Add(backToGeneral);footer.Controls.Add(saveCommands);
            save.Click+=delegate {
                if(toggle.Recording||capture.Recording){hint.Text="请先完成或取消快捷键录入。";return;}
                var next=original.Clone();next.ToggleKeys=toggle.Combination;next.CaptureKeys=capture.Combination;next.FocusOnOpen=focus.Checked;next.EdgeDockEnabled=dock.Checked;next.HideToTrayOnToggle=hideMode.SelectedIndex==1;
                next.Mode=windowMode.SelectedIndex==2?"reading":windowMode.SelectedIndex==1?"compact":"custom";
                string failure=apply(next);if(failure!=null){error.Text=failure;return;}
                DialogResult=DialogResult.OK;Close();
            };
            CancelButton=cancel;
            cancel.Click+=delegate{AttemptClose();};
            saveCommands.Click+=delegate{SaveCommands(quickAction);};
            backToGeneral.Click+=delegate{AttemptLeave(false);};
            keepEditing.Click+=delegate{confirming=false;confirmCloses=false;UpdateFooterButtons();error.Text="";};
            discardEditing.Click+=delegate{
                confirming=false;
                if(confirmCloses){DialogResult=DialogResult.Cancel;Close();return;}
                DiscardCommandEdits();ShowGeneralPage();
            };
            Shown+=delegate {
                var area=Screen.FromControl(this).WorkingArea;
                Bounds=FitBounds(Bounds,area,scale);Arrange();
            };
            body.Layout+=delegate{ArrangeContent();};surface.Resize+=delegate{Arrange();};
            Controls.Add(frame);smoothFrame=new SmoothFrame(this,scale,delegate{frame.UpdateShape(this,scale,smoothFrame.Ready);},false);
            if(openPage=="commands")ShowCommandsPage(true);else ShowGeneralPage();
            Arrange();frame.UpdateShape(this,scale);
            Resize+=delegate{if(smoothFrame!=null)smoothFrame.SyncOwner();frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);};
            ResizeBegin+=delegate{smoothFrame.BeginInteractiveResize();};ResizeEnd+=delegate{smoothFrame.EndInteractiveResize();};
        }
        // ----- commands page -----
        private readonly ToolTip tips=new ToolTip();
        private void BuildCommandsPage(Action<string> quickAction) {
            var intro=LabelAt(commandsPage,"内置指令：翻译、解释、提问（不可修改）",0,0,340,20,PanelTheme.CaptionSize,false);intro.ForeColor=PanelTheme.Muted;
            Place(commandsPage,commandList,0,24,340,128);commandList.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            commandList.Resize+=delegate{RenderCommandList();};
            var editor=new PanelCard();Place(commandsPage,editor,0,160,340,140);editor.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var nameLabel=LabelAt(editor,"名称",12,8,44,26,PanelTheme.BodySize,false);
            var nameFrame=new PanelButton("") {TabStop=false,InputFrame=true,BackColor=PanelTheme.Surface};Place(editor,nameFrame,62,6,266,28);nameFrame.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            commandName.Font=PanelTheme.CompactFont();commandName.BackColor=PanelTheme.Surface;nameFrame.Controls.Add(commandName);
            Action nameArrange=delegate{commandName.SetBounds(S(8),Math.Max(0,(nameFrame.Height-commandName.PreferredHeight)/2),Math.Max(1,nameFrame.Width-S(16)),commandName.PreferredHeight);};
            nameFrame.Resize+=delegate{nameArrange();};nameArrange();
            nameFrame.Click+=delegate{commandName.Focus();};
            Place(editor,nameError,12,36,316,18);nameError.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var bodyLabel=LabelAt(editor,"指令",12,58,44,26,PanelTheme.BodySize,false);
            var bodyFrame=new PanelButton("") {TabStop=false,InputFrame=true,BackColor=PanelTheme.Surface};Place(editor,bodyFrame,62,56,266,56);bodyFrame.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            commandBody.Font=PanelTheme.CompactFont();commandBody.BackColor=PanelTheme.Surface;bodyFrame.Controls.Add(commandBody);
            Action bodyArrange=delegate{commandBody.SetBounds(S(8),S(6),Math.Max(1,bodyFrame.Width-S(16)),Math.Max(1,bodyFrame.Height-S(12)));};
            bodyFrame.Resize+=delegate{bodyArrange();};bodyArrange();
            Place(editor,instructionError,12,116,316,18);instructionError.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            Place(commandsPage,addCommand,0,308,110,PanelTheme.CompactControlHeight);
            Place(commandsPage,saveEntry,118,308,110,PanelTheme.CompactControlHeight);
            addCommand.Click+=delegate{
                editing=null;var example=Examples[exampleIndex++%Examples.Length];
                commandName.Text=example[0];commandBody.Text=example[1];
                editorDirty=true;ClearFieldErrors();commandName.Focus();
            };
            saveEntry.Click+=delegate{SaveEntry();};
            commandName.TextChanged+=delegate{editorDirty=true;};
            commandBody.TextChanged+=delegate{editorDirty=true;};
            tips.SetToolTip(addCommand,"用示例预填编辑器，保存条目后才会进入目录");
            tips.SetToolTip(saveEntry,"校验并写入下方草稿，仍需「保存指令」落盘");
        }
        private void RenderCommandList() {
            if(commandList==null)return;
            commandList.SuspendLayout();
            foreach(Control row in commandList.Controls.Cast<Control>().ToArray())row.Dispose();
            commandList.Controls.Clear();
            int width=Math.Max(1,commandList.ClientSize.Width),y=0;
            foreach(var builtin in PromptCatalog.Builtins) {
                var label=RowLabel(builtin.Name+"（内置）",width);commandList.Controls.Add(label);label.SetBounds(S(8),y,Math.Max(1,width-S(16)),S(24));y+=S(26);
            }
            y+=S(4);
            foreach(var item in draft) {
                var captured=item;
                var label=RowLabel(captured.Name,width);commandList.Controls.Add(label);label.SetBounds(S(8),y+2,width-S(120),S(24));
                var edit=new PanelButton("编辑") {Ghost=true,Outline=true};edit.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);
                edit.Click+=delegate{editing=captured;commandName.Text=captured.Name;commandBody.Text=captured.Instruction;editorDirty=false;ClearFieldErrors();commandName.Focus();};
                commandList.Controls.Add(edit);edit.SetBounds(width-S(108),y,S(48),S(26));
                var remove=new PanelButton("删除") {Ghost=true,Outline=true};remove.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);
                remove.Click+=delegate{draft.Remove(captured);commandsDirty=true;if(editing==captured){editing=null;ClearEditor();}RenderCommandList();};
                commandList.Controls.Add(remove);remove.SetBounds(width-S(54),y,S(48),S(26));
                y+=S(30);
            }
            if(draft.Count==0) {
                var empty=RowLabel("还没有自定义指令",width);empty.ForeColor=PanelTheme.Muted;commandList.Controls.Add(empty);empty.SetBounds(S(8),y,Math.Max(1,width-S(16)),S(24));y+=S(26);
            }
            commandList.ResumeLayout();
        }
        private Label RowLabel(string text,int width) {
            return new Label{Text=text,Font=PanelTheme.CompactFont(PanelTheme.BodySize),ForeColor=PanelTheme.Ink,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
        }
        private void SaveEntry() {
            string name=PromptCatalog.ValidateName(commandName.Text,draft.Cast<PromptCommand>(),editing);
            string body=PromptCatalog.ValidateInstruction(commandBody.Text);
            nameError.Text=name??"";instructionError.Text=body??"";
            if(name!=null){commandName.Focus();return;}
            if(body!=null){commandBody.Focus();return;}
            if(editing==null&&draft.Count>=PromptCatalog.MaxCommands){error.Text="自定义指令最多 20 条，请先删除再新增。";return;}
            if(editing==null) {
                var created=new PromptCommand{Id=PromptCatalog.NewId(),Name=commandName.Text.Trim(),Instruction=commandBody.Text.Trim(),BuiltinIndex=-1};
                draft.Add(created);editing=created;
            } else { editing.Name=commandName.Text.Trim();editing.Instruction=commandBody.Text.Trim(); }
            commandsDirty=true;editorDirty=false;error.Text="";
            RenderCommandList();
        }
        private void SaveCommands(Action<string> quickAction) {
            string failure=PromptCatalog.Save(draft);
            if(failure!=null){error.Text=failure;return;}
            commandsDirty=false;editorDirty=false;error.Text="";
            if(quickAction!=null)quickAction("commands");
            ShowGeneralPage();
        }
        private void DiscardCommandEdits() {
            LoadDraft();editing=null;ClearEditor();
        }
        private void LoadDraft() {
            var catalog=PromptCatalog.Load();
            draft=catalog.Commands.Where(c=>c.Custom).ToList();
            loadError=catalog.Error;
        }
        private void ClearEditor(){commandName.Text="";commandBody.Text="";editorDirty=false;ClearFieldErrors();}
        private void ClearFieldErrors(){nameError.Text="";instructionError.Text="";}
        private bool UnsavedCommands {get{return commandsDirty||editorDirty;}}
        // ----- page switching -----
        private void ShowGeneralPage() {
            onCommandsPage=false;confirming=false;
            heading.Text="小窗设置";subtitle.Text="顺手唤起，随时问答"+VersionSuffix();
            commandsPage.Visible=false;content.Visible=true;
            error.Text="";
            UpdateFooterButtons();
            content.Size=new Size(S(340),S(GeneralContentHeight));
            body.AutoScrollMinSize=new Size(0,S(GeneralContentHeight));
            Arrange();
        }
        private void ShowCommandsPage(bool fresh) {
            if(fresh){LoadDraft();editing=null;ClearEditor();}
            onCommandsPage=true;confirming=false;
            heading.Text="快捷指令";subtitle.Text="自定义取词栏「更多指令」中的条目";
            content.Visible=false;commandsPage.Visible=true;
            error.Text=loadError??"";
            RenderCommandList();
            UpdateFooterButtons();
            commandsPage.Size=new Size(S(340),S(CommandsContentHeight));
            body.AutoScrollMinSize=new Size(0,S(CommandsContentHeight));
            Arrange();
        }
        private void AttemptClose(){AttemptLeave(true);}
        private void AttemptLeave(bool closes) {
            if(onCommandsPage&&UnsavedCommands) { confirmCloses=closes;confirming=true;error.Text="有未保存的指令修改。";error.ForeColor=PanelTheme.Error;UpdateFooterButtons();return; }
            if(onCommandsPage) { ShowGeneralPage();return; }
            DialogResult=DialogResult.Cancel;Close();
        }
        private void UpdateFooterButtons() {
            bool commands=onCommandsPage;
            save.Visible=!commands;cancel.Visible=!commands;
            saveCommands.Visible=commands&&!confirming;backToGeneral.Visible=commands&&!confirming;
            keepEditing.Visible=discardEditing.Visible=commands&&confirming;
            if(!commands)error.ForeColor=PanelTheme.Error;
            Arrange();
        }
        // ----- shared layout -----
        internal static Rectangle FitBounds(Rectangle bounds,Rectangle area,float dpiScale) {
            int margin=Math.Min((int)Math.Round(12*dpiScale),Math.Min(area.Width,area.Height)/4);
            int width=Math.Min(bounds.Width,area.Width-margin*2),height=Math.Min(bounds.Height,area.Height-margin*2);
            return new Rectangle(Math.Max(area.Left+margin,Math.Min(bounds.Left,area.Right-margin-width)),Math.Max(area.Top+margin,Math.Min(bounds.Top,area.Bottom-margin-height)),width,height);
        }
        // Test builds compile without the generated Version.g.cs, so 0.0.0 stays unsuffixed.
        internal static string VersionSuffix() {
            var version=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version!=null&&version.Major>0 ? " · v"+version.ToString(3) : "";
        }
        private void RecordingFeedback(object sender,EventArgs e) {
            var box=(HotkeyBox)sender;string validation=HotkeyBindings.Validate(toggle.Combination,capture.Combination);
            hint.Text=validation??box.Feedback;hint.ForeColor=validation!=null||box.Invalid?PanelTheme.Error:PanelTheme.Muted;
            error.Text="";save.Enabled=validation==null;
        }
        private void Arrange() {
            if(arranging)return;arranging=true;
            try {
                int width=Math.Max(1,surface.ClientSize.Width-S(40));
                heading.Width=Math.Max(1,surface.Width-S(76));subtitle.Width=width;
                close.SetBounds(surface.Width-S(52),S(16),S(32),S(28));
                body.SetBounds(S(20),S(80),width,Math.Max(1,surface.Height-S(160)));
                footer.SetBounds(S(20),surface.Height-S(72),width,S(64));
                error.SetBounds(0,0,width,S(28));
                Button primary,secondary;
                int primaryWidth,secondaryWidth;
                if(!onCommandsPage){primary=save;secondary=cancel;primaryWidth=secondaryWidth=78;}
                else if(confirming){primary=keepEditing;secondary=discardEditing;primaryWidth=80;secondaryWidth=96;}
                else{primary=saveCommands;secondary=backToGeneral;primaryWidth=86;secondaryWidth=70;}
                int right=Math.Max(1,footer.ClientSize.Width);
                primary.SetBounds(right-S(primaryWidth),S(34),S(primaryWidth),S(PanelTheme.CompactControlHeight));
                secondary.SetBounds(right-S(primaryWidth)-S(6)-S(secondaryWidth),S(34),S(secondaryWidth),S(PanelTheme.CompactControlHeight));
            }finally{arranging=false;}
            ArrangeContent();
            if(onCommandsPage)ArrangeCommands();
        }
        private void ArrangeCommands() {
            commandsPage.SetBounds(0,0,Math.Max(1,body.ClientSize.Width),S(CommandsContentHeight));
            foreach(Control row in commandsPage.Controls)row.Width=Math.Max(1,commandsPage.ClientSize.Width-row.Left);
            commandList.Width=Math.Max(1,commandsPage.ClientSize.Width);
        }
        private void ArrangeContent() {
            if(arranging)return;arranging=true;
            try {
                body.AutoScrollMinSize=new Size(0,S(onCommandsPage?CommandsContentHeight:GeneralContentHeight));
                content.SetBounds(0,body.AutoScrollPosition.Y,Math.Max(1,body.ClientSize.Width),S(GeneralContentHeight));
                // Native anchor rounding can leave the last column one pixel outside
                // a scrollbar-reduced viewport. Keep each full-width row inside it.
                foreach(Control row in content.Controls)row.Width=Math.Max(1,content.ClientSize.Width-row.Left);
            }finally{arranging=false;}
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        internal void ApplyDpi(float value,Rectangle bounds) {
            if(value<=0)return;
            float ratio=value/scale;scale=value;
            surface.Scale(new SizeF(ratio,ratio));
            Bounds=FitBounds(bounds,Screen.FromRectangle(bounds).WorkingArea,scale);
            if(smoothFrame!=null)smoothFrame.UpdateScale(value);
            Arrange();frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x2e0){ApplyDpi(WindowDpi.MessageScale(m.WParam),WindowDpi.Suggested(m.LParam));m.Result=IntPtr.Zero;return;}
            base.WndProc(ref m);
        }
        private void Place(Control parent,Control child,int x,int y,int w,int h){child.SetBounds(S(x),S(y),S(w),S(h));parent.Controls.Add(child);}
        private Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,bool bold) {
            var label=new Label {Text=text,Font=PanelTheme.CompactFont(size,bold),ForeColor=PanelTheme.Ink,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft};Place(parent,label,x,y,w,h);if(parent==content||parent==commandsPage)label.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;return label;
        }
        private void KeyRow(PanelCard card,string label,HotkeyBox box,int y) {
            var text=LabelAt(card,label,12,y,116,PanelTheme.CompactInputHeight,PanelTheme.BodySize,false);text.BackColor=PanelTheme.Surface;
            var keyFrame=new PanelButton("") {TabStop=false,InputFrame=true,BackColor=PanelTheme.Surface};card.Controls.Add(keyFrame);keyFrame.Cursor=Cursors.Hand;
            box.Font=PanelTheme.CompactFont();keyFrame.Controls.Add(box);keyFrame.Click+=delegate{box.Focus();box.BeginCapture();};
            Action arrange=delegate {
                int labelWidth=Math.Min(S(116),(int)(card.Width*0.35));text.Width=labelWidth;
                keyFrame.SetBounds(labelWidth+S(20),S(y),Math.Max(1,card.Width-labelWidth-S(32)),S(PanelTheme.CompactInputHeight));
                box.SetBounds(S(8),Math.Max(0,(keyFrame.Height-box.PreferredHeight)/2),Math.Max(1,keyFrame.Width-S(16)),box.PreferredHeight);
            };
            card.Resize+=delegate{arrange();};arrange();
            box.Enter+=delegate{keyFrame.InputFocused=true;keyFrame.Invalidate();};box.Leave+=delegate{keyFrame.InputFocused=false;keyFrame.Invalidate();};
        }
        private void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);WindowFrame.ConfigureNativeBorder(Handle);frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);}
        protected override void Dispose(bool disposing){if(disposing){tips.Dispose();if(smoothFrame!=null)smoothFrame.Dispose();}base.Dispose(disposing);}
    }
}
