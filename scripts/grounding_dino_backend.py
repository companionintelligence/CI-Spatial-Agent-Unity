#!/usr/bin/env python3
import io
from typing import List, Dict

try:
    import torch
    from PIL import Image
except Exception:  # pragma: no cover
    torch = None
    Image = None


class GroundingDinoBackend:
    def __init__(self, model_config: str, model_weights: str, device: str = "cpu"):
        if torch is None or Image is None:
            raise RuntimeError("Missing dependencies. Install torch + pillow + groundingdino.")

        from groundingdino.util.inference import load_model, predict

        self._predict = predict
        self._model = load_model(model_config, model_weights, device=device)
        self._device = device

    def detect(
        self,
        image_bytes: bytes,
        prompt: str,
        width: int,
        height: int,
        box_threshold: float,
        text_threshold: float,
    ) -> List[Dict]:
        if Image is None:
            return []
        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        if width <= 0 or height <= 0:
            width, height = image.size

        boxes, logits, phrases = self._predict(
            model=self._model,
            image=image,
            caption=prompt,
            box_threshold=box_threshold,
            text_threshold=text_threshold,
        )

        detections = []
        for box, score, label in zip(boxes, logits, phrases):
            x0, y0, x1, y1 = _to_xyxy_pixels(box, width, height)
            detections.append(
                {
                    "label": label,
                    "confidence": float(score),
                    "bbox": [x0, y0, x1 - x0, y1 - y0],
                }
            )
        return detections


def _to_xyxy_pixels(box, width: int, height: int):
    # Accept torch tensor or list. GroundingDINO returns cxcywh normalized.
    if hasattr(box, "tolist"):
        vals = box.tolist()
    else:
        vals = list(box)

    if len(vals) != 4:
        return 0, 0, 0, 0

    x0, y0, x1, y1 = vals
    # Heuristic: if values are in [0,1], treat as normalized cxcywh
    if max(vals) <= 1.5:
        cx, cy, w, h = vals
        x0 = (cx - w / 2.0) * width
        x1 = (cx + w / 2.0) * width
        y0 = (cy - h / 2.0) * height
        y1 = (cy + h / 2.0) * height
    return float(x0), float(y0), float(x1), float(y1)
