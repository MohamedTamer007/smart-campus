import http.client
import json
import threading
import unittest
from worker import BUSY, Encoder, handler
from http.server import ThreadingHTTPServer


class FakeModelBoundary:
    def embed(self, route, payload):
        return {"model": "test-double", "dimension": 2, "vector": [1.0, 0.0]}


class WorkerBoundaryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.key = "test-worker-key-with-at-least-32-characters"
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), handler(FakeModelBoundary(), cls.key))
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join()

    def request(self, route, payload=b"{}", headers=None):
        client = http.client.HTTPConnection("127.0.0.1", self.server.server_port, timeout=3)
        supplied = {"X-Worker-Key": self.key}
        supplied.update(headers or {})
        client.request("POST", route, payload, supplied)
        response = client.getresponse()
        status, body = response.status, json.loads(response.read())
        client.close()
        return status, body

    def test_missing_worker_key_is_forbidden(self):
        self.assertEqual(self.request("/embed/text", headers={"X-Worker-Key": "wrong"})[0], 403)

    def test_paths_and_urls_are_not_api_routes(self):
        self.assertEqual(self.request("/embed/path")[0], 404)

    def test_busy_worker_returns_unavailable_without_inference(self):
        BUSY.acquire()
        try:
            self.assertEqual(self.request("/embed/text")[0], 503)
        finally:
            BUSY.release()

    def test_oversized_request_is_rejected_before_reading(self):
        self.assertEqual(self.request("/embed/image", headers={"Content-Length": "6000000"})[0], 400)

    def test_http_contract_carries_model_vectors(self):
        status, body = self.request("/embed/text", b'{"text":"test"}')
        self.assertEqual(status, 200)
        self.assertEqual(body["vector"], [1.0, 0.0])

    def test_invalid_text_is_rejected_before_model_access(self):
        encoder = Encoder.__new__(Encoder)
        for payload in [b"{}", b"[]", b'{"text":null}', b'{"text":" "}', b'{"text":"x","path":"secret"}', b"not json"]:
            with self.subTest(payload=payload), self.assertRaises((ValueError, TypeError)):
                encoder.embed("/embed/text", payload)


if __name__ == "__main__":
    unittest.main()
