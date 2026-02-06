#!/usr/bin/env python3
import argparse
import base64
import io
import json
import os
from http.server import BaseHTTPRequestHandler, HTTPServer

try:
    from PIL import Image
except Exception:
    Image = None

DUMMY_DETECTIONS = os.environ.get("ZERO_SHOT_DUMMY", "1") == "1"


def get_image_size(image_bytes: bytes):
    if Image is None:
        return 0, 0
    try:
        with Image.open(io.BytesIO(image_bytes)) as img:
            return img.width, img.height
    except Exception:
        return 0, 0


def build_dummy_detections(prompt: str, width: int, height: int):
    if width <= 0 or height <= 0:
        return []
    # center box
    w = width * 0.25
    h = height * 0.35
    x = (width - w) * 0.5
    y = (height - h) * 0.5
    label = prompt if prompt else "object"
    return [
        {
            "label": label,
            "confidence": 0.42,
            "bbox": [x, y, w, h],
        }
    ]


class Handler(BaseHTTPRequestHandler):
    def _set_headers(self, code=200):
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.end_headers()

    def do_POST(self):
        if self.path != "/detect":
            self._set_headers(404)
            self.wfile.write(json.dumps({"error": "Not found"}).encode("utf-8"))
            return

        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length)
        try:
            payload = json.loads(raw)
        except Exception:
            self._set_headers(400)
            self.wfile.write(json.dumps({"error": "Invalid JSON"}).encode("utf-8"))
            return

        image_b64 = payload.get("image_base64", "")
        prompt = payload.get("prompt", "")
        image_bytes = b""
        if image_b64:
            try:
                image_bytes = base64.b64decode(image_b64)
            except Exception:
                image_bytes = b""

        width, height = get_image_size(image_bytes)
        detections = []
        if DUMMY_DETECTIONS:
            detections = build_dummy_detections(prompt, width, height)

        response = {
            "frame_width": width,
            "frame_height": height,
            "detections": detections,
        }
        self._set_headers(200)
        self.wfile.write(json.dumps(response).encode("utf-8"))

    def log_message(self, format, *args):
        # quiet
        return


def main():
    parser = argparse.ArgumentParser(description="Zero-shot detection stub server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()

    server = HTTPServer((args.host, args.port), Handler)
    print(f"Zero-shot server listening on http://{args.host}:{args.port}/detect (dummy={DUMMY_DETECTIONS})")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
