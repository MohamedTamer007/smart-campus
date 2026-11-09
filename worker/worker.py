"""Private loopback encoder. No paths, URLs, gallery records or credentials accepted."""
import argparse
import hmac
import io
import json
import math
import os
import threading
import shutil
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MODEL = "google/siglip2-base-patch16-224"
REVISION = "75de2d55ec2d0b4efc50b3e9ad70dba96a7b2fa2"
MAX_BYTES = 5 * 1024 * 1024
BUSY = threading.Lock()


class Encoder:
    def __init__(self):
        cache = Path(os.environ.setdefault("HF_HOME", str(Path.cwd() / "work" / "model-cache")))
        cache.mkdir(parents=True, exist_ok=True)
        if shutil.disk_usage(cache).free < 4 * 1024**3:
            raise RuntimeError("At least 4 GiB free disk is required before loading/downloading model assets")
        import torch
        from transformers import AutoModel, AutoProcessor
        torch.set_num_threads(2)
        self.torch = torch
        self.processor = AutoProcessor.from_pretrained(MODEL, revision=REVISION, trust_remote_code=False, use_fast=False)
        self.model = AutoModel.from_pretrained(MODEL, revision=REVISION, trust_remote_code=False, use_safetensors=True).eval()

    def embed(self, route, payload):
        from PIL import Image, ImageOps
        if route == "/embed/text":
            document = json.loads(payload)
            if not isinstance(document, dict) or set(document) != {"text"}:
                raise ValueError("invalid_text")
            text = document["text"]
            if not isinstance(text, str) or not text.strip() or len(text) > 4000:
                raise ValueError("invalid_text")
            inputs = self.processor(text=[text], padding="max_length", truncation=True, return_tensors="pt")
            with self.torch.inference_mode():
                embedding = self.model.get_text_features(**inputs, return_dict=True).pooler_output
        elif route == "/embed/image":
            with Image.open(io.BytesIO(payload)) as source:
                if source.format not in {"JPEG", "PNG", "WEBP"} or source.width * source.height > 16_000_000:
                    raise ValueError("invalid_image")
                source.load()
                image = ImageOps.exif_transpose(source).convert("RGB")
                inputs = self.processor(images=image, return_tensors="pt")
            with self.torch.inference_mode():
                embedding = self.model.get_image_features(**inputs, return_dict=True).pooler_output
        else:
            raise ValueError("invalid_route")
        normalized = self.torch.nn.functional.normalize(embedding, p=2, dim=-1)[0].tolist()
        if not normalized or any(not math.isfinite(number) for number in normalized):
            raise ValueError("invalid_embedding")
        return {"model": MODEL, "revision": REVISION, "dimension": len(normalized), "vector": normalized}


def handler(encoder, key):
    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def setup(self):
            super().setup()
            self.connection.settimeout(15)

        def log_message(self, *_):
            pass

        def reply(self, status, document):
            encoded = json.dumps(document, allow_nan=False).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(encoded)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(encoded)
            self.close_connection = True

        def do_GET(self):
            if self.path != "/health":
                self.reply(404, {"code": "not_found"})
            else:
                self.reply(200, {"model": MODEL, "revision": REVISION, "ready": True})

        def do_POST(self):
            if not hmac.compare_digest(self.headers.get("X-Worker-Key", ""), key):
                self.reply(403, {"code": "forbidden"})
                return
            if self.path not in {"/embed/text", "/embed/image"}:
                self.reply(404, {"code": "not_found"})
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length <= 0 or length > MAX_BYTES or self.headers.get("Transfer-Encoding"):
                    raise ValueError("invalid_size")
                if not BUSY.acquire(blocking=False):
                    self.reply(503, {"code": "worker_busy"})
                    return
                try:
                    payload = self.rfile.read(length)
                    if len(payload) != length:
                        raise ValueError("incomplete_request")
                    response = encoder.embed(self.path, payload)
                finally:
                    BUSY.release()
                self.reply(200, response)
            except (ValueError, TypeError, KeyError, OSError, UnicodeError):
                self.reply(400, {"code": "invalid_input"})
            except RuntimeError:
                self.reply(503, {"code": "worker_unavailable"})
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8123)
    args = parser.parse_args()
    key = os.environ.get("SMART_CAMPUS_WORKER_KEY", "")
    if len(key) < 32:
        parser.error("SMART_CAMPUS_WORKER_KEY must contain at least 32 characters")
    encoder = Encoder()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), handler(encoder, key))
    server.daemon_threads = True
    server.serve_forever()


if __name__ == "__main__":
    main()
