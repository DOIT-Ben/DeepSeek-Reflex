# BUG-031：PowerShell 升级脚本递归删除旧安装目录中的非托管文件

- 状态：已修复，本地专项验收通过（2026-11-06）。
- 影响范围：install.ps1。
- 证据边界：隔离数据和本地运行；不代表线上服务部署或所有外部应用兼容。

## 现象

PowerShell 升级脚本递归删除旧安装目录中的非托管文件。

## 复现

installer-negative.log：原脚本未通过递归删除 AST 检查；installer-final.log：修复后的脚本断言及 NSIS Upgrade 隔离场景通过。日志位于 `webview-host/tests/results/audit-20261106/`。

## 代码证据

install.ps1:107 保留旧安装目录；packaging/tests/test-installer-flow.ps1 的 InstallScriptPath 与 AST 断言。

## 根因

把整个旧程序目录等同于受管理文件集合，目录中自放的文件也会被删除。

## 修复方案

保留旧目录，不终止或递归删除旧目录中的内容；只替换已经列举并备份的受管理文件。此修复为源码审查验证，未在个人安装目录执行安装脚本。

## 验收标准与结果

installer-negative.log 中原脚本含递归 Remove-Item 的 AST 检查失败；installer-final.log 中修复检查通过。NSIS Upgrade 路径嵌套、进程锁和保留非托管文件场景使用完全重定向的隔离夹具；未在个人安装目录执行 PowerShell 安装脚本。

汇总和其余验证边界见[本轮功能审计](../testing/2026-11-06-functional-boundaries.md)。
