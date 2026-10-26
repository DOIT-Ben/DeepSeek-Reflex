"""Local checks for the feedback receiver. Does not bind the production port."""
import json
import os
import threading
import urllib.error
import urllib.request

import receiver


def post(port, body, headers=None):
    data = body if isinstance(body, bytes) else body.encode("utf-8")
    request = urllib.request.Request(
        "http://127.0.0.1:%d/feedback" % port,
        data=data,
        headers=headers or {},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code


def main():
    store = os.path.join(os.environ["TEMP"], "reflex-feedback-test.jsonl")
    if os.path.exists(store):
        os.remove(store)
    receiver.STORE = store
    receiver.RECENT.clear()
    receiver.MAX_STORE_BYTES = 8 * 1024 * 1024
    httpd = receiver.ThreadingHTTPServer(("127.0.0.1", 0), receiver.Handler)
    thread = threading.Thread(target=httpd.serve_forever, daemon=True)
    thread.start()
    port = httpd.server_address[1]
    body = json.dumps({"text": "hello", "version": "1.0.18"}).encode("utf-8")
    try:
        codes = [post(port, body, {"X-Real-IP": "203.0.113.%d" % item}) for item in range(7)]
        assert codes[:6] == [204] * 6, codes
        assert codes[6] == 429, codes
        assert receiver.valid_ip("203.0.113.8") and not receiver.valid_ip("not-an-ip")
        assert receiver.logged_ip("127.0.0.1", "203.0.113.8") == "203.0.113.8"
        assert receiver.logged_ip("127.0.0.1", "spoofed") == "127.0.0.1"
        receiver.RECENT.clear()
        receiver.MAX_STORE_BYTES = 1
        with open(store, "a", encoding="utf-8") as handle:
            handle.write("{}\n")
        assert post(port, body, {"X-Real-IP": "198.51.100.9"}) == 507
        print("PASS feedback receiver peer limit, ip literal and store cap")
    finally:
        httpd.shutdown()
        if os.path.exists(store):
            os.remove(store)


if __name__ == "__main__":
    main()
