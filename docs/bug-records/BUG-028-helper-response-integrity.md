# BUG-028：辅助进程异常退出或坏编码仍可能被当成成功

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：plugins/dsh-reflex/handoff.js。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

辅助进程异常退出或坏编码仍可能被当成成功。

## 复现

helper-before.log：非零退出负例失败；plugin-verified.log：对应门禁通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

plugins/dsh-reflex/handoff.js:63 的辅助进程 close 处理；tests/handoff.test.mjs 的非零退出与非法 UTF-8 用例。

## 根因

只验证 stdout 的 JSON 结构，未校验进程退出码，UTF-8 解码会静默替换坏字节。

## 修复方案

必须正常退出并通过严格 UTF-8 解码，否则返回 outcome-unknown，避免误导和自动重试。

## 验收标准与结果

旧实现把带有合法 JSON 的非零退出当作成功；修复后退出码异常或 UTF-8 解码失败均返回 outcome-unknown。插件 21 项检查通过，正常 helper→认证管道链路另有 12 项真实传输检查。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
