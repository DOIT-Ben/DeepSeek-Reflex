# Contributing

欢迎反馈问题与提交 Pull Request。请先搜索现有 Issue，并描述 Windows 版本、应用版本、缩放比例、复现步骤和期望行为。不要上传 Cookie、登录资料、完整 WebView2 profile 或含私人对话的日志 / 截图。

## Development

- Windows、PowerShell 7、.NET Framework 4.8、WebView2 Runtime。
- `pwsh -NoProfile -File .\webview-host\build.ps1`：首次构建；已有 SDK 可加 `-Offline`。
- Node.js 仅用于模拟 DOM 的测试；`pwsh -NoProfile -File .\webview-host\tests\run-tests.ps1` 创建隔离偏好和自有窗口。
- `pwsh -NoProfile -File .\packaging\build-release.ps1`：生成安装和 ZIP，要求 NSIS 3。

保留 C# 5 / x64 兼容性。窗口外框、热键、网站注入、取词及安装改动请补上对应检查；不要用模拟 DOM 或消息测试宣称已验证所有应用的物理键盘兼容性。不要把构建缓存、发行 EXE、测试日志或个人设置提交到仓库。

`VERSION` 是版本源。发布前同步应用 manifest，确保二进制、安装包、CHANGELOG 和 Release 标签版本一致。Pull Request 默认授权按本项目 MIT 许可分发贡献。

## Maintainer delivery workflow

维护者完成每次修改后，默认持续执行完整交付流程：运行对应回归检查，提交并推送变更，提升补丁版本并同步文档，构建并验证安装包与 ZIP，确认 GitHub 构建通过，再更新公开 Release 和本机安装。相同项目与交付范围内无需反复请求确认。

只提交本次涉及的文件，保留无关改动、设置及登录数据。发行文件的版本和哈希必须与经过验证的产物一致；检查失败或遇到阻塞时明确报告，不把仅本机修复称为发行完成。受信任签名尚未接入时，继续明确标注发行包未签名。
