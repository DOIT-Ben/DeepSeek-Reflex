# BUG-010 发行包中英文 README 的本地链接缺失

- 状态：已修复关闭
- 优先级：P3
- 发现时间与方式：2026-10-06，全项目审查 F10。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：解压后阅读说明、离线帮助。
- 证据边界：真实 v1.0.14 Release ZIP 检查；源码仓库的对应文件存在。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

发行包中英文 README 的本地链接缺失。每份 README 各有 8 个缺失目标：CHANGELOG.md、CONTRIBUTING.md、两个 ADR、docs/branding.md、docs/code-signing.md、交互稿 README 和测试 README。

## 复现

下载 v1.0.14 ZIP，校验公开哈希后解压，逐项核对两份 README 中的相对链接目标。

已知阴性对照：真实包的 22 项清单、全部 payload 哈希和 6 张 README 图片通过；仓库内目标存在。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [packaging/build-release.ps1:20](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/packaging/build-release.ps1#L20)
- [packaging/build-release.ps1:31](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/packaging/build-release.ps1#L31)
- [packaging/test-package.ps1:39](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/packaging/test-package.ps1#L39)
- [README.md:82](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/README.md#L82)

## 根因

包白名单不含这些文档；当前测试只验证 README 图片，未验证其他链接。

## 修复方案

以下保留发现时的修复建议，实施结果见末尾。

必要用户说明随包发布；源码／贡献／设计资料改为明确的 GitHub 页面链接；增加相对链接检查，不把整个源码塞进安装包。

## 验收标准

包内相对链接均存在或替换为有效绝对链接；双语说明一致；严格清单与哈希通过；不混入源码缓存、个人设置或登录资料。

发现时的验收要求保留；本轮状态以以下实施记录为准。

## v1.0.16 实施与验证（2026-10-06）

打包时把没有随包提供的相对文档链接转换为对应版本源码链接；保留随包 README、许可和图片。包验证增加本地链接存在检查，修复包与历史包对照结果见本轮验证报告。

修复源码：[v1.0.16](https://github.com/DOIT-Ben/DeepSeek-Reflex/tree/v1.0.16)。统一证据与范围：[本轮回归说明](../testing/1.0.16-regressions.md)。发现时源码快照仍保留。
