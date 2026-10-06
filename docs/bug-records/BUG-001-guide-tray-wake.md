# BUG-001 引导被托盘收起后，正常唤起链路无法恢复

- 状态：未修复
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

以下为建议，尚未实施。

统一托盘、热键和启动入口的模态状态转换。唤起时先恢复主窗再激活引导，或在模态期间只聚焦面板。

## 验收标准

覆盖设置／引导、两种收起方式以及托盘／热键／任务栏入口；不得留下不可见模态窗口；保留聊天、草稿及四角边框。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
