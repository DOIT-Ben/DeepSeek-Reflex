# ADR-0003 架构影响检查

2026-10-25，受检 `main@ac23e84a9e8a80cfe1e9670317594e0c6b2437b2`。独立 contract_architect `adr0003_impact` 只读检查，主线整理落盘；未改源码或启动安装/反馈。

目标为已审查 Issues #1–#5 的有界行为恢复。排除发布、安装替换、真实请求、个人资料迁移与 GitHub 关单。规模为受检版本近似行数，不按行数判定过载。

| 模块/规模 | 职责与调用链证据 | 结论/落点 |
| --- | --- | --- |
| packaging/setup.nsi / 172 | 必需段75–123：占用→写载荷→快捷方式→卸载登记；112额外递归清旧目录 | 删除危险额外职责，修复原成功分支；不添加复杂资料迁移或删除模块 |
| webview-host/TrayMenu.cs / 99 | UpdateSize25–49量测；renderer84–95绘制；模式/托盘/更多指令复用 | 局部统一文本标志，不改ChatWindow钩子与状态机 |
| webview-host/SettingsDialog.cs / 362 | ShowGeneralPage236–245、ShowCommandsPage247–258均调用ApplyDialogHeight55；Shown132、ApplyDpi329–335已拟合工作区 | 页面高度统一复用FitBounds，不增第二套算法或改共享FitBounds语义 |
| webview-host/FeedbackDialog.cs / 198 | BuildJson135–139、Send141–159持有请求；152成功记忆；构造96–97回填 | 在Send中使用提交联系方式快照，成功保存空值；不增加缓存/协议/重试层 |
| webview-host/Preferences.cs / 77 | ReadText支持空；Write使用.new替换；Root初始化执行真实目录迁移 | 生产接口不变，测试编译副本直接定向临时Root，不能反射前先触发个人迁移 |
| webview-host/tests/SettingsTests.cs / 954 | 菜单公式177–182、固定高度/小窗滚动、非空contact430/439 | 缺实际绘制、组合切页和空记忆；新增结果导向测试，不机械复制公式 |

## 职责边界与技术取舍

- 所有生产修复可留在现有内聚模块，没有本次必须新增的公共 API、设置版本或服务；不借修复重构整个设置框。
- #1取消旧目录自动清理，保留少量旧程序文件，是比路径别名/重解析点防护加白名单迁移更保守的技术选择。当前卸载器仍按既有名单删除。安装写入失败的历史部分安装语义不变，不声称新增事务回滚。
- #2文件存在不等于占用；新EXE/旧Float/旧launcher均区分成功继续与失败中止。
- #3原最大宽度、勾选槽、悬停带和超长省略保持；量测/绘制应采用相同NoPrefix/NoPadding语义。
- #4工作区取当前窗口屏幕与scale，反复切页应幂等；保留编辑稿及固定页脚，不重建WebView。
- #5在await前捕获提交contact，成功记忆包括空串；失败保留旧值。远端已成功而本地偏好写失败仍沿用成功语义，不重发。发送期间改正文可能重新启用发送的旧并发问题不在本次五项内。

## 验证与局限

安装需要真实NSIS指令的隔离fixture，文件访问保留，shell目录重定向、注册表和注册进程显式替身；不是正式安装包用户升级验收。原生需要真实renderer/页面调用和mock poster，Preferences初始化隔离。具体矩阵由唯一[测试合同](0003-stage-tests.md)维护。

检查没有发现必须向用户重新确认的重大产品取舍。NSIS路径、runner实现和产物路径属执行事实；全部只在本轮本地范围核对。方案审查独立进行，本报告不自授予PASS。
