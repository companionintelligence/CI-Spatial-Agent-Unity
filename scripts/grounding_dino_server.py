#!/usr/bin/env python3
import argparse
import base64
import io
import json
import os
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import Optional

try:
    from PIL import Image
except Exception:
    Image = None

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.append(SCRIPT_DIR)

DUMMY_DETECTIONS = os.environ.get("ZERO_SHOT_DUMMY", "1") == "1"
BACKEND = os.environ.get("ZERO_SHOT_BACKEND", "dummy").lower()
FORWARD_URL = os.environ.get("GROUNDING_DINO_URL", "")
MODEL_CONFIG = os.environ.get("GROUNDING_DINO_CONFIG", "")
MODEL_WEIGHTS = os.environ.get("GROUNDING_DINO_WEIGHTS", "")
DEVICE = os.environ.get("GROUNDING_DINO_DEVICE", "cpu")

_gdin_backend = None


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


def build_xyxy(detections):
    boxes = []
    scores = []
    labels = []
    for det in detections:
        x, y, w, h = det.get("bbox", [0, 0, 0, 0])
        boxes.append([x, y, x + w, y + h])
        scores.append(det.get("confidence", 0.0))
        labels.append(det.get("label", "object"))
    return boxes, scores, labels


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
        box_threshold = float(payload.get("threshold", 0.3))
        text_threshold = float(payload.get("text_threshold", 0.25))
        image_bytes = b""
        if image_b64:
            try:
                image_bytes = base64.b64decode(image_b64)
            except Exception:
                image_bytes = b""

        width, height = get_image_size(image_bytes)
        detections = []
        if BACKEND == "forward" and FORWARD_URL:
            detections = forward_request(image_b64, prompt, box_threshold, text_threshold)
        elif BACKEND == "groundingdino" and not DUMMY_DETECTIONS:
            detections = grounding_dino_detect(image_bytes, prompt, width, height, box_threshold, text_threshold)
        elif DUMMY_DETECTIONS:
            detections = build_dummy_detections(prompt, width, height)

        response = {
            "frame_width": width,
            "frame_height": height,
            "detections": detections,
        }
        boxes, scores, labels = build_xyxy(detections)
        response["boxes"] = boxes
        response["scores"] = scores
        response["labels"] = labels
        response["prompt"] = prompt
        self._set_headers(200)
        self.wfile.write(json.dumps(response).encode("utf-8"))

    def log_message(self, format, *args):
        # quiet
        return

    def do_GET(self):
        if self.path == "/health":
            self._set_headers(200)
            self.wfile.write(json.dumps({"ok": True, "dummy": DUMMY_DETECTIONS}).encode("utf-8"))
            return
        self._set_headers(404)
        self.wfile.write(json.dumps({"error": "Not found"}).encode("utf-8"))


def main():
    parser = argparse.ArgumentParser(description="Zero-shot detection stub server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    parser.add_argument("--backend", default=BACKEND, choices=["dummy", "groundingdino", "forward"])
    parser.add_argument("--forward-url", default=FORWARD_URL)
    parser.add_argument("--model-config", default=MODEL_CONFIG)
    parser.add_argument("--model-weights", default=MODEL_WEIGHTS)
    parser.add_argument("--device", default=DEVICE)
    args = parser.parse_args()

    global BACKEND, FORWARD_URL, MODEL_CONFIG, MODEL_WEIGHTS, DEVICE
    BACKEND = args.backend
    FORWARD_URL = args.forward_url
    MODEL_CONFIG = args.model_config
    MODEL_WEIGHTS = args.model_weights
    DEVICE = args.device

    server = HTTPServer((args.host, args.port), Handler)
    print(
        f"Zero-shot server listening on http://{args.host}:{args.port}/detect "
        f"(backend={BACKEND} dummy={DUMMY_DETECTIONS} device={DEVICE})"
    )
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


def forward_request(image_b64: str, prompt: str, box_threshold: float, text_threshold: float):
    try:
        import requests
    except Exception:
        return []

    if not FORWARD_URL:
        return []

    payload = {
        "image_base64": image_b64,
        "prompt": prompt,
        "threshold": box_threshold,
        "text_threshold": text_threshold,
    }
    try:
        resp = requests.post(FORWARD_URL, json=payload, timeout=20)
        if resp.status_code != 200:
            return []
        data = resp.json()
        return data.get("detections", [])
    except Exception:
        return []


def grounding_dino_detect(
    image_bytes: bytes,
    prompt: str,
    width: int,
    height: int,
    box_threshold: float,
    text_threshold: float,
):
    global _gdin_backend
    if image_bytes is None or len(image_bytes) == 0:
        return []

    if _gdin_backend is None:
        _gdin_backend = init_grounding_dino()
        if _gdin_backend is None:
            return []

    try:
        return _gdin_backend.detect(image_bytes, prompt, width, height, box_threshold, text_threshold)
    except Exception:
        return []


def init_grounding_dino():
    if not MODEL_CONFIG or not MODEL_WEIGHTS:
        return None
    try:
        from grounding_dino_backend import GroundingDinoBackend
    except Exception:
        return None

    try:
        return GroundingDinoBackend(
            model_config=MODEL_CONFIG,
            model_weights=MODEL_WEIGHTS,
            device=DEVICE,
        )
    except Exception:
        return None


if __name__ == "__main__":
    main()
