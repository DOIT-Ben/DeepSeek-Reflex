# BUG-008 自绘开关开启时无障碍接口仍报告 Off

- 状态：已修复关闭
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F08。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：读屏与辅助自动化。
- 证据边界：真实 Windows UI Automation 与 MSAA 接口复现；未做完整读屏软件验收。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

自绘开关开启时无障碍接口仍报告 Off。customChecked=True；TogglePattern=True；ToggleState=Off；MSAA 不含 Checked，而文本描述为“已开启”。

## 复现

在隔离窗口将 PanelSwitch.Checked 设为 true，通过 UI Automation 查询 TogglePattern.ToggleState，并与标准 CheckBox 对照。

已知阴性对照：同一窗口的标准 CheckBox 报告 Checked；自绘控件的可访问文本描述正确，但状态错误。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/SettingsPanelControls.cs:81](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SettingsPanelControls.cs#L81)
- [webview-host/SettingsPanelControls.cs:83](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SettingsPanelControls.cs#L83)

## 根因

Checked 只更新描述和绘制，继承 Button 的可访问对象未提供正确检查状态及变化通知。并非缺少 TogglePattern。

## 修复方案

以下保留发现时的修复建议，实施结果见末尾。

为自绘开关实现正确的 Checked／ToggleState 和状态变化通知；检查分段选择控件的选择状态，但不把未测控件算作已确认缺陷。

## 验收标准

开／关、鼠标／键盘操作均与 UIA／MSAA 一致；状态事件可观察；读屏与动画偏好检查；保留当前视觉样式。

发现时的验收要求保留；本轮状态以以下实施记录为准。

## v1.0.16 实施与验证（2026-10-06）

开关提供 Checked 状态、状态变化通知和无障碍默认操作。真实 MSAA 与 UI Automation TogglePattern 的 On／Off 和切换检查通过。

修复源码：[v1.0.16](https://github.com/DOIT-Ben/DeepSeek-Reflex/tree/v1.0.16)。统一证据与范围：[本轮回归说明](../testing/1.0.16-regressions.md)。发现时源码快照仍保留。
