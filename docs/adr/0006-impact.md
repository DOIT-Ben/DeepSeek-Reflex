# ADR-0006 影响检查

2026-11-07，基线 fbbcee8284346806e40100b1da37393dd422f6c6。开始时主活动工作树干净；无项目级 AGENTS 或独立待办入口。仅分析更新功能直接调用链，不评估全仓健康。

| 模块 | 现有职责与证据 | 新职责限制 |
| --- | --- | --- |
| Program.Main | mutex、桥接／快捷方式特殊入口、WinForms 启动 | 只增加更新 worker 的早期分派，不放下载或安装循环 |
| ChatWindow | WebView、热键、设置和托盘协调；退出设 quitting 后 Close，保存窗口位置 | 只提供未保存门禁、打开更新窗和正常退出，网络及事务不得堆入 |
| SettingsDialog | 三个页面、草稿、校验和模式目录编辑；未保存判断仅覆盖子页 | 复用布局与 Action 回调，加更新入口及只读脏状态查询，不关闭表单来启动检查 |
| Preferences.Root | Roaming 中的设置和 WebView 登录资料 | 安装器不得访问此目录；沿用现有配置格式 |
| install.ps1 | 文件 staging、受管文件备份与原子替换，强制停止精确安装进程 | 复用受管文件原则；不可直接作为无感更新器：缺少未保存门禁及 worker 握手 |
| setup.nsi | per-user 安装，忙碌时拒绝；无整体回滚 | 保留手动安装。更新使用已发布 ZIP，不能把静默 NSIS 当作事务保障 |
| packaging/build-release.ps1 | 版本唯一输入、21 文件 ZIP 与 SHA256SUMS | 沿用 ZIP 和品牌／版本检查，不新增服务端或签名服务 |
| OwnedWindowPlacement / EditorTextBox | 共享初始位置、DPI、输入选区 | 新更新窗复用，不重做设计系统 |

建议新建 UpdateFeed（网络和正式版协议）、UpdatePackage（ZIP 验证）、UpdateTransaction（磁盘事务和独立 worker）、UpdateCoordinator（状态机／未保存门禁组合）、UpdateDialog（UI）。调用方向 UI→Coordinator→Feed/Package/Transaction；Program 只分派 worker。升级和退出耦合风险较高，必须以真实隔离进程验证，不能以 mock 成功替代重启证据。独立审查仍需核对协议和事务细节。
