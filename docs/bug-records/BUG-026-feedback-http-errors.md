# BUG-026：反馈编码或存储失败直接断开 HTTP 连接

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：server/reflex-feedback/receiver.py。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

反馈编码或存储失败直接断开 HTTP 连接。

## 复现

receiver-before.log：非法 Unicode 与目录写入均 RemoteDisconnected；9 项接收端边界通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

server/reflex-feedback/receiver.py:105 的 UTF-8 编码与121 的存储错误处理；test_boundaries.py 的 Unicode、存储失败与后续正常请求用例。

## 根因

写入前未检查 UTF-8 编码，存储异常没有对应的 HTTP 失败响应。

## 修复方案

编码错误返回 400，存储异常返回 503，不报告成功、不写入半条编码记录。

## 验收标准与结果

旧实现遇未配对代理字符或存储失败时断开连接；修复后分别返回 400、503，且后续正常请求返回 204。测试使用隔离临时目录、随机回环端口；不写线上反馈。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
