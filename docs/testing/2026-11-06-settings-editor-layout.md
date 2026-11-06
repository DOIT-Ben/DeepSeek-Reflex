# 2026-11-06 设置子页字段遮挡与布局整理

范围：当前工作区的聊天模式、快捷指令、共享输入面与禁用文字按钮。基线 HEAD 为 `e19bfdc`，基线包含原有未提交改动；本轮没有替换个人安装或发布。

## 问题与变更

- 当前 `ArrangeSubpage` 会把横排字段标签拉满整行，标签与输入面重叠，100% 下已稳定复现。标签改为在字段上方独立成行；名称输入高 34 DIP，提示词输入高 88 DIP。
- 表单动作改由同一父行计算宽度，消除恢复默认的纵向错位，并在窄窗口保持可达。「保存到列表」采用次级按钮，最终保存仍为蓝色主按钮。
- 共享 `InputPanel` 填满单一圆角表面，输入框与内边距同色，只保留原有底线状态。禁用 Ghost 按钮与页底融合。

## 证据

本轮隔离产物保留在 `webview-host/tests/results/ui-20261106/`（Git 忽略），修改前的三个源文件另存于其 `source-before/`。测试使用自身创建的窗口和合成示例，没有读取用户聊天或调用反馈服务器。

- `before/checks.log`：新增回归检查在修改前失败，报 `editor field is not covered by sibling labels or actions: light-modes-100 模式名称`。
- `after/checks.log`：针对两个子页的浅／深色、100%／150%／200% 布局、窄窗口滚动、焦点、示例填入、动作可达性和禁用 Ghost；同时覆盖已有模式目录校验、内置覆盖编辑及保存回路。
- `after/editor-light-modes-100.png`、`after/editor-filled-light-modes-100.png` 等：真实 WinForms 控件绘制截图。高缩放截图使用 `ApplyDpi` 验证几何，不声称改动系统缩放。
- `regression.log`：默认全量 529 项通过，配套焦点脚本 10 项通过；该次编译早于最后补充示例填入／滚动可达性断言。高 DPI 轮在既有 `TestInteractionPolish` 鼠标模拟中误开模态设置窗口，停在 `synthetic physical click is delivered`，已结束本轮测试进程；因此该轮全量未完成，不能计为通过。

最终源码的针对性检查命令（相对于项目根目录）：

```powershell
pwsh -NoProfile -File webview-host/tests/run-tests.ps1 -EditorOnly -OutputDirectory webview-host/tests/results/ui-20261106/editor-final
```

两种编译均 158 项通过、无 SKIP；分别以普通及项目高 DPI manifest 编译，均在两主题下走过 100%／150%／200% 几何、短窗口滚动及模式编辑保存。高 DPI manifest 的 150% 截图已核对实际字体、输入面及按钮排版；其它模拟 DPI 组合只报告几何验证，不声称覆盖所有实体字号与缩放组合。`plugins/dsh-reflex` 的 `npm run check` 通过，共享令牌及浏览器产物一致；本轮没有改调色板令牌。

模式边界运行 `run-boundary-tests.ps1 -ChatModeOnly`：50 项边界、11 项草稿、22 项模式、10 项焦点通过，日志为 `boundary.log`。该轮使用隔离拦截的 WebView 页面及合成 DOM，不操作个人官网资料。

## 限制

机器短工作区可导致正文滚动，原生滚动条仍随 Windows 绘制；保存／返回留在固定页脚。未执行实体双屏 DPI 往返、用户安装替换或官网账号操作。发布和 BUG-007 的实体双屏验收继续按各自边界处理。
