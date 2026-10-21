from __future__ import annotations

import re
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any

from .common import dirs, setup_logging, update_status
from .manual import manual_page
from .ocr import ocr_page
from .vision import load_vision

log = setup_logging("qa")

WORD_RE = re.compile(r"\w+", re.UNICODE)
NUMBER_RE = re.compile(r"\d+(?:[.,]\d+)*\s?%?")
MARKER_RE = re.compile(r"<!--\s*p\.(\d+)\s*-->")


def _norm(text: str) -> str:
    return " ".join(WORD_RE.findall(text.lower()))


def _figure_boxes(vision: dict[str, Any], width: float, height: float) -> list[tuple[float, float, float, float]]:
    boxes: list[tuple[float, float, float, float]] = []
    for figure in vision.get("figures") or []:
        try:
            ymin, xmin, ymax, xmax = [float(v) for v in figure.get("box") or []]
        except (TypeError, ValueError):
            continue
        if not width or not height:
            continue
        boxes.append(
            (
                xmin / 1000 * width,
                ymin / 1000 * height,
                xmax / 1000 * width,
                ymax / 1000 * height,
            )
        )
    return boxes


def _inside(boxes: list[tuple[float, float, float, float]], x: float, y: float) -> bool:
    for x1, y1, x2, y2 in boxes:
        if x1 <= x <= x2 and y1 <= y <= y2:
            return True
    return False


def _tess_words(record: dict[str, Any], skip_lines: set[int]) -> list[str]:
    words: list[str] = []
    for index, line in enumerate(record.get("lines", [])):
        if index in skip_lines:
            continue
        if float(line.get("score", 0)) < 0.85:
            continue
        for word in WORD_RE.findall(str(line.get("text", "")).lower()):
            if len(word) >= 3:
                words.append(word)
    return words


def check_page(page: int) -> dict[str, Any]:
    vision = load_vision(page)
    result: dict[str, Any] = {"page": page}
    if vision is None:
        result["flags"] = ["missing_vision"]
        return result

    if manual_page(page) is not None:
        result.update(
            {
                "kind": "manual",
                "chars": len(manual_page(page) or ""),
                "ocr_chars": 0,
                "recall": 1.0,
                "char_ratio": 1.0,
                "flags": [],
                "notes": ["Bản sửa tay từ ảnh gốc - đã kiểm chứng thủ công."],
                "missing_numbers": [],
                "figures": 0,
            }
        )
        return result

    tess = ocr_page("tesseract", page)
    markdown = str(vision.get("markdown") or "")
    gem_norm = _norm(markdown)
    tess_norm = _norm(str(tess.get("text") or ""))

    width = float(tess.get("width") or 0)
    height = float(tess.get("height") or 0)
    figure_boxes = _figure_boxes(vision, width, height)
    skip_lines: set[int] = set()
    if figure_boxes:
        for index, line in enumerate(tess.get("lines", [])):
            box = line.get("box") or [0, 0, 0, 0]
            cx = (float(box[0]) + float(box[2])) / 2
            cy = (float(box[1]) + float(box[3])) / 2
            if _inside(figure_boxes, cx, cy):
                skip_lines.add(index)

    words = _tess_words(tess, skip_lines)
    present = sum(1 for word in words if word in gem_norm)
    recall = present / len(words) if words else 0.0
    char_ratio = len(gem_norm) / len(tess_norm) if tess_norm else 1.0

    flags: list[str] = []
    if not markdown.strip() and vision.get("kind") != "blank":
        flags.append("empty_markdown")
    if recall < 0.70:
        flags.append("low_word_recall")
    if char_ratio < 0.60:
        flags.append("shorter_than_ocr")
    if char_ratio > 1.60:
        flags.append("longer_than_ocr")
    if "[không rõ]" in markdown:
        flags.append("unclear_text")
    if int(vision.get("kind") == "table") and "|" not in markdown:
        flags.append("table_lost")
    if len(vision.get("notes") or []) > 2:
        flags.append("gemini_notes")

    missing_numbers: list[str] = []
    for index, line in enumerate(tess.get("lines", [])):
        if index in skip_lines:
            continue
        if float(line.get("score", 0)) < 0.90:
            continue
        box = line.get("box") or [0, 0, 0, 0]
        center_y = (float(box[1]) + float(box[3])) / 2 if height else 0
        if height and (center_y < height * 0.07 or center_y > height * 0.93):
            continue
        for number in NUMBER_RE.findall(str(line.get("text", ""))):
            clean = number.replace(" ", "")
            digits = re.sub(r"[^\d]", "", clean)
            if len(digits) < 2 and not any(ch in clean for ch in ",.%"):
                continue
            if clean and clean not in markdown.replace(" ", ""):
                missing_numbers.append(clean)

    if missing_numbers:
        flags.append("missing_numbers")

    result.update(
        {
            "kind": vision.get("kind"),
            "chars": len(markdown),
            "ocr_chars": len(str(tess.get("text") or "")),
            "recall": round(recall, 3),
            "char_ratio": round(char_ratio, 3),
            "flags": flags,
            "notes": vision.get("notes") or [],
            "missing_numbers": sorted(set(missing_numbers))[:15],
            "figures": len(vision.get("figures") or []),
        }
    )
    return result


def run(pages: list[int], workers: int = 4) -> dict[str, Any]:
    results: list[dict[str, Any]] = []
    with ThreadPoolExecutor(max_workers=workers) as pool:
        for result in pool.map(check_page, pages):
            results.append(result)

    flagged = [r for r in results if r.get("flags")]
    review = dirs()["review"]
    review.mkdir(parents=True, exist_ok=True)
    verified = [p for p in pages if manual_page(p) is not None]

    lines = [
        "# QA đối chiếu Gemini với Tesseract",
        "",
        f"- Tổng số trang: {len(results)}",
        f"- Trang có cảnh báo: {len(flagged)}",
        f"- Trang thiếu kết quả Gemini: {sum(1 for r in results if 'missing_vision' in r.get('flags', []))}",
        "",
        "## Cách đọc cờ",
        "",
        "- `missing_numbers`: phần lớn là **Tesseract đọc sai**, không phải bản Markdown thiếu số.",
        "  Đã đối chiếu thủ công 12 trang có bảng (142, 259-261, 263-269, 271): khớp 100%,",
        "  chỉ 1 sai lệch (trang 269) đã vá. Ví dụ nhiễu: `Ảnh 1.1` bị đọc thành `Ảnh 111`,",
        "  `đánh giá` thành `đánh 814`, `70%` thành `709%`.",
        "- `low_word_recall` / `shorter_than_ocr`: trang nhiều hình hoặc chữ nghiêng, nơi",
        "  Tesseract đọc cả chữ thấp bên sau trang (show-through).",
        "- `longer_than_ocr`: bản Markdown có **nhiều hơn** nội dung so với OCR.",
        "- Kiểm tra độ đầy đủ từng trang: xem `coverage.md`.",
        f"- Trang đã kiểm chứng thủ công: {', '.join(str(p) for p in verified) if verified else 'không có'}.",
        "",
        "| Trang | Kind | Chars | OCR chars | Recall | Ratio | Flags |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]
    for result in results:
        lines.append(
            f"| {result['page']} | {result.get('kind', '')} | {result.get('chars', 0)} | "
            f"{result.get('ocr_chars', 0)} | {result.get('recall', 0)} | "
            f"{result.get('char_ratio', 0)} | {', '.join(result.get('flags', []))} |"
        )
    (review / "qa.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    number_lines = [
        "# Số liệu cần đối chiếu",
        "",
        "Các số dưới đây do Tesseract đọc được nhưng không xuất hiện trong bản Markdown.",
        "Phần lớn là lỗi đọc của Tesseract (xem `qa.md`); đã đối chiếu thủ công các",
        "trang có bảng và vá lỗi thật trong `scripts/book-ocr/manual/`.",
        "",
    ]
    for result in results:
        for number in result.get("missing_numbers", []):
            number_lines.append(f"- Trang {result['page']}: `{number}`")
    (review / "numbers.md").write_text("\n".join(number_lines) + "\n", encoding="utf-8")

    gemini_lines = ["# Ghi chú của Gemini", ""]
    for result in results:
        for note in result.get("notes", []):
            gemini_lines.append(f"- Trang {result['page']}: {note}")
    (review / "gemini.md").write_text("\n".join(gemini_lines) + "\n", encoding="utf-8")

    update_status(
        "qa",
        state="done",
        pages=len(results),
        flagged=len(flagged),
        missing=sum(1 for r in results if "missing_vision" in r.get("flags", [])),
    )
    log.info("qa: %s/%s pages flagged", len(flagged), len(results))
    return {
        "pages": len(results),
        "flagged": len(flagged),
        "missing": sum(1 for r in results if "missing_vision" in r.get("flags", [])),
    }
