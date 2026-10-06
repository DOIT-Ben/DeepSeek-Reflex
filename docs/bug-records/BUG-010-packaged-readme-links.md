# BUG-010 发行包中英文 README 的本地链接缺失

- 状态：未修复
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

以下为建议，尚未实施。

必要用户说明随包发布；源码／贡献／设计资料改为明确的 GitHub 页面链接；增加相对链接检查，不把整个源码塞进安装包。

## 验收标准

包内相对链接均存在或替换为有效绝对链接；双语说明一致；严格清单与哈希通过；不混入源码缓存、个人设置或登录资料。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
