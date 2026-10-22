from __future__ import annotations

import io
import json
import os
import re
import threading
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image

from .common import (
    atomic_write_json,
    dirs,
    read_json,
    setup_logging,
    update_status,
    utc_now,
)
from .render import page_png

log = setup_logging("vision")

MAX_IMAGE_WIDTH = 1500
JPEG_QUALITY = 88
MAX_OUTPUT_TOKENS = 12288

PROMPT = """\
Bạn là công cụ số hoá sách tiếng Việt. Bạn nhận {count} ảnh chụp các trang sách, \
theo thứ tự: {pages}.

Với TỪNG trang, hãy:
1. Chép lại NGUYÊN VĂN toàn bộ nội dung chữ, đúng dấu tiếng Việt, đúng chính tả, \
đúng thứ tự đọc. Không tóm tắt, không viết lại, không thêm bình luận.
2. Giữ cấu trúc bằng Markdown: tiêu đề (#, ##, ###), đoạn văn, danh sách, bảng \
Markdown, chú thích. Giữ số mục, số trang tham chiếu và ký hiệu chú thích như ¹².
3. BỎ các dòng chạy đầu trang/chân trang lặp lại (tên sách, tên tác giả, số trang in). \
Không đưa chúng vào markdown.
4. Với hình ảnh, biểu đồ, sơ đồ (không phải bảng): chèn placeholder [[HINH:1]], \
[[HINH:2]]... tại đúng vị trí xuất hiện. Không mô tả nội dung hình.
5. Trả toạ độ bao của từng hình, chuẩn hoá 0-1000 theo [ymin, xmin, ymax, xmax] \
của ảnh trang tương ứng.
6. Chỗ nào thực sự không đọc được, ghi [không rõ] trong markdown và thêm ghi chú vào notes.
7. Trang trắng hoặc chỉ có ảnh bìa: markdown là chuỗi rỗng.

Trả về JSON đúng schema, mỗi ảnh một phần tử trong pages, page là số trang PDF đã \
ghi ở trên."""

SCHEMA: dict[str, Any] = {
    "type": "object",
    "properties": {
        "pages": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "page": {"type": "integer"},
                    "kind": {
                        "type": "string",
                        "enum": ["text", "table", "mixed", "figure", "blank"],
                    },
                    "markdown": {"type": "string"},
                    "figures": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "id": {"type": "integer"},
                                "box": {
                                    "type": "array",
                                    "items": {"type": "number"},
                                    "minItems": 4,
                                    "maxItems": 4,
                                },
                            },
                            "required": ["id", "box"],
                        },
                    },
                    "notes": {"type": "array", "items": {"type": "string"}},
                },
                "required": ["page", "markdown"],
            },
        }
    },
    "required": ["pages"],
}

_thread = threading.local()
_lock = threading.Lock()
_EXHAUSTED: set[str] = set()


def model_chain() -> list[str]:
    primary = os.environ.get("GEMINI_MODEL", "gemini-3.5-flash").strip()
    fallback = os.environ.get(
        "GEMINI_FALLBACK_MODELS",
        "gemini-3.5-flash-lite,gemini-3.6-flash,gemini-3.7-flash,gemini-3.8-flash",
    )
    models = [primary] + [m.strip() for m in fallback.split(",") if m.strip()]
    seen: list[str] = []
    for model in models:
        if model and model not in seen:
            seen.append(model)
    return seen


def _mark_exhausted(model: str, exc: Exception) -> None:
    with _lock:
        if model in _EXHAUSTED:
            return
        _EXHAUSTED.add(model)
        path = dirs()["review"] / "quota.md"
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("a", encoding="utf-8") as fh:
            fh.write(f"## {utc_now()} - {model}\n\n```\n{exc}\n```\n\n")
    log.warning("model %s marked exhausted for this run", model)


def _client() -> Any:
    from google import genai
    from google.genai import types

    client = getattr(_thread, "client", None)
    if client is None:
        key = os.environ.get("GOOGLE_AI__API_KEY") or os.environ.get("GEMINI_API_KEY")
        if not key:
            raise RuntimeError("GOOGLE_AI__API_KEY is not set")
        client = genai.Client(
            api_key=key,
            http_options=types.HttpOptions(timeout=180_000),
        )
        _thread.client = client
    return client


def _image_part(page: int) -> Any:
    from google.genai import types

    img = Image.open(page_png(page)).convert("RGB")
    if img.width > MAX_IMAGE_WIDTH:
        img = img.resize(
            (MAX_IMAGE_WIDTH, round(img.height * MAX_IMAGE_WIDTH / img.width)),
            Image.LANCZOS,
        )
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=JPEG_QUALITY, optimize=True)
    return types.Part.from_bytes(data=buf.getvalue(), mime_type="image/jpeg")


def _strip_fences(text: str) -> str:
    text = text.strip()
    if text.startswith("```"):
        text = re.sub(r"^```[a-zA-Z]*\s*", "", text)
        text = re.sub(r"\s*```$", "", text)
    return text.strip()


def _parse(text: str, pages: list[int]) -> list[dict[str, Any]]:
    data = json.loads(_strip_fences(text))
    items = data.get("pages") if isinstance(data, dict) else None
    if not isinstance(items, list):
        raise ValueError("response has no pages array")

    by_page: dict[int, dict[str, Any]] = {}
    for item in items:
        if not isinstance(item, dict):
            continue
        try:
            page = int(item.get("page"))
        except (TypeError, ValueError):
            continue
        if page not in pages:
            continue
        markdown = _strip_fences(str(item.get("markdown") or ""))
        figures = []
        for figure in item.get("figures") or []:
            try:
                box = [float(v) for v in figure.get("box") or []]
                if len(box) != 4:
                    continue
                figures.append({"id": int(figure.get("id", len(figures) + 1)), "box": box})
            except (TypeError, ValueError):
                continue
        by_page[page] = {
            "page": page,
            "kind": str(item.get("kind") or "text"),
            "markdown": markdown,
            "figures": figures,
            "notes": [str(n) for n in (item.get("notes") or [])],
        }

    missing = [p for p in pages if p not in by_page]
    if missing:
        raise ValueError(f"response missing pages: {missing}")
    return [by_page[p] for p in pages]


def _is_quota_error(exc: Exception) -> bool:
    text = str(exc).lower()
    return "429" in text or "resource_exhausted" in text or "quota" in text


def _sleep_backoff(attempt: int, quota: bool) -> None:
    delay = (10, 25, 45, 60)[min(attempt, 3)]
    if quota:
        delay = max(delay, 25)
    time.sleep(delay)


def _call(pages: list[int]) -> list[dict[str, Any]]:
    from google.genai import types

    prompt = PROMPT.format(
        count=len(pages),
        pages=", ".join(f"trang PDF {p} (ảnh thứ {i + 1})" for i, p in enumerate(pages)),
    )
    contents: list[Any] = [prompt]
    contents.extend(_image_part(p) for p in pages)
    config = types.GenerateContentConfig(
        response_mime_type="application/json",
        response_schema=SCHEMA,
        temperature=0.1,
        max_output_tokens=MAX_OUTPUT_TOKENS,
    )

    last_error: Exception | None = None
    available = [m for m in model_chain() if m not in _EXHAUSTED]
    if not available:
        raise RuntimeError(f"all models quota exhausted: {sorted(_EXHAUSTED)}")

    for model in available:
        for attempt in range(3):
            try:
                response = _client().models.generate_content(
                    model=model, contents=contents, config=config
                )
                if not response.text:
                    raise ValueError("empty response")
                records = _parse(response.text, pages)
                usage = getattr(response, "usage_metadata", None)
                tokens = {
                    "prompt": int(getattr(usage, "prompt_token_count", 0) or 0),
                    "output": int(getattr(usage, "candidates_token_count", 0) or 0),
                    "thoughts": int(getattr(usage, "thoughts_token_count", 0) or 0),
                }
                for record in records:
                    record["model"] = model
                    record["tokens"] = tokens
                return records
            except Exception as exc:  # noqa: BLE001 - provider errors are messy
                last_error = exc
                quota = _is_quota_error(exc)
                parse_error = isinstance(exc, (ValueError, json.JSONDecodeError))
                log.warning(
                    "model %s attempt %s failed for pages %s: %s",
                    model,
                    attempt + 1,
                    pages,
                    str(exc).splitlines()[0][:160],
                )
                if quota:
                    _mark_exhausted(model, exc)
                    break
                if parse_error:
                    if attempt == 0:
                        time.sleep(5)
                        continue
                    break
                if attempt < 2:
                    _sleep_backoff(attempt, False)
                    continue
                break
    raise RuntimeError(f"all models failed for pages {pages}: {last_error}")


RECOVERY_PROMPT = """\
Dưới đây là văn bản thô do OCR tự động đọc từ một trang sách tiếng Việt. \
Văn bản có lỗi nhận dạng ký tự (thiếu dấu, nhầm chữ), sai từ, và thiếu cấu trúc.

Hãy sửa lỗi OCR và khôi phục cấu trúc Markdown (tiêu đề #, ##, ###, đoạn văn, \
danh sách, bảng) cho đúng với bản in. Giữ nguyên nội dung và thứ tự, không tóm tắt, \
không thêm thông tin mới. Chỗ không chắc thì giữ nguyên như OCR đọc được.

Trả về JSON theo schema: một phần tử trong "pages", page = {page}, markdown = văn bản, \
figures = [], notes = các chỗ bạn không chắc."""

RECOVERY_SCHEMA: dict[str, Any] = {
    "type": "object",
    "properties": {
        "pages": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "page": {"type": "integer"},
                    "markdown": {"type": "string"},
                    "figures": {"type": "array"},
                    "notes": {"type": "array", "items": {"type": "string"}},
                },
                "required": ["page", "markdown"],
            },
        }
    },
    "required": ["pages"],
}


def recover_from_ocr(page: int) -> dict[str, Any] | None:
    """Rebuild a page Gemini refused to transcribe (recitation block).

    Tesseract supplies the raw text, Gemini only repairs the OCR mistakes and
    the structure, and the raw text is kept as a last resort.
    """
    from google.genai import types

    from .ocr import ocr_page

    try:
        tess = ocr_page("tesseract", page)
    except Exception as exc:  # noqa: BLE001
        log.error("recovery page %s: tesseract failed: %s", page, exc)
        return None

    raw = str(tess.get("text") or "").strip()
    if not raw:
        log.warning("recovery page %s: tesseract returned nothing", page)
        return None

    prompt = RECOVERY_PROMPT.format(page=page)
    for model in model_chain():
        if model in _EXHAUSTED:
            continue
        try:
            response = _client().models.generate_content(
                model=model,
                contents=[prompt, raw],
                config=types.GenerateContentConfig(
                    response_mime_type="application/json",
                    response_schema=SCHEMA,
                    temperature=0.1,
                    max_output_tokens=MAX_OUTPUT_TOKENS,
                ),
            )
            if not response.text:
                raise ValueError("empty response")
            records = _parse(response.text, [page])
            record = records[0]
            record["model"] = f"{model}+ocr-recovery"
            record["notes"] = list(record.get("notes") or []) + [
                "Trang này bị Gemini chặn RECITATION khi đọc ảnh; nội dung được "
                "khôi phục từ OCR Tesseract rồi sửa lỗi bằng model. Cần đối chiếu bản in."
            ]
            _save([record])
            log.info("recovered page %s via %s", page, model)
            return record
        except Exception as exc:  # noqa: BLE001
            log.warning("recovery page %s via %s failed: %s", page, model, exc)

    record = {
        "page": page,
        "kind": "text",
        "markdown": raw,
        "figures": [],
        "notes": [
            "Không sửa được bằng model; giữ nguyên OCR Tesseract. Cần đối chiếu bản in."
        ],
        "model": "tesseract-raw",
        "tokens": {"prompt": 0, "output": 0, "thoughts": 0},
    }
    _save([record])
    log.warning("page %s kept raw Tesseract text", page)
    return record


def vision_path(page: int) -> Path:
    return dirs()["vision"] / f"p{page:04d}.json"


def load_vision(page: int) -> dict[str, Any] | None:
    data = read_json(vision_path(page))
    if isinstance(data, dict) and (data.get("markdown") or data.get("kind") == "blank"):
        return data
    return None


def _save(records: list[dict[str, Any]]) -> None:
    for record in records:
        payload = dict(record)
        payload["created_at"] = utc_now()
        atomic_write_json(vision_path(record["page"]), payload)


def run_vision(
    pages: list[int],
    force: bool = False,
    workers: int | None = None,
    batch_size: int | None = None,
) -> dict[str, Any]:
    workers = workers or int(os.environ.get("VISION_WORKERS", "2"))
    batch_size = batch_size or int(os.environ.get("VISION_BATCH_SIZE", "2"))

    missing = [p for p in pages if force or load_vision(p) is None]
    done = len(pages) - len(missing)
    stats = {"total": len(pages), "done": done, "failed": 0, "paused": False, "tokens": 0}
    if not missing:
        update_status("vision", state="done", **stats)
        return stats

    log.info(
        "vision: %s pages to do (%s cached), workers=%s batch=%s",
        len(missing),
        done,
        workers,
        batch_size,
    )
    update_status("vision", state="running", total=len(pages), done=done, failed=0)

    batches = [missing[i : i + batch_size] for i in range(0, len(missing), batch_size)]
    completed = done
    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = {pool.submit(_call, batch): batch for batch in batches}
        for future in as_completed(futures):
            batch = futures[future]
            try:
                records = future.result()
            except Exception as exc:  # noqa: BLE001
                stats["failed"] += len(batch)
                log.error("batch %s failed: %s", batch, exc)
                (dirs()["review"]).mkdir(parents=True, exist_ok=True)
                with (dirs()["review"] / "vision_failures.md").open("a", encoding="utf-8") as fh:
                    fh.write(f"- pages {batch}: {exc}\n")
                if _is_quota_error(exc):
                    stats["paused"] = True
                    update_status(
                        "vision",
                        state="quota_paused",
                        total=len(pages),
                        done=completed,
                        failed=stats["failed"],
                    )
                continue

            _save(records)
            completed += len(records)
            stats["done"] = completed
            stats["tokens"] += sum(r.get("tokens", {}).get("prompt", 0) for r in records)
            stats["tokens"] += sum(r.get("tokens", {}).get("output", 0) for r in records)
            log.info(
                "vision %s/%s pages (batch %s, model %s)",
                completed,
                len(pages),
                batch,
                records[0].get("model"),
            )
            update_status("vision", state="running", total=len(pages), done=completed)

    state = "quota_paused" if stats["paused"] else ("done" if stats["failed"] == 0 else "partial")
    update_status("vision", state=state, **stats)
    return stats


def merge_status(page: int, data: dict[str, Any]) -> None:
    with _lock:
        atomic_write_json(vision_path(page), data)


def collect_gaps(pages: list[int]) -> list[int]:
    return [p for p in pages if load_vision(p) is None]

