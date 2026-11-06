"""Bounded HTTP/storage regression fixtures; never use the live store or port."""
import http.client
import json
import os
import socket
import tempfile
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from unittest.mock import patch

import receiver


class Boundaries(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="reflex-boundary-")
        self.store = os.path.join(self.temp.name, "feedback.jsonl")
        self.saved = (receiver.STORE, receiver.MAX_STORE_BYTES)
        receiver.STORE, receiver.MAX_STORE_BYTES = self.store, 8 * 1024 * 1024
        receiver.RECENT.clear()
        self.server = receiver.ThreadingHTTPServer(("127.0.0.1", 0), receiver.Handler)
        self.server.handle_error = lambda *_args: None
        self.thread = threading.Thread(target=self.server.serve_forever, kwargs={"poll_interval": 0.02}, daemon=True)
        self.thread.start()
        self.port = self.server.server_address[1]

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(2)
        receiver.STORE, receiver.MAX_STORE_BYTES = self.saved
        receiver.RECENT.clear()
        self.temp.cleanup()

    def post(self, value, headers=None, path="/feedback"):
        body = value if isinstance(value, bytes) else json.dumps(value).encode("utf-8")
        connection = http.client.HTTPConnection("127.0.0.1", self.port, timeout=2)
        try:
            connection.request("POST", path, body, headers or {})
            response = connection.getresponse()
            response.read()
            return response.status
        finally:
            connection.close()

    def test_exact_store_cap_cannot_be_crossed_by_one_append(self):
        receiver.MAX_STORE_BYTES = 10
        self.assertEqual(self.post({"text": "valid"}), 507)
        self.assertLessEqual(os.path.getsize(self.store) if os.path.exists(self.store) else 0, 10)

    def test_unpaired_unicode_is_bad_input_and_never_breaks_the_connection(self):
        self.assertEqual(self.post(b'{"text":"\\ud800"}'), 400)
        self.assertEqual(self.post({"text": "正常😀"}), 204)

    def test_storage_failure_returns_an_http_error(self):
        receiver.STORE = self.temp.name
        self.assertEqual(self.post({"text": "cannot write"}), 503)

    def test_structural_invalid_input_does_not_create_a_record(self):
        for value in (None, [], "plain", {"text": " "}, {"text": 1}):
            receiver.RECENT.clear()
            self.assertEqual(self.post(value), 400)
        self.assertFalse(os.path.exists(self.store))

    def test_byte_limit_and_invalid_utf8(self):
        self.assertEqual(self.post(b"x" * 8193), 413)
        self.assertEqual(self.post(b'{"text":"\xff"}'), 400)

    def test_size_boundary_and_legacy_truncation_are_explicit(self):
        self.assertEqual(self.post({"text": "字" * 1001, "contact": "x" * 101}), 204)
        with open(self.store, encoding="utf-8") as stream:
            entry = json.loads(stream.readline())
        self.assertEqual(len(entry["text"]), 1000)
        self.assertEqual(len(entry["contact"]), 100)

    def test_parallel_posts_obey_quota_and_keep_whole_json_records(self):
        with ThreadPoolExecutor(max_workers=4) as pool:
            statuses = list(pool.map(lambda _index: self.post({"text": "parallel"}), range(16)))
        self.assertEqual(statuses.count(204), 6)
        self.assertEqual(statuses.count(429), 10)
        with open(self.store, encoding="utf-8") as stream:
            self.assertEqual(len([json.loads(line) for line in stream]), 6)

    def test_wrong_route_and_invalid_length(self):
        self.assertEqual(self.post({"text": "x"}, path="/unknown"), 404)
        self.assertEqual(self.post(b"", {"Content-Length": "-1"}), 400)

    def test_incomplete_body_expires_instead_of_holding_a_worker(self):
        with patch.object(receiver, "READ_TIMEOUT_SECONDS", 0.05, create=True):
            with socket.create_connection(("127.0.0.1", self.port), timeout=1) as connection:
                connection.sendall(b"POST /feedback HTTP/1.0\r\nContent-Length: 10\r\n\r\n{")
                self.assertIn(b"408", connection.recv(1024).split(b"\r\n")[0])


if __name__ == "__main__":
    unittest.main()
