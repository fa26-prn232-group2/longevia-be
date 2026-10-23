from __future__ import annotations

import json
import math
import re
from pathlib import Path

import cv2
import pymupdf as fitz
import numpy as np
import pytesseract
from PIL import Image

from .common import dirs, page_png

TARGET_WIDTH = 2000
MAX_ZOOM = 2.0
MIN_DESKEW = 0.3
MAX_DESKEW = 8.0
RENDER_VERSION = 3
ORIENTATION_CONFIDENCE = 0.5


def page_count(pdf: Path) -> int:
    with fitz.open(pdf) as doc:
        return doc.page_count


def render_version() -> int:
    return RENDER_VERSION


def _versions_path() -> Path:
    return dirs()["pages"] / ".versions.json"


def _versions() -> dict[str, int]:
    try:
        data = json.loads(_versions_path().read_text(encoding="utf-8"))
        return {str(k): int(v) for k, v in data.items()}
    except (OSError, ValueError, TypeError):
        return {}


def page_is_current(number: int) -> bool:
    return _versions().get(str(number)) == RENDER_VERSION and page_png(number).exists()


def mark_pages_current(pages: list[int]) -> None:
    versions = _versions()
    for page in pages:
        versions[str(page)] = RENDER_VERSION
    _versions_path().write_text(json.dumps(versions), encoding="utf-8")


def render_page(doc: fitz.Document, number: int, force: bool = False) -> Path:
    out = page_png(number)
    if out.exists() and not force:
        return out

    page = doc[number - 1]
    zoom = min(MAX_ZOOM, TARGET_WIDTH / max(1.0, page.rect.width))
    pix = page.get_pixmap(
        matrix=fitz.Matrix(zoom, zoom), colorspace=fitz.csGRAY, alpha=False
    )
    buf = np.frombuffer(pix.samples, dtype=np.uint8).reshape(
        pix.height, pix.stride
    )[:, : pix.width]
    img = buf.copy()

    img = trim_white_margin(img)
    img = enhance(img)
    img = fix_orientation(img)
    img = deskew(img)

    out.parent.mkdir(parents=True, exist_ok=True)
    ok = cv2.imwrite(str(out), img, [cv2.IMWRITE_PNG_COMPRESSION, 3])
    if not ok:
        raise RuntimeError(f"cannot write {out}")
    return out


def fix_orientation(gray: np.ndarray) -> np.ndarray:
    """Rotate sideways scans upright using Tesseract OSD.

    OSD is cheap and reliable on text pages; it is skipped when confidence is
    low (cover images, nearly blank pages), leaving the page untouched.
    """
    height, width = gray.shape[:2]
    scale = min(1.0, 1600.0 / max(1, width))
    small = cv2.resize(gray, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
    try:
        osd = pytesseract.image_to_osd(Image.fromarray(small), config="--psm 0")
        rotate = int(re.search(r"Rotate:\s*(\d+)", osd).group(1))
        confidence = float(re.search(r"Orientation confidence:\s*([\d.]+)", osd).group(1))
    except (pytesseract.TesseractError, AttributeError, ValueError):
        return gray

    if confidence < ORIENTATION_CONFIDENCE:
        return gray
    if rotate == 90:
        return cv2.rotate(gray, cv2.ROTATE_90_CLOCKWISE)
    if rotate == 180:
        return cv2.rotate(gray, cv2.ROTATE_180)
    if rotate == 270:
        return cv2.rotate(gray, cv2.ROTATE_90_COUNTERCLOCKWISE)
    return gray


def sharpen_page(number: int, amount: float = 1.6) -> Path:
    """Boost local contrast and edges on a page that came out out of focus."""
    path = page_png(number)
    gray = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
    if gray is None:
        raise FileNotFoundError(path)
    blurred = cv2.GaussianBlur(gray, (0, 0), 3)
    sharp = cv2.addWeighted(gray, 1 + amount, blurred, -amount, 0)
    cv2.imwrite(str(path), sharp, [cv2.IMWRITE_PNG_COMPRESSION, 3])
    return path


def deskew(gray: np.ndarray) -> np.ndarray:
    small = cv2.resize(gray, None, fx=0.25, fy=0.25, interpolation=cv2.INTER_AREA)
    blur = cv2.GaussianBlur(small, (3, 3), 0)
    _, mask = cv2.threshold(blur, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    if cv2.countNonZero(mask) < 500:
        return gray

    coords = cv2.findNonZero(mask)
    angle = cv2.minAreaRect(coords)[-1]
    if angle > 45:
        angle -= 90
    if abs(angle) < MIN_DESKEW or abs(angle) > MAX_DESKEW:
        return gray

    h, w = gray.shape[:2]
    center = (w / 2.0, h / 2.0)
    matrix = cv2.getRotationMatrix2D(center, angle, 1.0)
    return cv2.warpAffine(
        gray,
        matrix,
        (w, h),
        flags=cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_CONSTANT,
        borderValue=255,
    )


def trim_white_margin(gray: np.ndarray, pad: int = 8) -> np.ndarray:
    h, w = gray.shape[:2]
    ink = gray < 235
    rows = np.flatnonzero(ink.any(axis=1))
    cols = np.flatnonzero(ink.any(axis=0))
    if rows.size == 0 or cols.size == 0:
        return gray

    top, bottom = int(rows[0]), int(rows[-1])
    left, right = int(cols[0]), int(cols[-1])
    if top < h * 0.015 and left < w * 0.015 and bottom > h * 0.985 and right > w * 0.985:
        return gray

    top = max(0, top - pad)
    left = max(0, left - pad)
    bottom = min(h, bottom + pad + 1)
    right = min(w, right + pad + 1)
    if (bottom - top) < h * 0.5 or (right - left) < w * 0.5:
        return gray
    return gray[top:bottom, left:right]


def enhance(gray: np.ndarray) -> np.ndarray:
    """Remove page shine and bleed-through, then lift local contrast.

    Background division is what makes Tesseract usable on the photographed
    pages; it keeps the foreground strokes and flattens the show-through from
    the reverse side. Blank pages are left untouched to avoid amplifying noise.
    """
    ink_ratio = float(np.count_nonzero(gray < 200)) / gray.size
    if ink_ratio < 0.002:
        return gray

    background = cv2.GaussianBlur(gray, (0, 0), 25)
    normalized = cv2.divide(gray, background, scale=255)
    return cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8)).apply(normalized)


def skew_note(gray: np.ndarray) -> float:
    small = cv2.resize(gray, None, fx=0.25, fy=0.25, interpolation=cv2.INTER_AREA)
    _, mask = cv2.threshold(small, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    coords = cv2.findNonZero(mask)
    if coords is None:
        return 0.0
    angle = cv2.minAreaRect(coords)[-1]
    if angle > 45:
        angle -= 90
    return float(angle) if not math.isnan(angle) else 0.0

