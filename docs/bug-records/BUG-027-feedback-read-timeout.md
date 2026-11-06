# BUG-027：不完整反馈请求长期占住工作线程

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：server/reflex-feedback/receiver.py。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

不完整反馈请求长期占住工作线程。

## 复现

receiver-timeout-before.log：读取超时失败；test_incomplete_body_expires 修复后通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

server/reflex-feedback/receiver.py:47 的 setup 与106 的 TimeoutError 分支；test_boundaries.py 的半包 socket 用例。

## 根因

请求连接没有读取期限，短于 Content-Length 的请求体可一直等待。

## 修复方案

设置连接读取超时，正文未读完超时返回 408；使用独立回环端口复验。

## 验收标准与结果

旧实现未在限定时间内返回；修复后在测试专用的 0.05 秒空闲读取超时下返回 408。生产默认空闲读取超时为 3 秒；本测试不代表生产代理或所有慢速攻击场景的压测。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
