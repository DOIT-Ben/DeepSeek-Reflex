# Reflex 反馈接收端（hk）

小窗「反馈」按钮的落地端：nginx（test.doitbenai.cloud，`location = /reflex/feedback`）→ `127.0.0.1:8787` → 追加写 `/opt/reflex-feedback/feedback.jsonl`。

- 限制：同一 TCP 对端每小时 6 条。`X-Real-IP` 只在它是单个合法 IP 时写入日志，不能用来换新的配额。nginx 反代时对端都是 `127.0.0.1`，访客级限流放在 nginx。请求体 ≤8192 字节，文本截断 1000 字，只存文本、版本、时间、类别、联系方式和 IP。日志达到 8 MiB 后返回 507，不再追加。
- 部署：`receiver.py` 放 `/opt/reflex-feedback/`，service 进 `/etc/systemd/system/`，然后 `systemctl enable --now reflex-feedback`。
- nginx 变更备份：`/etc/nginx/test-doitbenai.bak-reflex`。
