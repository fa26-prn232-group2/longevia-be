from __future__ import annotations

import re
from pathlib import Path
from typing import Any

import cv2
import pytesseract
from PIL import Image

from .common import atomic_write_json, dirs, page_id, page_png, read_json, setup_logging

log = setup_logging("ocr")

VIETNAMESE_CHARS = set(
    "ăâđêôơưàáảãạằắẳẵặầấẩẫậèéẻẽẹềếểễệ"
    "ìíỉĩịòóỏõọồốổỗộờớởỡợùúủũụừứửữựỳýỷỹỵ"
    "ĂÂĐÊÔƠƯÀÁẢÃẠẰẮẲẴẶẦẤẨẪẬÈÉẺẼẸỀẾỂỄỆ"
    "ÌÍỈĨỊÒÓỎÕỌỒỐỔỖỘỜỚỞỠỢÙÚỦŨỤỪỨỬỮỰỲÝỶỸỴ"
)


def _line(text: str, score: Any, box: Any) -> dict[str, Any]:
    return {
        "text": str(text).strip(),
        "score": round(float(score), 4),
        "box": [float(v) for v in box],
    }


def _from_tesseract(path: Path) -> list[dict[str, Any]]:
    with Image.open(path) as img:
        data = pytesseract.image_to_data(
            img,
            lang="vie",
            config="--oem 1 --psm 3 -c preserve_interword_spaces=1",
            output_type=pytesseract.Output.DICT,
        )

    grouped: dict[tuple[int, int, int], list[tuple[str, float, tuple[int, int, int, int]]]] = {}
    for i, text in enumerate(data["text"]):
        text = (text or "").strip()
        if not text:
            continue
        try:
            conf = float(data["conf"][i])
        except (TypeError, ValueError):
            conf = -1.0
        if conf < 0:
            continue
        key = (data["block_num"][i], data["par_num"][i], data["line_num"][i])
        box = (data["left"][i], data["top"][i], data["width"][i], data["height"][i])
        grouped.setdefault(key, []).append((text, conf, box))

    lines: list[dict[str, Any]] = []
    for key in sorted(grouped):
        words = grouped[key]
        text = " ".join(w[0] for w in words)
        score = sum(w[1] for w in words) / len(words) / 100.0
        x1 = min(w[2][0] for w in words)
        y1 = min(w[2][1] for w in words)
        x2 = max(w[2][0] + w[2][2] for w in words)
        y2 = max(w[2][1] + w[2][3] for w in words)
        lines.append(_line(text, score, [x1, y1, x2, y2]))
    return lines


def metrics(lines: list[dict[str, Any]], text: str) -> dict[str, Any]:
    letters = [c for c in text if c.isalpha()]
    viet = [c for c in text if c in VIETNAMESE_CHARS]
    scores = [l["score"] for l in lines]
    return {
        "lines": len(lines),
        "chars": len(text),
        "words": len(text.split()),
        "mean_score": round(sum(scores) / len(scores), 4) if scores else 0.0,
        "min_score": round(min(scores), 4) if scores else 0.0,
        "vietnamese_ratio": round(len(viet) / len(letters), 4) if letters else 0.0,
        "replacement_chars": text.count("\ufffd"),
        "suspicious_chars": len(re.findall(r"[\x00-\x08\x0b\x0c\x0e-\x1f]", text)),
    }


def ocr_page(engine: str, number: int, force: bool = False) -> dict[str, Any]:
    if engine != "tesseract":
        raise ValueError(f"unsupported engine: {engine}")
    out = dirs()["ocr"] / engine / f"{page_id(number)}.json"
    if out.exists() and not force:
        cached = read_json(out)
        if isinstance(cached, dict) and cached.get("text"):
            return cached

    png = page_png(number)
    if not png.exists():
        raise FileNotFoundError(f"missing rendered page: {png}")

    lines = _from_tesseract(png)
    text = "\n".join(l["text"] for l in lines)
    image = cv2.imread(str(png), cv2.IMREAD_GRAYSCALE)
    height, width = (image.shape[:2] if image is not None else (0, 0))
    record = {
        "page": number,
        "engine": engine,
        "width": int(width),
        "height": int(height),
        "text": text,
        "lines": lines,
        "metrics": metrics(lines, text),
    }
    atomic_write_json(out, record)
    return record
