#!/usr/bin/env python3
"""DeepSeek-Reflex feedback receiver: append-only JSONL on localhost:8787."""
import ipaddress
import json
import os
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

LOCK = threading.Lock()
RECENT = {}
WINDOW_SECONDS, LIMIT_PER_WINDOW = 3600.0, 6
MAX_BYTES = 8192
MAX_STORE_BYTES = 8 * 1024 * 1024
MAX_RECENT_KEYS = 256
STORE = os.environ.get("REFLEX_FEEDBACK_STORE", "/opt/reflex-feedback/feedback.jsonl")


def valid_ip(value):
    if not isinstance(value, str) or not value or len(value) > 64:
        return False
    try:
        ipaddress.ip_address(value)
    except ValueError:
        return False
    return True


def logged_ip(peer, forwarded):
    """Keep a client address for the log only when it is one IP literal.

    The rate-limit key is the TCP peer. X-Real-IP is chosen by the caller, so it
    cannot open a fresh quota bucket. Behind nginx every peer is 127.0.0.1;
    per-visitor limits belong in nginx.
    """
    return forwarded if valid_ip(forwarded) else (peer or "unknown")


def _fresh(hits, now):
    return [item for item in hits if now - item < WINDOW_SECONDS]


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
        peer = self.client_address[0] if self.client_address else ""
        ip = logged_ip(peer, self.headers.get("X-Real-IP"))
        key = peer or "unknown"
        now = time.time()
        with LOCK:
            stale = [item for item, hits in RECENT.items() if not _fresh(hits, now)]
            for item in stale:
                del RECENT[item]
            hits = _fresh(RECENT.get(key, []), now)
            if key not in RECENT and len(RECENT) >= MAX_RECENT_KEYS:
                self._reply(429)
                return
            if len(hits) >= LIMIT_PER_WINDOW:
                RECENT[key] = hits
                self._reply(429)
                return
            hits.append(now)
            RECENT[key] = hits
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
            category = data.get("category")
            if isinstance(category, str) and category.strip():
                entry["category"] = category.strip()[:16]
            contact = data.get("contact")
            if isinstance(contact, str) and contact.strip():
                entry["contact"] = contact.strip()[:100]
        except Exception:
            self._reply(400)
            return
        with LOCK:
            try:
                size = os.path.getsize(STORE)
            except OSError:
                size = 0
            if size >= MAX_STORE_BYTES:
                self._reply(507)
                return
            with open(STORE, "a", encoding="utf-8") as fh:
                fh.write(json.dumps(entry, ensure_ascii=False) + "\n")
        self._reply(204)

    def do_GET(self):
        self._reply(404)

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    ThreadingHTTPServer(("127.0.0.1", 8787), Handler).serve_forever()
