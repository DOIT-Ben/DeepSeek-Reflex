# BUG-025：反馈文件可被最后一次追加写穿容量上限

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：server/reflex-feedback/receiver.py。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

反馈文件可被最后一次追加写穿容量上限。

## 复现

receiver-before.log：10 字节上限仍返回 204；test_boundaries 全部通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

server/reflex-feedback/receiver.py:105 的记录编码、117 的追加前容量判断；test_boundaries.py 的 test_exact_store_cap_cannot_be_crossed_by_one_append。

## 根因

只检查当前文件大小，没有把本条记录的编码长度算入容量。

## 修复方案

先编码完整记录，在同一锁内检查已有字节数加记录字节数，再追加。

## 验收标准与结果

旧实现允许一条追加写穿上限；修复后真实回环 HTTP 返回 507，文件字节数保持在上限内。最终 9 项接收端边界测试通过；线上接收服务未部署此修复。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
