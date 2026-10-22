from __future__ import annotations

import os
import shutil
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any

import pymupdf as fitz

from .assemble import build as build_markdown
from .common import (
    dirs,
    input_pdf,
    parse_pages,
    setup_logging,
    update_status,
    utc_now,
)
from .ocr import ocr_page
from .qa import run as run_qa
from .render import mark_pages_current, page_is_current, render_page
from .vision import collect_gaps, run_vision

log = setup_logging("night")


def _render_pages(doc: fitz.Document, pages: list[int], force: bool) -> None:
    for page in pages:
        render_page(doc, page, force=force)
    mark_pages_current(pages)


def _tesseract_all(pages: list[int], workers: int = 4) -> None:
    def one(page: int) -> bool:
        try:
            ocr_page("tesseract", page)
            return True
        except Exception as exc:  # noqa: BLE001
            log.error("tesseract page %s failed: %s", page, exc)
            return False

    with ThreadPoolExecutor(max_workers=workers) as pool:
        ok = sum(1 for result in pool.map(one, pages) if result)
    log.info("tesseract: %s/%s pages", ok, len(pages))


def run(
    pages_spec: str = "all",
    workers: int | None = None,
    batch: int | None = None,
    skip_qa: bool = False,
) -> dict[str, Any]:
    pdf = input_pdf()
    if not pdf.exists():
        raise SystemExit(f"input PDF not found: {pdf}")

    with fitz.open(pdf) as doc:
        total = doc.page_count
        pages = parse_pages(pages_spec, total)
        if not pages:
            raise SystemExit("no pages selected")
        dirs()["meta"].mkdir(parents=True, exist_ok=True)
        (dirs()["meta"] / "page_count.txt").write_text(str(total), encoding="utf-8")
        stale = [p for p in pages if not page_is_current(p)]
        if stale:
            log.info("render: %s pages are stale (version changed)", len(stale))
            _render_pages(doc, stale, force=True)
            for page in stale:
                (dirs()["ocr"] / "tesseract" / f"p{page:04d}.json").unlink(missing_ok=True)
                (dirs()["vision"] / f"p{page:04d}.json").unlink(missing_ok=True)

    update_status("night", state="running", started_at=utc_now(), pages=len(pages), total=total)
    log.info("night start: %s/%s pages", len(pages), total)

    vision_stats = run_vision(
        pages,
        workers=workers,
        batch_size=batch,
    )
    gaps = collect_gaps(pages)
    if gaps:
        log.warning("vision gaps: %s pages (e.g. %s)", len(gaps), gaps[:10])

    _tesseract_all(pages)
    assembled = build_markdown(pages)
    qa_stats = {"skipped": True} if skip_qa else run_qa(pages)

    state = "quota_paused" if vision_stats.get("paused") else (
        "done" if not gaps else "partial"
    )
    summary = {
        "state": state,
        "pages": len(pages),
        "vision_done": vision_stats.get("done", 0),
        "vision_failed": vision_stats.get("failed", 0),
        "gaps": len(gaps),
        "chapters": assembled.get("chapters", 0),
        "qa": qa_stats,
        "finished_at": utc_now(),
    }
    update_status("night", **summary)
    log.info("night finished: %s", summary)
    return summary

