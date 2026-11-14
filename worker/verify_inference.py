"""Real pinned-model smoke verification using generated, non-sensitive input only."""
import http.client
from http.server import ThreadingHTTPServer
import io
import json
import math
import threading
import time
from PIL import Image
from worker import Encoder, MODEL, REVISION, handler


def main():
    started = time.perf_counter()
    encoder = Encoder()
    loaded = time.perf_counter()
    server = ThreadingHTTPServer(("127.0.0.1", 0), handler(encoder, "synthetic-ci-worker-key-with-at-least-32-characters"))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()

    def request(route, payload):
        client = http.client.HTTPConnection("127.0.0.1", server.server_port, timeout=60)
        client.request("POST", route, payload, {"X-Worker-Key": "synthetic-ci-worker-key-with-at-least-32-characters"})
        response = client.getresponse()
        result = json.loads(response.read())
        client.close()
        if response.status != 200 or result.get("model") != MODEL or result.get("revision") != REVISION:
            raise RuntimeError("Real worker HTTP/model contract failed")
        vector = result.get("vector", [])
        if len(vector) < 2 or len(vector) != result.get("dimension") or any(not math.isfinite(x) for x in vector) or \
           abs(sum(x * x for x in vector) - 1) > 0.001:
            raise RuntimeError("Real embedding dimension/norm validation failed")
        return vector

    try:
        english = request("/embed/text", json.dumps({"text": "A blue water bottle with a white cap"}).encode())
        arabic = request("/embed/text", json.dumps({"text": "زجاجة مياه زرقاء ذات غطاء أبيض"}, ensure_ascii=False).encode())
        images = []
        for color in ("blue", "red"):
            data = io.BytesIO()
            Image.new("RGB", (224, 224), color).save(data, format="PNG")
            images.append(request("/embed/image", data.getvalue()))
        if len({len(vector) for vector in (english, arabic, *images)}) != 1:
            raise RuntimeError("Text and image dimensions are incompatible")
        if english == arabic or images[0] == images[1]:
            raise RuntimeError("Distinct inputs did not affect real embeddings")
        print(json.dumps({"result": "real_worker_smoke_passed", "model": MODEL, "revision": REVISION,
                          "dimension": len(english), "loadSeconds": round(loaded - started, 3),
                          "fourRequestsSeconds": round(time.perf_counter() - loaded, 3),
                          "verified": ["English text", "Arabic text", "two generated images", "HTTP transport", "unit vectors", "compatible dimensions"],
                          "limits": "Synthetic smoke inputs only; this does not establish retrieval calibration or ownership accuracy"}))
    finally:
        server.shutdown()
        server.server_close()
        thread.join()


if __name__ == "__main__":
    main()
