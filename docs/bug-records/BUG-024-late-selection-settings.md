# BUG-024：设置打开后延迟完成的取词仍填入输入框

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：ChatWindow.cs、AutoSelectionController.cs。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

设置打开后延迟完成的取词仍填入输入框。

## 复现

ui-negative.log：延迟取词暴露取词栏；deleted-mode-proof.log：不填入、不暴露。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

ChatWindow.cs:385 的 CaptureAsync 返回后检查、399 的 PresentSelectionAt、481 的 FillSelection；AutoSelectionController.ShowSelection 同样检查主窗编辑状态。

## 根因

热键入口暂停没有取消已在等待的取词结果，后续路径缺少状态检查。

## 修复方案

在返回结果、显示选择和开始填入时重查设置/引导状态，自动取词窗口采用同一限制。

## 验收标准与结果

ui-negative.log 中设置期间的延迟取词仍展示取词栏；修复后的两套专项检查均不填入、不展示。该证据验证结果返回后的状态守卫，不声称可以撤回此前已经提交给 WebView 执行的脚本。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
