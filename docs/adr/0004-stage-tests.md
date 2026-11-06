# ADR-0004 阶段合同

2026-11-04。

| 阶段 | 范围 | 门禁 |
| --- | --- | --- |
| S1 模式自定义 | ModeCatalog、ChatModes Effective、注入/取词消费链、设置模式页 | `run-tests.ps1` 全量；新增 SettingsTests/TestModeCatalog（存储/校验/容错/目录层/页面冒烟）、BoundaryTests 覆盖与自定义模式进注入 catalog 断言、chat-mode-tests.cjs 覆盖前缀与自定义模式用例；DSH 插件 `npm test` 兼容回归 |
| S2 网页主题接线 | ApplyWebProfileTheme（初始化+ApplyTheme） | 真机验收：设置切换深/浅/跟随系统，内嵌 chat.deepseek.com 跟随变色；弹窗同步；接口失败不中断初始化（BoundaryTests 既有生命周期用例保持绿） |

通用约束：隔离偏好目录（测试脚本既有 root 重定向）；不动个人安装；真机验收截图留档 `docs/testing/`。2026-11-06 验证约束更新：失败必须定位到实现、环境或夹具并保留证据；必要检查不允许仅凭重跑变绿判定通过。
