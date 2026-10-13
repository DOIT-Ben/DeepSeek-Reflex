# BUG-012 重复设置默认背景色请求不支持的 WebView 接口

- 状态：已修复关闭（v1.0.17 验证边界内）
- 优先级：P2
- 发现：2026-10-12 隔离宿主验收；2026-10-13 发布前复验。
- 受检提交：`59394346add5f7d1a774fae0ead2289aea1e60c7`。
- 影响：主窗及重建、链接／登录弹窗和旧独立取词问答初始化。
- 边界：隔离 WebView 与生产控件；不将该结果推广为所有 Runtime 版本的兼容性结论。

## 现象

Core 对象创建后，WinForms 初始化因 `DefaultBackgroundColor` 设置抛出 `NotImplementedException`。内部 `ICoreWebView2Controller2` 查询返回 `E_NOINTERFACE`，宿主停留在 `about:blank` 并显示打开失败。

## 复现

在独立 profile 中初始化原生产控件；显式指定 `Color.White` 的路径失败，未显式指定背景的相同 SDK 控件可初始化。两份 DLL 的安装与构建哈希一致，阴性对照排除简单的 SDK 文件不一致。

## 代码／行号证据

受检提交中的赋值位于 [ChatWindow.cs:187](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/59394346add5f7d1a774fae0ead2289aea1e60c7/webview-host/ChatWindow.cs#L187)、[PopupWindow.cs:10](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/59394346add5f7d1a774fae0ead2289aea1e60c7/webview-host/PopupWindow.cs#L10) 及主窗重建／QuickAnswerWindow 创建路径。异常栈定位 `CoreWebView2Controller.set_DefaultBackgroundColor`。

## 根因

为重复设置既有默认值而触发额外控制器接口调用。微软说明该属性默认是不透明白色；见 [ICoreWebView2Controller2 官方合同](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2controller2)。本轮只确认该接口调用在受检环境不受支持，未推断所有 Runtime 的成因。

## 修复方案

删除四条创建／初始化路径的重复白色赋值，沿用默认背景。移除测试中的等价控件替换，直接验证原生产控件。未吞掉初始化异常或关闭安全设置。

## 验收标准与结果

生产控件保持不透明白色并完整初始化；真实隔离进程退出后重建可用；关闭中的弹窗初始化正常取消；模式、重载与跨屏草稿检查通过。完整回归退出码 0，见 [v1.0.17 发布验证](../testing/1.0.17-release.md)。没有新增跨问题经验条目。
