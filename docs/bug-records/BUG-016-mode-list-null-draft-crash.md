# BUG-016: 未进过聊天模式页时 DPI 缩放触发模式列表空引用崩溃

- 状态：已修复关闭（2026-11-06，用户反馈"组件消失"后按 150% 用户流复现抓到）
- 优先级：P1（布局路径未处理异常 → 界面半成品，用户表现为"组件消失"）
- 影响面：设置对话框在任何 `modeList` 尺寸变化路径（WM_DPICHANGED/ApplyDpi、宿主缩放）且聊天模式页尚未激活过时

## 现象

用户安装 1.0.19 后报"东西组件都消失到哪里去了"。复现探针按用户流（开设置→切快捷指令页→ApplyDpi 1.5）稳定触发：

```
NullReferenceException 在 SettingsDialog.RenderModeList()
   在 Control.OnResize → ScaleControl → Control.Scale
   在 SettingsDialog.ApplyDpi
```

## 根因

`modeDraft` 字段仅在首次激活聊天模式页时由 `LoadModeDraft()` 填充，而 `modeList.Resize` 处理器在构造期 `BuildModesPage()` 就已挂接；任何先于首次激活的 `modeList` 尺寸变化（DPI 缩放重排 body 子项）都会让 `RenderModeList` 读到 null 的 `modeDraft`。WinForms 布局路径中的未处理异常会中止当前布局与重绘，界面停留在半成品状态——控件看似"消失"。

## 修复（实施记录 2026-11-06）

- `RenderModeList` 空引用守卫：`modeList==null||modeDraft==null` 时跳过渲染（页面激活后 `RenderModeList` 会再次调用，无需补偿）。
- 同类可见性风险预防：命令页/模式页内容预算加高（338→356、420→436），动作行尾部保持 ≥24 DIP 填充，窗口高度被屏幕钳制时先裁到留白而不是控件。
- 回归测试：`TestInteractionPolish` 新增"未访问模式页时 ApplyDpi 不抛异常且动作行在页内"检查（含返回常规页恢复后续流程）。

## 验收证据

- `dpi150` 探针（150% 用户流：切命令页→ApplyDpi→点「填入示例」「保存到列表」→切模式页）修复前稳定 NRE，修复后全程无异常并出三张截图。
- 全量回归通过后重装安装版，待用户实机复核。
