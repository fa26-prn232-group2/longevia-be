from __future__ import annotations

import json
import os
import re
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from typing import Any

from .common import atomic_write_json, dirs, read_json, setup_logging, update_status, utc_now
from .vision import _client, _image_part, _is_quota_error, _mark_exhausted, model_chain

log = setup_logging("coverage")

WORD_RE = re.compile(r"\w+", re.UNICODE)
SUPERSCRIPT = str.maketrans("⁰¹²³⁴⁵⁶⁷⁸⁹ⁱ", "0123456789i")

PROMPT = """\
Bạn đang kiểm tra CHẤT LƯỢNG một bản số hoá sách, KHÔNG chép lại sách.

Với mỗi ảnh trang sách tiếng Việt, hãy mô tả CẤU TRÚC của trang:
1. "paragraphs": danh sách các đoạn văn. Với mỗi đoạn chỉ ghi "head" (tối đa 8 từ đầu) \
và "tail" (tối đa 8 từ cuối). KHÔNG ghi toàn bộ nội dung đoạn.
2. "headings": các tiêu đề có trong trang (nguyên văn tiêu đề).
3. "has_table": trang có bảng không.
4. "has_figure": trang có hình ảnh, biểu đồ không.

Tuyệt đối không chép lại nguyên văn các đoạn văn dài - chỉ 8 từ đầu và 8 từ cuối."""

SHORT_PROMPT = """\
Với mỗi ảnh trang sách, chỉ liệt kê cấu trúc: các tiêu đề trong trang và 5 từ đầu \
của mỗi đoạn văn. Không chép lại nội dung dài."""

SCHEMA: dict[str, Any] = {
    "type": "object",
    "properties": {
        "pages": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "page": {"type": "integer"},
                    "paragraphs": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "head": {"type": "string"},
                                "tail": {"type": "string"},
                            },
                            "required": ["head"],
                        },
                    },
                    "headings": {"type": "array", "items": {"type": "string"}},
                    "has_table": {"type": "boolean"},
                    "has_figure": {"type": "boolean"},
                },
                "required": ["page", "paragraphs"],
            },
        }
    },
    "required": ["pages"],
}


def _norm(text: str) -> str:
    return " ".join(WORD_RE.findall(text.lower().translate(SUPERSCRIPT)))


def _head_key(head: str) -> str:
    """First words of a paragraph, ignoring a leading footnote marker."""
    words = _norm(head).split()
    while words and words[0] in {"i", "ii", "iii", "iv", "v"}:
        words = words[1:]
    return " ".join(words[:5])


def _blocks(markdown: str) -> list[str]:
    blocks: list[str] = []
    for raw in re.split(r"\n\s*\n", markdown):
        text = raw.strip()
        if not text:
            continue
        if text.startswith("#") or text.startswith("|"):
            continue
        blocks.append(text)
    return blocks


def coverage_path(page: int) -> Path:
    return dirs()["root"] / "coverage" / f"p{page:04d}.json"


def load_coverage(page: int) -> dict[str, Any] | None:
    data = read_json(coverage_path(page))
    return data if isinstance(data, dict) else None


def _parse(text: str, pages: list[int]) -> list[dict[str, Any]]:
    data = json.loads(text)
    items = data.get("pages") if isinstance(data, dict) else None
    if not isinstance(items, list):
        raise ValueError("no pages array")

    def to_record(item: dict[str, Any], page: int) -> dict[str, Any]:
        return {
            "page": page,
            "paragraphs": [
                {
                    "head": str(p.get("head") or ""),
                    "tail": str(p.get("tail") or ""),
                }
                for p in (item.get("paragraphs") or [])
                if isinstance(p, dict)
            ],
            "headings": [str(h) for h in (item.get("headings") or [])],
            "has_table": bool(item.get("has_table")),
            "has_figure": bool(item.get("has_figure")),
        }

    out: list[dict[str, Any]] = []
    by_page: dict[int, dict[str, Any]] = {}
    for item in items:
        if not isinstance(item, dict):
            continue
        try:
            page = int(item.get("page"))
        except (TypeError, ValueError):
            continue
        if page in pages:
            by_page[page] = to_record(item, page)

    # Fallback: the model sometimes omits the page field; map by position.
    if len(by_page) != len(pages) and len(items) == len(pages):
        positional = [it for it in items if isinstance(it, dict)]
        by_page = {page: to_record(item, page) for page, item in zip(pages, positional)}

    for page in pages:
        if page in by_page:
            out.append(by_page[page])
    missing = [p for p in pages if p not in {item["page"] for item in out}]
    if missing:
        raise ValueError(f"response missing pages: {missing}")
    return out


def _call(pages: list[int]) -> list[dict[str, Any]]:
    from google.genai import types

    config = types.GenerateContentConfig(
        response_mime_type="application/json",
        response_schema=SCHEMA,
        temperature=0.1,
        max_output_tokens=8192,
    )
    contents: list[Any] = [PROMPT]
    contents.extend(_image_part(p) for p in pages)

    last_error: Exception | None = None
    for model in model_chain():
        try:
            response = _client().models.generate_content(
                model=model, contents=contents, config=config
            )
            if not response.text:
                raise ValueError("empty response")
            return _parse(response.text, pages)
        except Exception as exc:  # noqa: BLE001
            last_error = exc
            if _is_quota_error(exc):
                _mark_exhausted(model, exc)
                break
            log.warning("coverage %s failed on %s: %s", pages, model, exc)
            time.sleep(3)

    log.warning("coverage %s: retry with short prompt", pages)
    short_contents: list[Any] = [SHORT_PROMPT]
    short_contents.extend(_image_part(p) for p in pages)
    for model in model_chain():
        try:
            response = _client().models.generate_content(
                model=model,
                contents=short_contents,
                config=types.GenerateContentConfig(
                    response_mime_type="application/json",
                    response_schema=SCHEMA,
                    temperature=0.1,
                    max_output_tokens=8192,
                ),
            )
            if not response.text:
                raise ValueError("empty response")
            return _parse(response.text, pages)
        except Exception as exc:  # noqa: BLE001
            last_error = exc
            if _is_quota_error(exc):
                _mark_exhausted(model, exc)
                break

    raise RuntimeError(f"coverage failed for {pages}: {last_error}")


def run_coverage(pages: list[int], workers: int = 2, batch_size: int = 2) -> dict[str, Any]:
    missing = [p for p in pages if load_coverage(p) is None]
    done = len(pages) - len(missing)
    log.info("coverage: %s pages to do (%s cached)", len(missing), done)
    failures: list[int] = []

    if missing:
        batches = [missing[i : i + batch_size] for i in range(0, len(missing), batch_size)]
        failed_pages: list[int] = []
        with ThreadPoolExecutor(max_workers=workers) as pool:
            futures = {pool.submit(_call, batch): batch for batch in batches}
            for future in as_completed(futures):
                batch = futures[future]
                try:
                    records = future.result()
                except Exception as exc:  # noqa: BLE001
                    failed_pages.extend(batch)
                    log.error("coverage batch %s failed: %s", batch, exc)
                    continue
                for record in records:
                    record["created_at"] = utc_now()
                    atomic_write_json(coverage_path(record["page"]), record)
                done += len(records)
                log.info("coverage %s/%s pages", done, len(pages))
                update_status("coverage", state="running", total=len(pages), done=done)

        # Second pass: retry the failed pages one by one.
        for page in failed_pages:
            try:
                records = _call([page])
            except Exception as exc:  # noqa: BLE001
                log.error("coverage page %s failed: %s", page, exc)
                failures.append(page)
                continue
            for record in records:
                record["created_at"] = utc_now()
                atomic_write_json(coverage_path(record["page"]), record)
            done += len(records)
            log.info("coverage (retry) %s/%s pages", done, len(pages))

    return compare(pages, failures)


def compare(pages: list[int], failures: list[int] | None = None) -> dict[str, Any]:
    from .manual import manual_page
    from .vision import load_vision

    failures = failures or []
    rows: list[dict[str, Any]] = []
    for page in pages:
        cov = load_coverage(page)
        if cov is None:
            rows.append(
                {
                    "page": page,
                    "expected": 0,
                    "missing": -1,
                    "missing_heads": [],
                    "note": "coverage failed" if page in failures else "no coverage data",
                }
            )
            continue
        if manual_page(page) is not None:
            rows.append(
                {
                    "page": page,
                    "expected": len(cov.get("paragraphs") or []),
                    "missing": 0,
                    "missing_heads": [],
                    "note": "manual",
                }
            )
            continue

        vision = load_vision(page)
        markdown = str((vision or {}).get("markdown") or "")
        text = _norm(markdown)
        blocks = [_norm(b) for b in _blocks(markdown)]
        missing_heads: list[str] = []
        for paragraph in cov.get("paragraphs") or []:
            head = _norm(str(paragraph.get("head") or ""))
            if not head:
                continue
            key = _head_key(str(paragraph.get("head") or ""))
            if not key:
                continue
            if key in text:
                continue
            if any(block.startswith(key) for block in blocks):
                continue
            missing_heads.append(str(paragraph.get("head") or ""))

        rows.append(
            {
                "page": page,
                "expected": len(cov.get("paragraphs") or []),
                "missing": len(missing_heads),
                "missing_heads": missing_heads,
                "note": "",
            }
        )

    review = dirs()["review"]
    review.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Kiểm tra độ đầy đủ trang",
        "",
        "So số đoạn văn mà model nhìn thấy trong ảnh với bản Markdown đã tạo.",
        "`missing` = số đoạn không tìm thấy trong bản Markdown.",
        "",
        "| Trang | Đoạn trong ảnh | Thiếu | Ghi chú |",
        "| --- | --- | --- | --- |",
    ]
    for row in rows:
        lines.append(
            f"| {row['page']} | {row['expected']} | {row['missing']} | {row['note']} |"
        )
    lines.extend(["", "## Trang có đoạn bị thiếu", ""])
    flagged = [r for r in rows if r["missing"] > 0]
    if not flagged:
        lines.append("Không trang nào bị thiếu đoạn.")
    for row in flagged:
        lines.append(f"- Trang {row['page']} (thiếu {row['missing']}):")
        for head in row["missing_heads"]:
            lines.append(f"  - {head}")
    (review / "coverage.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    update_status(
        "coverage",
        state="done",
        pages=len(rows),
        flagged=len(flagged),
        failed=len(failures),
    )
    log.info("coverage: %s pages flagged", len(flagged))
    return {"pages": len(rows), "flagged": len(flagged), "failed": len(failures)}
