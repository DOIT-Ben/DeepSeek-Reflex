# BUG-022：子页最终保存跳过正在编辑的条目

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：SettingsDialog.cs。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

子页最终保存跳过正在编辑的条目。

## 复现

ui-negative.log：模式与指令直接保存均失败；deleted-mode-proof.log：均通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

SettingsDialog.cs:305 的 SaveCommands、436 的 SaveModes、409 的 SaveModeEntry；回归见 SettingsTests.cs 的 TestAuditUi。

## 根因

最终保存只序列化列表，随后把尚未加入列表的编辑标志清空。

## 修复方案

最终保存先校验并接受当前条目；校验失败保持当前页和草稿。

## 验收标准与结果

ui-negative.log 中直接保存当前指令／模式编辑失败；deleted-mode-proof.log 中两套编译均通过。最终保存先接受当前编辑，非法条目保留当前页和文本；不要求用户再额外点一次“保存到列表”。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
