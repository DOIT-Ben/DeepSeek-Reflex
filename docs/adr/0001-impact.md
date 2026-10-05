# ADR-0001 架构影响检查

检查日期：2026-10-05。代码身份：main，b614ceb24eed1fefa3513bc2060db20159969a1f，VERSION=1.0.12；检查时工作树干净，远端 main 与本地 HEAD 一致。

检查范围：主窗口唤起／隐藏、原生移动与拉伸、尺寸保存、设置／引导、取词填入、偏好保存及圆角图层。未执行原生应用、登录、跨应用取词、性能测量或全仓审计；以下是代码证据，不是新功能已经实现的声明。

## 现有职责与耦合

| 模块／规模 | 已核对的职责与入口 | 本次影响与风险 |
|---|---|---|
| ChatWindow，648 行 | CaptureSelection → PresentSelection → FillSelection；ShowChat／ToggleWindow；ShowSettings／ShowHelp；ResizeEnd → SaveBounds；WndProc 路由原生消息及热键 | 同时协调窗口生命周期、模态、异步取词、草稿所有权与 WebView。新增几何判定、配置解析或模板编辑会进一步混合职责；该判断来自调用链，不仅是行数。 |
| SmoothFrame，172 行；WindowFrame，137 行 | 四个缓存 alpha 圆角窗口；跟随 HWND 实际置顶层级；WM_MOVING／WM_SIZING 批量移动；主窗命中与边框 | 是已验收的外框基础。边缘标签尺寸小于现有四角图块组合，不能把主窗缩成标签或强套其固定 22 DIP 半径。 |
| SettingsDialog，136 行 | 克隆设置，保存／取消，固定底部操作区，快捷操作回调，模态圆角 | 管理指令适合在同一面板内部切页；避免套第二个原生菜单或重复对话框壳。 |
| WindowSettings，44 行；Preferences，61 行 | 版本 1 设置、旧热键兼容、模式验证、Preferences.Write 临时文件与替换；独立保存 webview-bounds | 新布尔项可缺省为 false；指令目录单独配置，避免损坏指令时重置热键。主窗真实矩形与标签矩形必须分离。 |
| PromptInserter，49 行 | Compose(text, int)；官方 HTTPS origin 双重检查；唯一可见输入框；已有草稿检查及本工具草稿所有权；结果码 | 重用安全插入器。Compose 的整数入口仍被 QuickAnswerWindow 使用，须保留兼容映射，不顺手改实验浮窗。 |
| SelectionCapture | 先取得外部前台窗口，UIA／剪贴板回退，Limit=20000，显式导入入口 | 收纳后取词必须先记录并捕获外部来源，再展开主窗；否则会对自己的窗口取词。 |

## 新职责归属

| 新职责 | 建议归属与方向 | 不加入的位置 | 边界验证 |
|---|---|---|---|
| 收纳状态、外侧屏幕边缘、恢复矩形、显示器变化 | EdgeDockController＋纯几何 DockGeometry；主窗传递事件，控制器发出隐藏／恢复意图 | PromptInserter、Preferences、SmoothFrame | 纯几何测试与原生状态回归；不依赖 WebView 或取词正文 |
| 32×80 DIP 标签、alpha 绘制、点击与非激活显示 | EdgeDockTab；控制器持有其生命周期，标签不持有 WebView 或主窗设置 | 既有主窗四角图层 | 一份标签，hover 不展开，关闭／退出无残留 |
| 内置／自定义指令解析、保存与验证 | PromptCatalog＋CommandStore；Presenter 选稳定 ID，再交给 PromptInserter | ChatWindow.WndProc、热键注册代码 | 内置 ID、兼容整数映射、坏配置与写入失败 |
| 取词两行视图、更多指令、管理子页 | SelectionCommandView、CommandSettingsPage；使用共享面板样式、由主窗传递当前材料 | 网页 DOM、网站布局、SmoothFrame | 焦点、Esc／外部点击、窄宽度和长内容 |

## 已确认事实与限制

- SaveBounds 当前会保存 Bounds 或 RestoreBounds，未认识贴边状态；设计须通过独立主窗矩形记录确保标签尺寸不会进入该入口。
- ToggleWindow 当前根据 Visible 判断隐藏或展开；新入口需先识别 Docked 状态并统一回到 ShowChat。
- ShowSettings／ShowHelp 会暂停热键，关闭时恢复；新管理页应留在同一个模态生命周期中。
- 现有草稿保护由 PromptInserter 与 lastInserted 配合实现；管理指令不得直接写网页输入框或绕过 origin 检查。
- 当前没有贴边或指令目录实现。新增模块和接口是 ADR 提案；本轮只制作 HTML 演示。
