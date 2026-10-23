#!/usr/bin/env python3
"""DeepSeek-Reflex feedback receiver: append-only JSONL on localhost:8787."""
import json
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

LOCK = threading.Lock()
RECENT = {}
WINDOW_SECONDS, LIMIT_PER_WINDOW = 3600.0, 6
MAX_BYTES = 4096
STORE = "/opt/reflex-feedback/feedback.jsonl"


class Handler(BaseHTTPRequestHandler):
    server_version = "ReflexFeedback/1.0"

    def _reply(self, code):
        self.send_response(code)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def do_POST(self):
        if self.path != "/feedback":
            self._reply(404)
            return
        ip = self.client_address[0]
        now = time.time()
        with LOCK:
            hits = [t for t in RECENT.get(ip, []) if now - t < WINDOW_SECONDS]
            if len(hits) >= LIMIT_PER_WINDOW:
                RECENT[ip] = hits
                self._reply(429)
                return
            hits.append(now)
            RECENT[ip] = hits
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            self._reply(400)
            return
        if length <= 0 or length > MAX_BYTES:
            self._reply(413 if length > MAX_BYTES else 400)
            return
        try:
            data = json.loads(self.rfile.read(length).decode("utf-8"))
            text = data.get("text")
            if not isinstance(text, str) or not text.strip():
                raise ValueError("empty text")
            entry = {
                "time": int(now),
                "ip": ip,
                "version": str(data.get("version", ""))[:32],
                "text": text[:1000],
            }
        except Exception:
            self._reply(400)
            return
        with LOCK:
            with open(STORE, "a", encoding="utf-8") as fh:
                fh.write(json.dumps(entry, ensure_ascii=False) + "\n")
        self._reply(204)

    def do_GET(self):
        self._reply(404)

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    ThreadingHTTPServer(("127.0.0.1", 8787), Handler).serve_forever()
