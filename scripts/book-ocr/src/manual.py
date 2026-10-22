from __future__ import annotations

import json
import os
from pathlib import Path

from .common import page_id, setup_logging

log = setup_logging("manual")


def manual_dir() -> Path:
    return Path(os.environ.get("BOOK_OCR_MANUAL", "/manual"))


def manual_page_path(page: int) -> Path:
    return manual_dir() / f"{page_id(page)}.md"


def manual_patches_path(page: int) -> Path:
    return manual_dir() / f"{page_id(page)}.patches.json"


def manual_page(page: int) -> str | None:
    path = manual_page_path(page)
    try:
        text = path.read_text(encoding="utf-8").strip()
    except OSError:
        return None
    return text or None


def manual_patches(page: int) -> list[dict[str, str]]:
    path = manual_patches_path(page)
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return []
    if isinstance(data, dict):
        data = data.get("patches") or []
    if not isinstance(data, list):
        return []
    return [p for p in data if isinstance(p, dict) and p.get("find")]


def apply_patches(markdown: str, page: int) -> tuple[str, list[str]]:
    """Apply local patches to a page. Returns the new text and a list of problems."""
    problems: list[str] = []
    text = markdown
    for patch in manual_patches(page):
        find = str(patch.get("find") or "")
        replace = str(patch.get("replace") or "")
        note = str(patch.get("note") or "")
        if not find:
            problems.append(f"- Trang {page}: patch thiếu trường 'find'.")
            continue
        count = text.count(find)
        if count == 1:
            text = text.replace(find, replace, 1)
            log.info("page %s: patch applied (%s)", page, note or find[:40])
        elif count == 0:
            problems.append(f"- Trang {page}: patch không khớp: {find[:60]!r} ({note})")
        else:
            problems.append(
                f"- Trang {page}: patch khớp {count} lần, bỏ qua: {find[:60]!r} ({note})"
            )
    return text, problems


def manual_pages() -> list[int]:
    directory = manual_dir()
    if not directory.exists():
        return []
    pages: list[int] = []
    for path in sorted(directory.glob("p*.md")):
        stem = path.stem[1:]
        if stem.isdigit():
            pages.append(int(stem))
    return pages
