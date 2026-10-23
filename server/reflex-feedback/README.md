# Reflex 反馈接收端（hk）

小窗「反馈」按钮的落地端：nginx（test.doitbenai.cloud，`location = /reflex/feedback`）→ `127.0.0.1:8787` → 追加写 `/opt/reflex-feedback/feedback.jsonl`。

- 限制：单 IP 每小时 6 条、请求体 ≤4KB、文本截断 1000 字、只存文本+版本+时间+IP。
- 部署：`receiver.py` 放 `/opt/reflex-feedback/`，service 进 `/etc/systemd/system/`，然后 `systemctl enable --now reflex-feedback`。
- nginx 变更备份：`/etc/nginx/test-doitbenai.bak-reflex`。
