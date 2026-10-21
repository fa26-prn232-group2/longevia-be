from __future__ import annotations

import json
import logging
import os
import sys
from datetime import datetime, timezone
from pathlib import Path


def env_path(name: str, default: str) -> Path:
    return Path(os.environ.get(name, default))


def input_pdf() -> Path:
    return env_path("BOOK_OCR_INPUT", "/input/book.pdf")


def work_root() -> Path:
    return env_path("BOOK_OCR_WORK", "/work")


def models_dir() -> Path:
    return env_path("BOOK_OCR_MODELS", "/models")


def dirs() -> dict[str, Path]:
    root = work_root()
    layout = {
        "root": root,
        "pages": root / "pages",
        "ocr": root / "ocr",
        "vision": root / "vision",
        "draft": root / "draft",
        "draft_pages": root / "draft" / "pages",
        "chapters": root / "draft" / "chapters",
        "out": root / "out",
        "images": root / "images",
        "review": root / "review",
        "logs": root / "logs",
        "sample": root / "sample",
        "meta": root / "meta",
    }
    for path in layout.values():
        path.mkdir(parents=True, exist_ok=True)
    return layout


def page_id(number: int) -> str:
    return f"p{number:04d}"


def page_png(number: int) -> Path:
    return dirs()["pages"] / f"{page_id(number)}.png"


def parse_pages(spec: str, total: int) -> list[int]:
    spec = (spec or "all").strip().lower()
    if spec in {"all", "*", ""}:
        return list(range(1, total + 1))
    pages: set[int] = set()
    for chunk in spec.split(","):
        chunk = chunk.strip()
        if not chunk:
            continue
        if "-" in chunk:
            start, end = chunk.split("-", 1)
            lo, hi = int(start), int(end)
            pages.update(range(lo, hi + 1))
        else:
            pages.add(int(chunk))
    return sorted(p for p in pages if 1 <= p <= total)


def atomic_write_json(path: Path, data: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def read_json(path: Path) -> object | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def setup_logging(name: str) -> logging.Logger:
    logger = logging.getLogger(name)
    if logger.handlers:
        return logger
    logger.setLevel(logging.INFO)
    fmt = logging.Formatter("%(asctime)s %(levelname)s %(message)s", "%H:%M:%S")
    stream = logging.StreamHandler(sys.stdout)
    stream.setFormatter(fmt)
    logger.addHandler(stream)
    try:
        log_dir = dirs()["logs"]
        file_handler = logging.FileHandler(log_dir / f"{name}.log", encoding="utf-8")
        file_handler.setFormatter(fmt)
        logger.addHandler(file_handler)
    except OSError:
        pass
    return logger


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def update_status(stage: str, **fields: object) -> None:
    path = dirs()["root"] / "status.json"
    status = read_json(path)
    if not isinstance(status, dict):
        status = {}
    status.setdefault("stages", {})
    stage_state = status["stages"].get(stage, {})
    stage_state.update(fields)
    stage_state["updated_at"] = utc_now()
    status["stages"][stage] = stage_state
    status["updated_at"] = utc_now()
    atomic_write_json(path, status)
