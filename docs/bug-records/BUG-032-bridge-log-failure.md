# BUG-032：诊断日志失败会终止唯一桥接服务循环

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：ReflexBridge.cs。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

诊断日志失败会终止唯一桥接服务循环。

## 复现

bridge-log-before.log：日志抛异常后下一次请求失败；bridge-final.log：12 项真实传输通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

webview-host/ReflexBridge.cs:116 的 ReportUnexpected；BridgeTransportTests.cs 的日志回调故障用例。

## 根因

catch 内直接调用诊断回调，回调再次抛出的异常逃出 Serve 循环。

## 修复方案

诊断回调失败单独吞掉，保留按异常类型去重，服务继续处理后续请求。

## 验收标准与结果

bridge-log-before.log 中回调抛错后下一请求失败；bridge-final.log 中首次调用有界返回、后续调用成功，12 项真实传输检查通过。日志仍按异常类型去重，诊断失败不再终止服务循环。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
