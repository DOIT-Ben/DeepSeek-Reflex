# 对话模式验证记录

日期：2026-10-12。基线：`65768104132e2f0bcf9db92ab6094c24198e503b`，`main`，v1.0.16。修改前工作区无未提交改动。此次仅修改本地源码与文档，未提交、推送、更新公开发行包或替换本机安装。

## 通过的验证

| 命令／层级 | 本轮结果 | 范围 |
| --- | --- | --- |
| `webview-host/build.ps1 -Offline` | 通过 | C# 5、x64、警告视为错误，构建生产 EXE 与兼容启动器 |
| `webview-host/tests/run-tests.ps1` 的 default／high-dpi 阶段 | 各 280 项通过 | 原生窗口、最小宽度模式栏布局、取词栏收起、模式保存与拒写保全；两阶段聚焦检查各 10 项通过，兼容启动器通过 |
| `webview-host/tests/run-boundary-tests.ps1 -ChatModeOnly` | 退出码 0 | 24 项 Core／真实 WebView／宿主联动检查，3 项内存剪贴板，11 项草稿，19 项模式 DOM，10 项聚焦 |
| `git diff --check` | 通过 | 差异中的空白与格式问题 |

最终专项证据目录：`%TEMP%\Reflex-mode-20261012-ready`，日志：`%TEMP%\Reflex-mode-20261012-ready.log`。常驻翻译模式的原生控件截图为该目录的 `chat-mode-host-translate.png`。普通／高 DPI 窗口证据在 `%TEMP%\Reflex-mode-20261012-full`。

真实 WebView 夹具以 HTTPS 聊天来源提供本地 HTML，拦截所有资源请求，并使用独立资料目录。验证鼠标／Enter 两次提交均只加一个前缀、模式切换保留原文、历史编辑器不变、一次性取词绕过，以及生产模式切换与页面重载联动。无外部模型请求，不读取或改写个人账号资料。

## 未通过与未覆盖

完整 `run-tests.ps1` 的边界阶段未通过，不能声称完整回归成功。原有 `native DPI message honors suggested bounds including negative coordinates` 检查期望模拟消息的 1.5 倍缩放，消息队列处理后系统恢复为 2 倍，位置与圆角仍正确。本轮观测：`X=-720,Y=40,Width=960,Height=1240,scale=2,aligned=True`。导出基线 HEAD 的原始源码至独立临时目录重新编译运行，旧代码在同一断言失败；未修改生产 DPI 行为或放宽旧断言。

额外宿主验收遇到原有显式背景色设置的运行时异常：`NotImplementedException`，内部为 `ICoreWebView2Controller2` 的 `E_NOINTERFACE`，发生于 `DefaultBackgroundColor` 设置。模式宿主夹具换用未显式设置背景色的默认 WebView 控件后，执行相同生产导航／模式代码并通过。替换仅限测试，生产背景色与初始化代码保持不变；原控件初始化的本机兼容性仍未通过，不能据此宣称应用启动端到端验收成功。

未实测个人官网登录页面、真实模型译文与其他回答质量、物理输入法按键、两块不同缩放显示器及旧安装版升级。模式定义和本地注入已完成；发布与真实官网适配验收需要另行推进。
