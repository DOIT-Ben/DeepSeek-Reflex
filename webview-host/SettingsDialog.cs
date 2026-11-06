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
        private readonly PanelChoices hideMode=new PanelChoices("任务栏","托盘");
        private readonly PanelChoices windowMode=new PanelChoices("自定义尺寸","小窗模式","阅读模式");
        private readonly PanelChoices themeChoice=new PanelChoices("跟随系统","浅色","深色");
        private readonly Label error=new Label {AutoSize=false,BackColor=Color.Transparent,ForeColor=PanelTheme.Error};
        private readonly Label hint=new Label {AutoSize=false,BackColor=Color.Transparent,ForeColor=PanelTheme.Muted};
        private readonly Panel surface=new Panel {Name="settings-surface",Dock=DockStyle.Fill,BackColor=PanelTheme.Back};
        private readonly Panel body=new Panel {Name="settings-body",AutoScroll=true,BackColor=PanelTheme.Back,TabStop=false};
        private readonly Panel content=new Panel {BackColor=PanelTheme.Back,TabStop=false};
        private readonly Panel commandsPage=new Panel {Name="settings-commands",BackColor=PanelTheme.Back,TabStop=false};
        private readonly Panel modesPage=new Panel {Name="settings-modes",BackColor=PanelTheme.Back,TabStop=false};
        private readonly Panel footer=new Panel {Name="settings-footer",BackColor=PanelTheme.Back};
        private readonly Panel commandList=new Panel {AutoScroll=true,BackColor=PanelTheme.Back,TabStop=false};
        private readonly Panel modeList=new Panel {AutoScroll=true,BackColor=PanelTheme.Back,TabStop=false};
        private readonly PanelButton save=new PanelButton("保存") {Primary=true};
        private readonly PanelButton cancel=new PanelButton("取消") {DialogResult=DialogResult.Cancel};
        private readonly PanelButton saveCommands=new PanelButton("保存指令") {Primary=true};
        private readonly PanelButton saveModes=new PanelButton("保存模式") {Primary=true};
        private readonly PanelButton backToGeneral=new PanelButton("返回");
        private readonly PanelButton keepEditing=new PanelButton("继续编辑");
        private readonly PanelButton discardEditing=new PanelButton("放弃并返回") {Ghost=true};
        private readonly PanelButton addCommand=new PanelButton("填入示例") {Ghost=true};
        private readonly PanelButton restoreDefault=new PanelButton("恢复默认") {Ghost=true};
        private readonly PanelButton saveEntry=new PanelButton("保存到列表");
        private readonly PanelButton addMode=new PanelButton("填入示例") {Ghost=true};
        private readonly PanelButton saveModeEntry=new PanelButton("保存到列表");
        private readonly PanelButton resetMode=new PanelButton("恢复默认") {Ghost=true};
        private readonly TextBox commandName=new EditorTextBox {BorderStyle=BorderStyle.None};
        private readonly TextBox commandBody=new EditorTextBox {BorderStyle=BorderStyle.None,Multiline=true};
        private readonly TextBox modeName=new EditorTextBox {BorderStyle=BorderStyle.None};
        private readonly TextBox modeBody=new EditorTextBox {BorderStyle=BorderStyle.None,Multiline=true};
        private readonly Label nameError=new Label {AutoSize=false,BackColor=Color.Transparent,ForeColor=PanelTheme.Error};
        private readonly Label instructionError=new Label {AutoSize=false,BackColor=Color.Transparent,ForeColor=PanelTheme.Error};
        private readonly Label modeNameError=new Label {AutoSize=false,BackColor=Color.Transparent,ForeColor=PanelTheme.Error};
        private readonly Label modeInstructionError=new Label {AutoSize=false,BackColor=Color.Transparent,ForeColor=PanelTheme.Error};
        private readonly ChromeButton close=new ChromeButton("close","关闭设置");
        private readonly Label heading;
        private readonly WindowSettings original;
        private static readonly string[][] Examples={
            new[]{"提炼要点","请从下面的材料提炼 3–5 个要点，用简短列表输出，不添加材料中没有的事实。"},
            new[]{"润色表达","请润色下面的文字，保持原意和语言，让表达更清楚、自然、简洁。"},
            new[]{"解释报错","请解释下面这段报错的含义和常见原因，并给出排查建议。"}
        };
        private List<PromptCommand> draft=new List<PromptCommand>();
        private ModeCatalog.Catalog modeDraft;
        private string loadError,modeLoadError;
        private bool commandsDirty,editorDirty,confirming,confirmCloses,onCommandsPage,onModesPage;
        private bool modesDirty,modeEditorDirty;
        // Null = nothing loaded in the editor; a builtin id edits the prompt
        // override only; a custom id edits name+prompt.
        private string editingModeId;
        private bool editingModeCustom;
        private PromptCommand editing;
        private int exampleIndex;
        private float scale;
        private readonly WindowFrame frame=new WindowFrame();
        private SmoothFrame smoothFrame;
        private bool arranging;
        private bool closingApproved;
        private bool initialPlacementDone;
        private Control rememberedFocus;
        private Point rememberedScroll;
        private int rememberedSelectionStart,rememberedSelectionLength;
        private int sessionRestoreVersion;
        internal event Action<bool> ShortcutEntryChanged;
        internal bool ShortcutEntryFocused {get{return toggle.ContainsFocus||capture.ContainsFocus;}}
        internal void ShowSessionError(string message){error.Text=message;}
        internal void OwnerResized() {
            int previous=original.Mode=="reading"?2:original.Mode=="compact"?1:0;
            original.Mode="custom";if(windowMode.SelectedIndex==previous)windowMode.SelectedIndex=0;
        }
        // Action rows keep >=24 DIP of tail padding inside the scrollable
        // page, so a height-clamped window cuts into padding before it cuts
        // into controls.
        private const int GeneralContentHeight=520,CommandsContentHeight=436,ModesContentHeight=516;
        // Header 52 + content + viewport slack 12 + footer band 80: the dialog
        // is exactly as tall as the active page, so nothing scrolls by default.
        private int DialogHeight(){return 144+(onCommandsPage?CommandsContentHeight:onModesPage?ModesContentHeight:GeneralContentHeight);}
        private void ApplyDialogHeight(){
            // During construction no owner has been assigned yet. Asking for
            // Screen.FromControl would create the HWND before ShowDialog can
            // inherit its owner's native topmost band.
            var area=(IsHandleCreated?Screen.FromControl(this):Screen.FromRectangle(Bounds)).WorkingArea;
            var requested=new Rectangle(Location,new Size(Width,S(DialogHeight())));
            var fitted=FitBounds(requested,area,scale);
            if(Bounds!=fitted){Bounds=fitted;Arrange();}
        }
        internal SettingsDialog(WindowSettings settings,Func<WindowSettings,string> apply,Action<string> quickAction=null,string openPage=null) {
            original=settings.Clone();toggle=new HotkeyBox(settings.ToggleKeys);capture=new HotkeyBox(settings.CaptureKeys);
            toggle.AccessibleName="唤出 / 收起快捷键";capture.AccessibleName="选中文字取词快捷键";
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96f;
            Text="DeepSeek-Reflex 设置";Font=PanelTheme.CompactFont();BackColor=PanelTheme.Back;ForeColor=PanelTheme.Ink;DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(S(460),S(DialogHeight()));Padding=Padding.Empty;
            Controls.Add(surface);
            heading=LabelAt(surface,"小窗设置",20,14,292,28,PanelTheme.HeadingSize,true);
            surface.Controls.Add(close);close.Click+=delegate{AttemptClose();};
            heading.MouseDown+=Drag;surface.MouseDown+=Drag;
            surface.Controls.Add(body);body.Controls.Add(content);body.Controls.Add(commandsPage);body.Controls.Add(modesPage);surface.Controls.Add(footer);
            content.Size=new Size(S(420),S(GeneralContentHeight));
            var keys=GeneralSection("快捷键",0,132);
            KeyRow(keys,"唤出 / 收起",toggle,28);KeyRow(keys,"选中文字取词",capture,70);
            hint.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);hint.Text="点击录入；Tab 完成，Esc 取消。";Place(keys,hint,0,110,330,20);
            restoreDefault.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);restoreDefault.AccessibleName="恢复默认快捷键";tips.SetToolTip(restoreDefault,"回到 Ctrl+Space 与 Ctrl+Shift+D");Place(keys,restoreDefault,346,108,74,24);restoreDefault.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            restoreDefault.Click+=delegate{
                toggle.ResetTo((int)(Keys.Control|Keys.Space));
                capture.ResetTo((int)(Keys.Control|Keys.D|Keys.Shift));
                hint.ForeColor=PanelTheme.Muted;hint.Text="已恢复默认快捷键，保存后生效。";
            };
            restoreDefault.BringToFront();
            keys.Resize+=delegate{hint.Width=Math.Max(1,keys.ClientSize.Width-S(90));};
            toggle.FeedbackChanged+=RecordingFeedback;capture.FeedbackChanged+=RecordingFeedback;
            foreach(var input in new[]{toggle,capture}) {
                input.Enter+=delegate{if(ShortcutEntryChanged!=null)ShortcutEntryChanged(true);};
                input.Leave+=delegate{if(IsHandleCreated&&!IsDisposed)BeginInvoke(new Action(delegate{if(!IsDisposed&&ShortcutEntryChanged!=null)ShortcutEntryChanged(ShortcutEntryFocused);}));};
                input.ExitCaptureRequested+=delegate{close.Focus();};
            }
            var behavior=GeneralSection("窗口行为",148,138);
            focus.Checked=settings.FocusOnOpen;Place(behavior,focus,0,26,420,32);focus.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            dock.Checked=settings.EdgeDockEnabled;Place(behavior,dock,0,64,420,32);dock.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            tips.SetToolTip(dock,"拖到屏幕左右边缘释放即收纳为标签，点击标签恢复。");
            hideMode.SelectedIndex=settings.HideToTrayOnToggle?1:0;GeneralChoice(behavior,"收起方式",hideMode,104);
            var appearance=GeneralSection("外观",302,98);
            windowMode.SelectedIndex=settings.Mode=="reading"?2:settings.Mode=="compact"?1:0;GeneralChoice(appearance,"窗口尺寸",windowMode,26);
            themeChoice.SelectedIndex=settings.Theme=="dark"?2:settings.Theme=="light"?1:0;GeneralChoice(appearance,"主题",themeChoice,66);
            themeChoice.AccessibleName="界面主题";
            var actions=new Panel {Margin=Padding.Empty,Padding=Padding.Empty,BackColor=PanelTheme.Back};
            Place(content,actions,0,486,420,PanelTheme.ControlHeight);actions.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var refresh=new PanelButton("刷新页面") {Ghost=true};var import=new PanelButton("粘贴导入") {Ghost=true};var help=new PanelButton("使用帮助") {Ghost=true};
            tips.SetToolTip(import,"导入剪贴板中的文字");
            var actionButtons=new[]{refresh,import,help};
            foreach(var button in actionButtons){button.Margin=Padding.Empty;button.Enabled=quickAction!=null;actions.Controls.Add(button);}
            // In-place navigation, unlike the three actions above that close the
            // window; always available so the commands page is discoverable.
            var commands=new PanelButton("快捷指令");commands.Margin=Padding.Empty;commands.AccessibleName="快捷指令管理";
            tips.SetToolTip(commands,"自定义取词栏「更多指令」中的条目");
            commands.Click+=delegate{ShowCommandsPage(true);};
            var management=GeneralSection("内容与模式",416,54);
            Place(management,commands,0,24,206,30);
            var modes=new PanelButton("聊天模式");modes.Margin=Padding.Empty;modes.AccessibleName="聊天模式提示词";
            tips.SetToolTip(modes,"自定义各模式的提示词，或新增自己的模式");
            modes.Click+=delegate{ShowModesPage(true);};
            Place(management,modes,214,24,206,30);
            management.Resize+=delegate{int half=Math.Max(1,(management.ClientSize.Width-S(8))/2);commands.SetBounds(0,S(24),half,S(30));modes.SetBounds(half+S(8),S(24),half,S(30));};
            var allActions=new[]{refresh,import,help};
            Action arrangeActions=delegate{for(int i=0;i<allActions.Length;i++){int left=actions.Width*i/allActions.Length,right=actions.Width*(i+1)/allActions.Length;allActions[i].SetBounds(left,0,right-left,actions.Height);}};
            actions.Resize+=delegate{arrangeActions();};arrangeActions();
            // Same close-then-act contract as the other quick actions; the host
            // restores a docked window before showing the feedback dialog.
            var feedback=new PanelButton("反馈") {Ghost=true};feedback.Margin=Padding.Empty;feedback.AccessibleName="发送反馈";
            tips.SetToolTip(feedback,"把建议直接发给开发者");
            feedback.Click+=delegate{DialogResult=DialogResult.Cancel;Close();if(quickAction!=null)quickAction("feedback");};
            actions.Controls.Add(feedback);allActions=new[]{refresh,import,help,feedback};arrangeActions();
            refresh.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("refresh");};
            import.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("import");};
            help.Click+=delegate{DialogResult=DialogResult.Cancel;Close();quickAction("help");};
            BuildCommandsPage(quickAction);
            BuildModesPage(quickAction);
            error.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);footer.Controls.Add(error);
            footer.Controls.Add(keepEditing);footer.Controls.Add(discardEditing);
            footer.Controls.Add(cancel);footer.Controls.Add(save);
            footer.Controls.Add(backToGeneral);footer.Controls.Add(saveCommands);
            footer.Controls.Add(saveModes);
            save.Click+=delegate {
                if(toggle.Recording||capture.Recording){hint.Text="请先完成或取消快捷键录入。";return;}
                var next=original.Clone();next.ToggleKeys=toggle.Combination;next.CaptureKeys=capture.Combination;next.FocusOnOpen=focus.Checked;next.EdgeDockEnabled=dock.Checked;next.HideToTrayOnToggle=hideMode.SelectedIndex==1;
                next.Mode=windowMode.SelectedIndex==2?"reading":windowMode.SelectedIndex==1?"compact":"custom";
                next.Theme=themeChoice.SelectedIndex==2?"dark":themeChoice.SelectedIndex==1?"light":"auto";
                string failure=apply(next);if(failure!=null){error.Text=failure;return;}
                DialogResult=DialogResult.OK;Close();
            };
            CancelButton=cancel;
            cancel.Click+=delegate{AttemptClose();};
            saveCommands.Click+=delegate{SaveCommands(quickAction);};
            saveModes.Click+=delegate{SaveModes(quickAction);};
            backToGeneral.Click+=delegate{AttemptLeave(false);};
            keepEditing.Click+=delegate{confirming=false;confirmCloses=false;UpdateFooterButtons();error.Text="";};
            discardEditing.Click+=delegate{
                confirming=false;
                if(confirmCloses){closingApproved=true;DialogResult=DialogResult.Cancel;Close();return;}
                if(onModesPage)DiscardModeEdits();else DiscardCommandEdits();
                ShowGeneralPage();
            };
            Shown+=delegate {
                if(initialPlacementDone)return;
                initialPlacementDone=true;
                PositionAtOwner();Arrange();
                close.Focus();
                // Moving to the owner's monitor can deliver WM_DPICHANGED.
                // Center again with the settled physical size, only on first open.
                int version=sessionRestoreVersion;
                if(Owner!=null)BeginInvoke(new Action(delegate{if(!IsDisposed&&Visible&&version==sessionRestoreVersion)PositionAtOwner();}));
            };
            body.Layout+=delegate{ArrangeContent();};surface.Resize+=delegate{Arrange();};
            Controls.Add(frame);smoothFrame=new SmoothFrame(this,scale,delegate{frame.UpdateShape(this,scale,smoothFrame.Ready);},false);
            if(openPage=="commands")ShowCommandsPage(true);
            else if(openPage=="modes")ShowModesPage(true);
            else ShowGeneralPage();
            Arrange();frame.UpdateShape(this,scale);
            Resize+=delegate{if(smoothFrame!=null)smoothFrame.SyncOwner();frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);};
            ResizeBegin+=delegate{smoothFrame.BeginInteractiveResize();};ResizeEnd+=delegate{smoothFrame.EndInteractiveResize();};
        }
        // ----- commands page -----
        private readonly ToolTip tips=new ToolTip();
        private void BuildCommandsPage(Action<string> quickAction) {
            var intro=LabelAt(commandsPage,"选中网页文字后，这些指令会出现在取词栏的指令菜单里。",0,0,420,18,PanelTheme.CaptionSize,false);intro.ForeColor=PanelTheme.Muted;
            var intro2=LabelAt(commandsPage,"内置翻译、解释、提问不可修改；在下方新增自己的指令。",0,17,420,16,PanelTheme.CaptionSize,false);intro2.ForeColor=PanelTheme.Muted;
            Place(commandsPage,commandList,0,36,420,106);commandList.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            commandList.Resize+=delegate{RenderCommandList();};
            BuildEditorCard(commandsPage,168,commandName,commandBody,nameError,instructionError,"指令","新增或编辑指令");
            BuildEditorActions(commandsPage,382,addCommand,saveEntry);
            addCommand.Click+=delegate{
                editing=null;var example=Examples[exampleIndex++%Examples.Length];
                commandName.Text=example[0];commandBody.Text=example[1];
                editorDirty=true;ClearFieldErrors();FocusEditor(commandName);
            };
            saveEntry.Click+=delegate{SaveEntry();};
            commandName.TextChanged+=delegate{editorDirty=true;};
            commandBody.TextChanged+=delegate{editorDirty=true;};
            tips.SetToolTip(addCommand,"用示例预填下面的表单");
            tips.SetToolTip(saveEntry,"校验并加入上面的列表；点右下「保存指令」才写入文件生效");
        }
        private void BuildEditorCard(Panel page,int y,TextBox name,TextBox body,Label nameError,Label bodyError,string bodyLabelText,string formTitle) {
            LabelAt(page,formTitle,0,y-24,420,20,PanelTheme.BodySize,true);
            LabelAt(page,"名称",0,y,420,18,PanelTheme.BodySize,false);
            InputWell(page,name,0,y+22,420,PanelTheme.CompactInputHeight,false);
            Place(page,nameError,0,y+58,420,16);
            LabelAt(page,bodyLabelText,0,y+78,420,18,PanelTheme.BodySize,false);
            InputWell(page,body,0,y+100,420,88,true);
            Place(page,bodyError,0,y+190,420,16);
        }
        private void BuildEditorActions(Panel page,int y,params PanelButton[] buttons) {
            var actions=new Panel {BackColor=PanelTheme.Back,TabStop=false};
            Place(page,actions,0,y,420,PanelTheme.CompactControlHeight);
            foreach(var button in buttons)actions.Controls.Add(button);
            Action arrange=delegate {
                int gap=S(8),width=Math.Min(S(110),Math.Max(1,(actions.ClientSize.Width-gap*(buttons.Length-1))/buttons.Length));
                for(int index=0;index<buttons.Length;index++)buttons[index].SetBounds(index*(width+gap),0,width,S(PanelTheme.CompactControlHeight));
            };
            actions.Resize+=delegate{arrange();};arrange();
        }
        // One input field: parent surface, click-to-focus, one accent outline
        // while focused. Read-only boxes still focus so their text stays
        // copyable.
        private InputPanel InputWell(Control parent,TextBox box,int x,int y,int w,int h,bool multiline) {
            var frame=new InputPanel {TabStop=false,BackColor=parent.BackColor};Place(parent,frame,x,y,w,h);
            box.Font=PanelTheme.CompactFont();box.BackColor=parent.BackColor;box.ForeColor=PanelTheme.Ink;frame.Controls.Add(box);
            Action arrange=multiline
                ?(Action)delegate{box.SetBounds(S(8),S(6),Math.Max(1,frame.Width-S(16)),Math.Max(1,frame.Height-S(12)));}
                :delegate{box.SetBounds(S(8),Math.Max(0,(frame.Height-box.PreferredHeight)/2),Math.Max(1,frame.Width-S(16)),box.PreferredHeight);};
            frame.Resize+=delegate{arrange();};arrange();
            frame.Click+=delegate{box.Focus();};
            box.Enter+=delegate{frame.Accent=true;};
            box.Leave+=delegate{frame.Accent=false;};
            return frame;
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
                RowAction(commandList,"编辑",width-S(108),y,delegate{editing=captured;commandName.Text=captured.Name;commandBody.Text=captured.Instruction;editorDirty=false;ClearFieldErrors();FocusEditor(commandName);});
                RowAction(commandList,"删除",width-S(54),y,delegate{draft.Remove(captured);commandsDirty=true;if(editing==captured){editing=null;ClearEditor();}RenderCommandList();});
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
        // Compact ghost action pinned to the right edge of a list row.
        private PanelButton RowAction(Panel list,string text,int right,int y,Action onClick) {
            var button=new PanelButton(text) {Ghost=true};button.Font=PanelTheme.CompactFont(PanelTheme.CaptionSize);
            button.Click+=delegate{onClick();};
            list.Controls.Add(button);button.SetBounds(right,y,S(48),S(26));return button;
        }
        private void SaveEntry() {
            string name=PromptCatalog.ValidateName(commandName.Text,PromptCatalog.Builtins.Concat(draft),editing);
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
            if(editorDirty){SaveEntry();if(editorDirty)return;}
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
        // ----- modes page -----
        private void BuildModesPage(Action<string> quickAction) {
            var intro=LabelAt(modesPage,"内置模式可改提示词，也可新增自己的模式；普通对话不加任务提示词。",0,0,420,18,PanelTheme.CaptionSize,false);intro.ForeColor=PanelTheme.Muted;
            // 196 DIP fits ordinary + all five builtins (24/28-row rhythm)
            // without scrolling, so the default view never shows the
            // unthemed system scrollbar.
            Place(modesPage,modeList,0,22,420,196);modeList.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            modeList.Resize+=delegate{RenderModeList();};
            BuildEditorCard(modesPage,248,modeName,modeBody,modeNameError,modeInstructionError,"提示词","新增或编辑模式");
            modeName.AccessibleName="模式名称";modeBody.AccessibleName="模式提示词";
            BuildEditorActions(modesPage,462,addMode,saveModeEntry,resetMode);
            addMode.Click+=delegate{
                editingModeId=null;editingModeCustom=true;
                modeName.ReadOnly=false;modeName.Text="我的模式";modeBody.Text="请把下面的内容改写成口语化的中文，保留原意和所有事实，语气自然。";
                modeEditorDirty=true;ClearModeFieldErrors();UpdateModeEditorState();FocusEditor(modeName);
            };
            saveModeEntry.Click+=delegate{SaveModeEntry();};
            resetMode.Click+=delegate{
                if(editingModeId==null||editingModeCustom||!modeDraft.Overrides.ContainsKey(editingModeId))return;
                modeDraft.Overrides.Remove(editingModeId);modesDirty=true;modeEditorDirty=false;
                modeBody.Text=ChatModes.All.First(m=>m.Id==editingModeId).Instruction;
                ClearModeFieldErrors();UpdateModeEditorState();RenderModeList();
            };
            modeName.TextChanged+=delegate{modeEditorDirty=true;};
            modeBody.TextChanged+=delegate{modeEditorDirty=true;};
            tips.SetToolTip(addMode,"用示例预填下面的表单，开始一个新模式");
            tips.SetToolTip(saveModeEntry,"校验并加入上面的列表；点右下「保存模式」才写入文件生效");
            tips.SetToolTip(resetMode,"把内置模式的提示词恢复为出厂文案");
            UpdateModeEditorState();
        }
        private void RenderModeList() {
            // A resize can arrive before the modes page was ever opened (a
            // DPI change rescales every child of the body), and the draft
            // only loads when the page activates. Skip until then instead
            // of throwing mid-layout, which would leave controls unpainted.
            if(modeList==null||modeDraft==null)return;
            modeList.SuspendLayout();
            foreach(Control row in modeList.Controls.Cast<Control>().ToArray())row.Dispose();
            modeList.Controls.Clear();
            int width=Math.Max(1,modeList.ClientSize.Width),y=0;
            var normal=RowLabel("普通对话（内置）",width);normal.ForeColor=PanelTheme.Muted;modeList.Controls.Add(normal);normal.SetBounds(S(8),y,Math.Max(1,width-S(16)),S(24));y+=S(24);
            foreach(var builtin in ChatModes.All) {
                if(builtin.Id=="normal")continue;
                string id=builtin.Id;
                bool overridden=modeDraft.Overrides.ContainsKey(id);
                var label=RowLabel(builtin.Name+(overridden?"（内置 · 已自定义）":"（内置）"),width);modeList.Controls.Add(label);
                label.SetBounds(S(8),y+2,width-S(66),S(24));
                RowAction(modeList,"编辑",width-S(54),y,delegate{EditBuiltinMode(id);});
                y+=S(28);
            }
            y+=S(4);
            foreach(var item in modeDraft.Custom) {
                var captured=item;
                var label=RowLabel(captured.Name,width);modeList.Controls.Add(label);label.SetBounds(S(8),y+2,width-S(120),S(24));
                RowAction(modeList,"编辑",width-S(108),y,delegate{EditCustomMode(captured);});
                RowAction(modeList,"删除",width-S(54),y,delegate{
                    modeDraft.Custom.Remove(captured);modesDirty=true;
                    if(editingModeId==captured.Id)ClearModeEditor();
                    RenderModeList();
                });
                y+=S(28);
            }
            if(modeDraft.Custom.Count==0) {
                var empty=RowLabel("还没有自定义模式",width);empty.ForeColor=PanelTheme.Muted;modeList.Controls.Add(empty);empty.SetBounds(S(8),y,Math.Max(1,width-S(16)),S(24));y+=S(24);
            }
            modeList.ResumeLayout();
        }
        // Loading an editor must not leave WinForms first-focus select-all active.
        // Apply after Focus; explicit mouse selection and Ctrl+A remain native.
        private static void FocusEditor(TextBox editor){editor.Focus();editor.Select(editor.TextLength,0);}
        private void EditBuiltinMode(string id) {
            editingModeId=id;editingModeCustom=false;
            var builtin=ChatModes.All.First(m=>m.Id==id);
            string instruction;
            modeName.ReadOnly=true;modeName.Text=builtin.Name;
            modeBody.Text=modeDraft.Overrides.TryGetValue(id,out instruction)?instruction:builtin.Instruction;
            modeEditorDirty=false;ClearModeFieldErrors();UpdateModeEditorState();FocusEditor(modeBody);
        }
        private void EditCustomMode(CustomChatMode item) {
            editingModeId=item.Id;editingModeCustom=true;
            modeName.ReadOnly=false;modeName.Text=item.Name;modeBody.Text=item.Instruction;
            modeEditorDirty=false;ClearModeFieldErrors();UpdateModeEditorState();FocusEditor(modeName);
        }
        private void UpdateModeEditorState() {
            // A locked builtin name keeps the shared outline and reads muted.
            modeName.ForeColor=modeName.ReadOnly?PanelTheme.Muted:PanelTheme.Ink;
            resetMode.Enabled=editingModeId!=null&&!editingModeCustom&&modeDraft!=null&&modeDraft.Overrides.ContainsKey(editingModeId);
        }
        private void SaveModeEntry() {
            if(editingModeId==null&&!editingModeCustom){if(!modeEditorDirty)return;editingModeCustom=true;}
            string instruction=ModeCatalog.ValidateInstruction(modeBody.Text);
            if(!editingModeCustom) {
                // Builtin modes keep their name; only the prompt is editable.
                modeNameError.Text="";
                modeInstructionError.Text=instruction??"";
                if(instruction!=null){modeBody.Focus();return;}
                var builtin=ChatModes.All.First(m=>m.Id==editingModeId);
                if(modeBody.Text.Trim()==builtin.Instruction.Trim())modeDraft.Overrides.Remove(editingModeId);
                else modeDraft.Overrides[editingModeId]=modeBody.Text.Trim();
                modesDirty=true;modeEditorDirty=false;error.Text="";
                RenderModeList();UpdateModeEditorState();return;
            }
            var self=editingModeId==null?null:modeDraft.Custom.FirstOrDefault(c=>c.Id==editingModeId);
            string name=ModeCatalog.ValidateName(modeName.Text,modeDraft.Custom,self);
            modeNameError.Text=name??"";modeInstructionError.Text=instruction??"";
            if(name!=null){modeName.Focus();return;}
            if(instruction!=null){modeBody.Focus();return;}
            if(self==null) {
                if(modeDraft.Custom.Count>=ModeCatalog.MaxCustomModes){error.Text="自定义模式最多 "+ModeCatalog.MaxCustomModes+" 个，请先删除再新增。";return;}
                var created=new CustomChatMode{Id=ModeCatalog.NewId(),Name=modeName.Text.Trim(),Instruction=modeBody.Text.Trim()};
                modeDraft.Custom.Add(created);editingModeId=created.Id;
            } else { self.Name=modeName.Text.Trim();self.Instruction=modeBody.Text.Trim(); }
            modesDirty=true;modeEditorDirty=false;error.Text="";
            RenderModeList();UpdateModeEditorState();
        }
        private void SaveModes(Action<string> quickAction) {
            if(modeEditorDirty){SaveModeEntry();if(modeEditorDirty)return;}
            string failure=ModeCatalog.Save(modeDraft);
            if(failure!=null){error.Text=failure;return;}
            modesDirty=false;modeEditorDirty=false;error.Text="";
            if(quickAction!=null)quickAction("modes");
            ShowGeneralPage();
        }
        private void DiscardModeEdits(){LoadModeDraft();ClearModeEditor();}
        private void LoadModeDraft() {
            modeDraft=ModeCatalog.Load();
            modeLoadError=modeDraft.Error;
        }
        private void ClearModeEditor(){editingModeId=null;editingModeCustom=false;modeName.ReadOnly=false;modeName.Text="";modeBody.Text="";modeEditorDirty=false;ClearModeFieldErrors();UpdateModeEditorState();}
        private void ClearModeFieldErrors(){modeNameError.Text="";modeInstructionError.Text="";}
        private bool UnsavedModes {get{return modesDirty||modeEditorDirty;}}
        // ----- page switching -----
        // All three pages switch through the same shape; only the freshly
        // loaded state and the load-error text differ per page.
        private void ActivatePage(Panel page,string title,int height,string pageError) {
            onCommandsPage=page==commandsPage;onModesPage=page==modesPage;confirming=false;
            heading.Text=title;
            foreach(Control candidate in new Control[]{content,commandsPage,modesPage})candidate.Visible=candidate==page;
            error.Text=pageError??"";error.ForeColor=PanelTheme.Error;
            page.Size=new Size(S(420),S(height));
            body.AutoScrollMinSize=new Size(0,S(height));
            // A layout pass that throws mid-way (now guarded) or a clipped
            // first paint can leave bands of these hand-painted controls
            // unpainted, which reads as crushed rows. Repaint the settled
            // page fully, children included.
            if(IsHandleCreated)page.Invalidate(true);
            UpdateFooterButtons();
            ApplyDialogHeight();
            Arrange();
        }
        private void ShowGeneralPage(){ActivatePage(content,"小窗设置",GeneralContentHeight,null);}
        private void ShowCommandsPage(bool fresh) {
            if(fresh){LoadDraft();editing=null;ClearEditor();}
            RenderCommandList();
            ActivatePage(commandsPage,"快捷指令",CommandsContentHeight,loadError);
        }
        private void ShowModesPage(bool fresh) {
            if(fresh){LoadModeDraft();ClearModeEditor();}
            RenderModeList();
            ActivatePage(modesPage,"聊天模式",ModesContentHeight,modeLoadError);
        }
        private void AttemptClose(){AttemptLeave(true);}
        private void AttemptLeave(bool closes) {
            if(onCommandsPage&&UnsavedCommands) { confirmCloses=closes;confirming=true;error.Text="有未保存的指令修改。";error.ForeColor=PanelTheme.Error;UpdateFooterButtons();return; }
            if(onModesPage&&UnsavedModes) { confirmCloses=closes;confirming=true;error.Text="有未保存的模式修改。";error.ForeColor=PanelTheme.Error;UpdateFooterButtons();return; }
            if(onCommandsPage||onModesPage) { ShowGeneralPage();return; }
            DialogResult=DialogResult.Cancel;Close();
        }
        private void UpdateFooterButtons() {
            bool commands=onCommandsPage,modes=onModesPage;
            save.Visible=!commands&&!modes;cancel.Visible=!commands&&!modes;
            saveCommands.Visible=commands&&!confirming;saveModes.Visible=modes&&!confirming;
            backToGeneral.Visible=(commands||modes)&&!confirming;
            keepEditing.Visible=discardEditing.Visible=(commands||modes)&&confirming;
            if(!commands&&!modes)error.ForeColor=PanelTheme.Error;
            Arrange();
        }
        // ----- shared layout -----
        private void PositionAtOwner() {
            var owner=Owner;
            // A minimized owner's native Bounds are its off-screen icon slot.
            var ownerBounds=owner==null?Bounds:owner.WindowState==FormWindowState.Minimized?owner.RestoreBounds:owner.Bounds;
            var area=Screen.FromRectangle(ownerBounds).WorkingArea;
            var requested=owner==null?Bounds:new Rectangle(ownerBounds.Left+(ownerBounds.Width-Width)/2,ownerBounds.Top+(ownerBounds.Height-Height)/2,Width,Height);
            Bounds=FitBounds(requested,area,scale);
        }
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
            tips.SetToolTip(hint,hint.Text);
            error.Text="";save.Enabled=validation==null;
        }
        private void Arrange() {
            if(arranging)return;arranging=true;
            try {
                int width=Math.Max(1,surface.ClientSize.Width-S(40));
                heading.Width=Math.Max(1,surface.Width-S(76));
                close.SetBounds(surface.Width-S(52),S(16),S(32),S(28));
                // Viewport runs to just above the footer; the old 136 left a
                // band of dead white between the last row and the buttons.
                body.SetBounds(S(20),S(52),width,Math.Max(1,surface.Height-S(132)));
                footer.SetBounds(S(20),surface.Height-S(72),width,S(64));
                error.SetBounds(0,0,width,S(28));
                Button primary,secondary;
                int primaryWidth,secondaryWidth;
                if(!onCommandsPage&&!onModesPage){primary=save;secondary=cancel;primaryWidth=secondaryWidth=78;}
                else if(confirming){primary=keepEditing;secondary=discardEditing;primaryWidth=80;secondaryWidth=96;}
                else if(onCommandsPage){primary=saveCommands;secondary=backToGeneral;primaryWidth=86;secondaryWidth=70;}
                else{primary=saveModes;secondary=backToGeneral;primaryWidth=86;secondaryWidth=70;}
                int right=Math.Max(1,footer.ClientSize.Width);
                primary.SetBounds(right-S(primaryWidth),S(34),S(primaryWidth),S(PanelTheme.CompactControlHeight));
                secondary.SetBounds(right-S(primaryWidth)-S(6)-S(secondaryWidth),S(34),S(secondaryWidth),S(PanelTheme.CompactControlHeight));
            }finally{arranging=false;}
            ArrangeContent();
            if(onCommandsPage)ArrangeSubpage(commandsPage,CommandsContentHeight,commandList);
            if(onModesPage)ArrangeSubpage(modesPage,ModesContentHeight,modeList);
        }
        private void ArrangeSubpage(Panel page,int height,Panel list) {
            page.SetBounds(0,0,Math.Max(1,body.ClientSize.Width),S(height));
            // Subpage children carry no Right anchors at all — anchoring
            // inside an AutoScroll parent with a nonzero scroll position
            // mangles child rectangles into slivers (BUG-017). Stretch every
            // row here instead, except the fixed-width action buttons, so
            // they stay side by side instead of smearing into gray bars
            // (BUG-014).
            foreach(Control row in page.Controls) {
                var button=row as PanelButton;
                if(button!=null&&(button.Ghost||button.Segment))continue;
                row.Width=Math.Max(1,page.ClientSize.Width-row.Left);
            }
            list.Width=Math.Max(1,page.ClientSize.Width);
            page.Invalidate(true);
        }
        private void ArrangeContent() {
            if(arranging)return;arranging=true;
            try {
                body.AutoScrollMinSize=new Size(0,S(onCommandsPage?CommandsContentHeight:onModesPage?ModesContentHeight:GeneralContentHeight));
                content.SetBounds(0,body.AutoScrollPosition.Y,Math.Max(1,body.ClientSize.Width),S(GeneralContentHeight));
                // Native anchor rounding can leave the last column one pixel outside
                // a scrollbar-reduced viewport. Keep each row inside it; the hint
                // spans under the restore-default button, which sits above it.
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
        protected override void OnFormClosing(FormClosingEventArgs e) {
            base.OnFormClosing(e);
            if(!e.Cancel&&!closingApproved&&(e.CloseReason==CloseReason.UserClosing||e.CloseReason==CloseReason.TaskManagerClosing)&&((onCommandsPage&&UnsavedCommands)||(onModesPage&&UnsavedModes))) {
                e.Cancel=true;DialogResult=DialogResult.None;AttemptLeave(true);
            }
        }
        private void Place(Control parent,Control child,int x,int y,int w,int h){child.SetBounds(S(x),S(y),S(w),S(h));parent.Controls.Add(child);}
        internal void SuspendForToggle() {
            sessionRestoreVersion++;
            rememberedFocus=ActiveControl;rememberedScroll=body.AutoScrollPosition;
            var text=rememberedFocus as TextBoxBase;
            if(text!=null){rememberedSelectionStart=text.SelectionStart;rememberedSelectionLength=text.SelectionLength;}
            Hide();
        }
        internal void RestoreAfterToggle(Form owner) {
            if(!Visible)Show(owner);WindowState=FormWindowState.Normal;
            Bounds=FitBounds(Bounds,Screen.FromControl(this).WorkingArea,scale);
            int version=++sessionRestoreVersion;
            // Owner restoration queues native activation messages. Restore the
            // editor after those messages so activation cannot reset selection.
            BeginInvoke(new Action(delegate {
                if(IsDisposed||!Visible||version!=sessionRestoreVersion)return;
                Activate();Native.SetForegroundWindow(Handle);
                if(rememberedFocus!=null&&!rememberedFocus.IsDisposed&&rememberedFocus.CanFocus){rememberedFocus.Focus();var text=rememberedFocus as TextBoxBase;if(text!=null)text.Select(Math.Min(rememberedSelectionStart,text.TextLength),Math.Min(rememberedSelectionLength,Math.Max(0,text.TextLength-rememberedSelectionStart)));}
                body.AutoScrollPosition=new Point(-rememberedScroll.X,-rememberedScroll.Y);
            }));
        }
        private Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,bool bold) {
            // Transparent so labels sit on whatever surface they decorate —
            // an opaque page-colored label reads as a foreign patch on cards.
            // Right anchors stay off the scrollable subpages: anchoring plus
            // a nonzero AutoScrollPosition is the documented WinForms
            // corruption that mangles child rectangles into slivers
            // (BUG-017); ArrangeSubpage stretches those rows explicitly.
            var label=new Label {Text=text,Font=PanelTheme.CompactFont(size,bold),ForeColor=PanelTheme.Ink,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft};Place(parent,label,x,y,w,h);if(parent==content)label.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;return label;
        }
        private void KeyRow(Control parent,string label,HotkeyBox box,int y) {
            LabelAt(parent,label,0,y,112,34,PanelTheme.BodySize,false);
            var keyFrame=new InputPanel {TabStop=false,BackColor=parent.BackColor};parent.Controls.Add(keyFrame);keyFrame.Cursor=Cursors.Hand;
            box.Font=PanelTheme.CompactFont();box.TextAlign=HorizontalAlignment.Left;box.BackColor=parent.BackColor;keyFrame.Controls.Add(box);keyFrame.Click+=delegate{box.Focus();box.BeginCapture();};
            Action arrange=delegate{keyFrame.SetBounds(S(124),S(y),Math.Max(1,parent.ClientSize.Width-S(124)),S(34));box.SetBounds(S(12),Math.Max(0,(keyFrame.Height-box.PreferredHeight)/2),Math.Max(1,keyFrame.Width-S(24)),box.PreferredHeight);};
            parent.Resize+=delegate{arrange();};arrange();
            box.Enter+=delegate{keyFrame.Accent=true;};box.Leave+=delegate{keyFrame.Accent=false;};
        }
        private Panel GeneralSection(string title,int y,int height) {
            var section=new Panel {BackColor=PanelTheme.Back,TabStop=false};Place(content,section,0,y,420,height);
            LabelAt(section,title,0,0,420,20,PanelTheme.BodySize,true);
            return section;
        }
        private void GeneralChoice(Panel section,string label,PanelChoices choice,int y) {
            LabelAt(section,label,0,y,112,32,PanelTheme.BodySize,false);
            choice.Flat=true;foreach(Control option in choice.Controls)option.BackColor=PanelTheme.Back;
            Place(section,choice,124,y,296,32);
            section.Resize+=delegate{choice.SetBounds(S(124),S(y),Math.Max(1,section.ClientSize.Width-S(124)),S(32));};
        }
        private void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);WindowFrame.ConfigureNativeBorder(Handle);frame.UpdateShape(this,scale,smoothFrame!=null&&smoothFrame.Ready);}
        protected override void Dispose(bool disposing){if(disposing){tips.Dispose();if(smoothFrame!=null)smoothFrame.Dispose();}base.Dispose(disposing);}
    }
}
