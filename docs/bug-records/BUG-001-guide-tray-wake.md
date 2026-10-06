# BUG-001 引导被托盘收起后，正常唤起链路无法恢复

- 状态：已修复关闭
- 优先级：P1
- 发现时间与方式：2026-10-06，全项目审查 F01。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：窗口唤起、首次引导和使用帮助。
- 证据边界：隔离原生窗口复现；未证明所有 Windows 系统恢复入口均失效。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

引导被托盘收起后，正常唤起链路无法恢复。主窗 Normal、引导可见 → 主窗 Minimized、引导不可见 → ShowChat 后仍然 Minimized、引导不可见。

## 复现

打开使用帮助，保持默认“最小化到任务栏”；左键点击托盘，再点击托盘或调用打开入口。

已知阴性对照：设置面板同样收起后，ShowChat 可恢复主窗及面板。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/ChatWindow.cs:221](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L221)
- [webview-host/ChatWindow.cs:336](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L336)
- [webview-host/ChatWindow.cs:349](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L349)
- [webview-host/ChatWindow.cs:619](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L619)

## 根因

托盘直接执行 ToggleWindow，没有模态状态保护；ShowChat 在 guideOpen 时只激活引导并提前返回，未恢复其所属主窗。模态期间热键又暂停。

## 修复方案

以下保留发现时的修复建议，实施结果见末尾。

统一托盘、热键和启动入口的模态状态转换。唤起时先恢复主窗再激活引导，或在模态期间只聚焦面板。

## 验收标准

覆盖设置／引导、两种收起方式以及托盘／热键／任务栏入口；不得留下不可见模态窗口；保留聊天、草稿及四角边框。

发现时的验收要求保留；本轮状态以以下实施记录为准。

## v1.0.16 实施与验证（2026-10-06）

模态期间托盘操作聚焦当前面板；显式唤起先恢复主窗，再恢复设置或引导。两种收起方式 × 两类面板的真实模态检查通过。测试直接调用托盘所连接的方法；没有宣称所有实体键盘或任务栏环境均已复验。

修复源码：[v1.0.16](https://github.com/DOIT-Ben/DeepSeek-Reflex/tree/v1.0.16)。统一证据与范围：[本轮回归说明](../testing/1.0.16-regressions.md)。发现时源码快照仍保留。
