# BUG-011 登录与外链弹窗没有复用统一外框

- 状态：未修复
- 优先级：P3
- 发现时间与方式：2026-10-06，全项目审查 F11。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：弹窗视觉、导航与关闭反馈。
- 证据边界：源码创建路径确认样式差异；本轮没有触发真实账号登录弹窗。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

登录与外链弹窗没有复用统一外框。该路径只设置普通 Form 的标题、尺寸和图标，没有复用统一标题栏与圆角外框。

## 复现

核对 OpenPopup 新建 Form、WebView 和关闭逻辑，与主窗／设置／引导的 WindowFrame、SmoothFrame 创建链比较。

已知阴性对照：主窗、设置与引导已使用统一组件，相关四角回归通过。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/ChatWindow.cs:525](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L525)
- [webview-host/ChatWindow.cs:533](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L533)

## 根因

弹窗创建路径与现有统一窗口组件分离，保留系统原生外框。

## 修复方案

以下为建议，尚未实施。

受控登录／链接窗复用统一样式，同时清楚呈现域名与关闭反馈；适当外链交给默认浏览器；处理初始化、重定向和关闭生命周期。

## 验收标准

登录重定向、关闭、父窗置顶、DPI 及圆角反馈一致；保持网址来源清晰；实际账号登录验收另做，不以样式测试代替。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
