# BUG-033：主页旧快照会写回已删除的当前模式

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：ChatWindow.cs。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

主页旧快照会写回已删除的当前模式。

## 复现

deleted-mode-before.log：删除后保存主页写回旧 ID；deleted-mode-proof.log：内存与文件都保留 normal。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

webview-host/ChatWindow.cs:846 的 ApplySettings；SettingsTests.cs 的 general settings save cannot resurrect a deleted active mode。

## 根因

模式管理已经更新宿主状态，主页仍以打开时的快照覆盖未由该页编辑的 ChatMode。

## 修复方案

应用主页设置时保留宿主当前 ChatMode，只接受该页实际管理的设置。

## 验收标准与结果

deleted-mode-before.log 中保存主页重新写回已删 ID；deleted-mode-proof.log 中按 m-audit→normal→normal 检查内存与文件，两套编译各 10 项检查通过。此最终补丁采用专项复验，不把更早的全量结果当成补丁后的重跑。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
