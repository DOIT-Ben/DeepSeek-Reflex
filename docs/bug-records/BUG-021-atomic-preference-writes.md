# BUG-021：并发偏好写入争抢同一个临时文件

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：Preferences.cs。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

并发偏好写入争抢同一个临时文件。

## 复现

catalog-before.log：60 路写入出现 AggregateException；catalog-final.log：并发及失败清理通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

Preferences.cs:47 的 Write；并发与清理断言见 webview-host/tests/AuditBoundaryTests.cs。

## 根因

所有写入共用固定 .new 文件名，失败也没有清理本次临时文件。

## 修复方案

进程内串行化写入，使用每次唯一的同目录临时文件，原子替换并在 finally 清理。

## 验收标准与结果

原实现 60 次并发写入发生临时文件争用；修复后保留完整的一个有效值，且没有 .new 临时文件残留。替换失败时原文件保留，属于本次写入的临时文件清理通过。仅验证同进程并发，不把它称作跨进程事务。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
