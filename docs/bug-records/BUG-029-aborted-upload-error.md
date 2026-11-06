# BUG-029：上传中断后的 socket 错误可成为未处理异常

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：plugins/dsh-reflex/handoff.js。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

上传中断后的 socket 错误可成为未处理异常。

## 复现

upload-abort-before.log：aborted 后 error 抛出；30 次中断循环及全部 21 项 Node 测试通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

plugins/dsh-reflex/handoff.js:31 的 readStage.fail；tests/handoff.test.mjs 的中断事件顺序与监听释放用例。

## 根因

aborted 清理时先移除了 error 监听，而 IncomingMessage 随后还会发 error。

## 修复方案

保留错误吸收监听到 close，关闭后移除；验证 data/error 监听均释放。

## 验收标准与结果

旧实现的 aborted 后续 error 可抛未处理异常；修复后连续 30 次 aborted→error→close 都以受控失败完成，close 后 error 监听移除。21 项 Node 检查通过；不等同于重新安装真实 DSH 宿主验收。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
