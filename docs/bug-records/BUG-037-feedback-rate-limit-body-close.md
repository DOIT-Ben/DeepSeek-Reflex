# BUG-037：反馈限流在请求体到达前关闭连接

- 优先级：P2
- 状态：已修复，本地接收端回归与有界并发检查通过；未部署线上服务。

## 现象

Windows CI 的并发反馈检查出现 ConnectionAbortedError 10053，未收到预期的 429。[失败运行](https://github.com/DOIT-Ben/DeepSeek-Reflex/actions/runs/42213213103)的设置、构建、核心和插件检查均已通过。

## 复现

达到限额后，只发送带合法 Content-Length 的请求头，稍后再发送正文。旧实现立即回复并关闭，新增线协议检查失败，见忽略目录 `webview-host/tests/results/feedback-rate-close-before.log`。该提前关闭路径与 CI 中未收到限流响应的断连现象相符。

## 代码证据

`server/reflex-feedback/receiver.py` 的 do_POST 限流分支在读取正文前返回；`test_boundaries.py` 的 test_rate_limited_split_body_is_consumed_before_closing。

## 根因

限流分支直接关闭仍可能有未读正文的连接，客户端发送正文与接收响应之间存在竞争。原并发测试未覆盖请求头与正文分开发送的时序。

## 修复方案

限流判定后释放全局锁，在既有读取超时内消费长度合法且不超过 MAX_BYTES 的正文，再返回 429。不解析、不落盘这些正文；不消费无上限输入，不修改限额与有效请求的保存语义。

## 验收标准与结果

修改前新增检查失败，修改后原接收端回归及 10 项边界检查通过（feedback-rate-close-after.log）。另执行 20 轮有界并发场景，每轮 16 请求、4 工作线程，均核对 6 个成功、10 个限流响应以及 6 条完整 JSON 记录；日志为 feedback-rate-concurrency.log。不以无变化重跑代替修复，不宣称所有网络故障均被穷举。
