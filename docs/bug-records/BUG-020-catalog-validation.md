# BUG-020：模式与指令目录可载入重复、保留或超限条目

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：ModeCatalog.cs、PromptCatalog.cs。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

模式与指令目录可载入重复、保留或超限条目。

## 复现

catalog-negative-complete.log：保留 ID、同名、超限与保存无效条目失败；catalog-final.log：11 项通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

ModeCatalog.cs:27、75，PromptCatalog.cs:28、69，Preferences.cs:14；目录极限测试见 webview-host/tests/AuditBoundaryTests.cs。

## 根因

读取与保存未共用完整校验，名称归一化在重复检查之后发生。

## 修复方案

统一入口校验、规范化 ID/名称，拒绝覆盖内置 ID、控制字符、重复和超限条目；512 KiB 读取上限；读失败给出明确错误。

## 验收标准与结果

原实现负例见 catalog-negative-complete.log；修复后 catalog-final.log 的 11 项全部通过，覆盖保留 ID、规范化后的重名、控制字符、无效保存和 512 KiB 限额。有效的最大目录及 Unicode 往返通过。此处校验使用 UTF-16 长度；不改变现有文件格式。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
