# BUG-030：桥接回归入口缺少模式目录编译依赖

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：webview-host/tests/run-bridge-tests.ps1。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

桥接回归入口缺少模式目录编译依赖。

## 复现

bridge-before.log：CS0246 ModeCatalog；bridge-final.log：12 项真实传输通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

webview-host/tests/run-bridge-tests.ps1:18 的实际生产依赖；BridgeTransportTests.cs。

## 根因

ChatModes 增加 ModeCatalog 依赖后，独立测试仍只编译原来的三个文件。

## 修复方案

编译真实目录和偏好依赖，将偏好初始化重定向到隔离目录，不用目录逻辑的替身。

## 验收标准与结果

旧测试入口编译报 CS0246；加入实际 ModeCatalog、隔离 Preferences 及其程序集引用后，12 项真实 HTTP／WinExe／认证管道检查通过。没有用目录 stub 掩盖缺失依赖。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
