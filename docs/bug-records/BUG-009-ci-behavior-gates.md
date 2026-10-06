# BUG-009 发布 CI 未接入已有核心行为回归

- 状态：已修复关闭
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F09。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：构建门禁与发行可信度。
- 证据边界：受检工作流与成功 CI 运行核对；不代表所有可能的外部工作流均已检索。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

发布 CI 未接入已有核心行为回归。当前流程仅解析脚本、构建打包、核对 ZIP／图标和上传产物；未执行已有窗口／聚焦回归。

## 复现

读取 .github/workflows/build.yml 的全部步骤，检索 run-tests.ps1、focus-tests.cjs 与草稿／剪贴板边界测试调用。

已知阴性对照：当前流程可以检测包清单、图标或哈希错误；这些门禁仍有效。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [.github/workflows/build.yml:13](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/.github/workflows/build.yml#L13)

## 根因

已有测试未接入公开门禁，核心行为退化仍可能得到绿色构建。

## 修复方案

以下保留发现时的修复建议，实施结果见末尾。

先接入适合 CI 的设置、脚本、草稿和剪贴板状态测试；原生交互测试使用适配的 Windows 桌面 runner，明确记录 Skip，先验证 runner 兼容性。

## 验收标准

刻意破坏核心行为测试应使 CI 失败；正常提交通过；产物关联确切提交；依赖桌面权限的跳过必须可见，不能算作通过。

发现时的验收要求保留；本轮状态以以下实施记录为准。

## v1.0.16 实施与验证（2026-10-06）

GitHub Actions 新增 CoreOnly 行为门禁：生产代码生成的草稿／聚焦脚本、剪贴板适配器、偏好与故障分类；本机同入口通过。交互桌面检查仍由本机完整入口负责，CI 明确跳过；公开 [CI 37421086727](https://github.com/DOIT-Ben/DeepSeek-Reflex/actions/runs/37421086727) 已通过，包含打包、包验证和 Core behavior gates；据此关闭。

修复源码：[v1.0.16](https://github.com/DOIT-Ben/DeepSeek-Reflex/tree/v1.0.16)。统一证据与范围：[本轮回归说明](../testing/1.0.16-regressions.md)。发现时源码快照仍保留。
