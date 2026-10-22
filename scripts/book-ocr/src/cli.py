from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

import pymupdf as fitz
from tqdm import tqdm

from .common import (
    dirs,
    input_pdf,
    parse_pages,
    read_json,
    setup_logging,
    update_status,
)
from .ocr import ocr_page
from .render import mark_pages_current, page_is_current, render_page

log = setup_logging("cli")


def _doc_and_total() -> tuple[fitz.Document, int]:
    pdf = input_pdf()
    if not pdf.exists():
        raise SystemExit(f"input PDF not found: {pdf}")
    doc = fitz.open(pdf)
    return doc, doc.page_count


def _ensure_page_count(total: int) -> None:
    dirs()["meta"].mkdir(parents=True, exist_ok=True)
    (dirs()["meta"] / "page_count.txt").write_text(str(total), encoding="utf-8")


def cmd_info(_: argparse.Namespace) -> int:
    doc, total = _doc_and_total()
    first = doc[0]
    print(
        json.dumps(
            {
                "input": str(input_pdf()),
                "pages": total,
                "page0_pt": [round(first.rect.width, 1), round(first.rect.height, 1)],
                "work": str(dirs()["root"]),
                "render_version": page_is_current(1),
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0


def cmd_render(args: argparse.Namespace) -> int:
    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    for number in tqdm(pages, desc="render", unit="page"):
        render_page(doc, number, force=args.force or not page_is_current(number))
    mark_pages_current(pages)
    update_status("render", state="done", pages=len(pages))
    return 0


def cmd_vision(args: argparse.Namespace) -> int:
    from .vision import run_vision

    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    stale = [number for number in pages if not page_is_current(number)]
    if stale:
        for number in tqdm(stale, desc="render", unit="page"):
            render_page(doc, number, force=True)
        mark_pages_current(stale)
    stats = run_vision(pages, force=args.force, workers=args.workers, batch_size=args.batch)
    print(json.dumps(stats, ensure_ascii=False, indent=2))
    return 0 if not stats.get("failed") else 1


def cmd_ocr(args: argparse.Namespace) -> int:
    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    ok = failed = 0
    for number in tqdm(pages, desc="ocr:tesseract", unit="page"):
        try:
            render_page(doc, number)
            ocr_page("tesseract", number, force=args.force)
            ok += 1
        except Exception as exc:  # noqa: BLE001 - one bad page must not stop the run
            failed += 1
            log.error("page %s failed: %s", number, exc)
    update_status("ocr_tesseract", state="done" if failed == 0 else "partial", pages=len(pages), ok=ok, failed=failed)
    return 0 if failed == 0 else 1


def cmd_assemble(args: argparse.Namespace) -> int:
    from .assemble import build

    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    result = build(pages)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def cmd_qa(args: argparse.Namespace) -> int:
    from .qa import run as run_qa

    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    result = run_qa(pages, workers=args.workers)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def cmd_night(args: argparse.Namespace) -> int:
    from .night import run as run_night

    result = run_night(
        pages_spec=args.pages,
        workers=args.workers,
        batch=args.batch,
        skip_qa=args.skip_qa,
    )
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def cmd_models(_: argparse.Namespace) -> int:
    import os

    from google import genai

    key = os.environ.get("GOOGLE_AI__API_KEY") or os.environ.get("GEMINI_API_KEY")
    if not key:
        raise SystemExit("GOOGLE_AI__API_KEY is not set")
    client = genai.Client(api_key=key)
    for model in client.models.list():
        print(model.name)
    return 0


def cmd_sharpen(args: argparse.Namespace) -> int:
    from .render import sharpen_page

    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    for number in pages:
        sharpen_page(number, args.amount)
        log.info("sharpened page %s", number)
    return 0


def cmd_recover(args: argparse.Namespace) -> int:
    from .vision import load_vision, recover_from_ocr

    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    recovered = 0
    for number in pages:
        if load_vision(number) is not None and not args.force:
            continue
        if recover_from_ocr(number) is not None:
            recovered += 1
    print(json.dumps({"recovered": recovered, "pages": len(pages)}, ensure_ascii=False))
    return 0


def cmd_coverage(args: argparse.Namespace) -> int:
    from .coverage import run_coverage

    doc, total = _doc_and_total()
    _ensure_page_count(total)
    pages = parse_pages(args.pages, total)
    result = run_coverage(pages, workers=args.workers, batch_size=args.batch)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def cmd_status(_: argparse.Namespace) -> int:
    path = dirs()["root"] / "status.json"
    data = read_json(path)
    print(json.dumps(data or {"status": "missing"}, ensure_ascii=False, indent=2))
    return 0


def cmd_clean(args: argparse.Namespace) -> int:
    root = dirs()["root"]
    if args.work and root.exists():
        target = root.resolve()
        if target.name != "book-ocr":
            raise SystemExit(f"refusing to delete unexpected path: {target}")
        shutil.rmtree(target, ignore_errors=True)
        print(f"removed {target}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="book-ocr")
    sub = parser.add_subparsers(dest="command", required=True)

    info = sub.add_parser("info", help="show input PDF and work paths")
    info.set_defaults(func=cmd_info)

    render = sub.add_parser("render", help="render pages to enhanced grayscale PNG")
    render.add_argument("--pages", default="all")
    render.add_argument("--force", action="store_true")
    render.set_defaults(func=cmd_render)

    vision = sub.add_parser("vision", help="transcribe pages with Gemini vision")
    vision.add_argument("--pages", default="all")
    vision.add_argument("--force", action="store_true")
    vision.add_argument("--workers", type=int, default=None)
    vision.add_argument("--batch", type=int, default=None)
    vision.set_defaults(func=cmd_vision)

    ocr = sub.add_parser("ocr", help="Tesseract cross-check OCR")
    ocr.add_argument("--pages", default="all")
    ocr.add_argument("--engine", default="tesseract", choices=["tesseract"])
    ocr.add_argument("--force", action="store_true")
    ocr.set_defaults(func=cmd_ocr)

    assemble = sub.add_parser("assemble", help="build chapter Markdown from vision output")
    assemble.add_argument("--pages", default="all")
    assemble.set_defaults(func=cmd_assemble)

    qa = sub.add_parser("qa", help="compare Gemini output with Tesseract")
    qa.add_argument("--pages", default="all")
    qa.add_argument("--workers", type=int, default=4)
    qa.set_defaults(func=cmd_qa)

    night = sub.add_parser("night", help="run render -> vision -> assemble -> QA")
    night.add_argument("--pages", default="all")
    night.add_argument("--workers", type=int, default=None)
    night.add_argument("--batch", type=int, default=None)
    night.add_argument("--skip-qa", action="store_true")
    night.set_defaults(func=cmd_night)

    status = sub.add_parser("status", help="print status.json")
    status.set_defaults(func=cmd_status)

    models = sub.add_parser("models", help="list models available to the API key")
    models.set_defaults(func=cmd_models)

    sharpen = sub.add_parser("sharpen", help="sharpen blurred pages in place")
    sharpen.add_argument("--pages", default="all")
    sharpen.add_argument("--amount", type=float, default=1.6)
    sharpen.set_defaults(func=cmd_sharpen)

    recover = sub.add_parser("recover", help="rebuild pages blocked by Gemini (OCR + text fix)")
    recover.add_argument("--pages", default="all")
    recover.add_argument("--force", action="store_true")
    recover.set_defaults(func=cmd_recover)

    coverage = sub.add_parser("coverage", help="check that every page is fully transcribed")
    coverage.add_argument("--pages", default="all")
    coverage.add_argument("--workers", type=int, default=2)
    coverage.add_argument("--batch", type=int, default=2)
    coverage.set_defaults(func=cmd_coverage)

    clean = sub.add_parser("clean", help="remove work artifacts inside the container")
    clean.add_argument("--work", action="store_true")
    clean.set_defaults(func=cmd_clean)

    return parser


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    args = build_parser().parse_args(argv)
    return int(args.func(args))


if __name__ == "__main__":
    raise SystemExit(main())

