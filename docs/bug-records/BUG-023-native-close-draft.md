# BUG-023：原生关闭绕过未保存确认

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：SettingsDialog.cs。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

原生关闭绕过未保存确认。

## 复现

ui-negative.log：WM_CLOSE 关闭并丢弃草稿；deleted-mode-proof.log：确认前保持打开、明确放弃后关闭。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

SettingsDialog.cs 的 OnFormClosing、AttemptClose；回归见 SettingsTests.cs 的 TestAuditUi。

## 根因

只有自绘关闭按钮经过 AttemptLeave，系统关闭消息未受保护。

## 修复方案

在 FormClosing 拦截 UserClosing/TaskManagerClosing；明确放弃后才允许关闭，所有者退出仍可清理窗口。

## 验收标准与结果

原生 WM_CLOSE 在此运行时报告 TaskManagerClosing；旧实现绕过确认。修复后该路径和 UserClosing 都保留有修改的表单并询问是否放弃；明确放弃后关闭。两套编译通过；所有者退出不被此确认阻塞。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
