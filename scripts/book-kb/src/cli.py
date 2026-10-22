"""CLI của pipeline tri thức sách: ``build | load | verify | eval`` (T074)."""

from __future__ import annotations

import argparse
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

from .assemble import build_chunk_records, page_flag_records, summarize_chunks
from .corpus import load_corpus
from .embed import (
    DEFAULT_BATCH_SIZE,
    EmbeddingCache,
    Embedder,
    create_client,
    embed_texts,
)
from .loader import connect, load_build, verify_database
from .review import flag_summary, load_page_flags

DEFAULT_CORPUS_DIR = "docs/reference/che-do-an-truong-tho"
DEFAULT_WORK_DIR = "artifacts/book-kb"
DEFAULT_CORPUS_KEY = "che-do-an-truong-tho"


def _env_int(name: str, default: int) -> int:
    raw = os.getenv(name)
    if raw is None or not raw.strip():
        return default
    try:
        return int(raw)
    except ValueError as exc:
        raise ValueError(f"Biến môi trường {name} phải là số nguyên: {raw!r}.") from exc


def _paths(args: argparse.Namespace) -> tuple[Path, Path]:
    corpus_dir = Path(args.corpus or os.getenv("BOOK_KB_CORPUS") or DEFAULT_CORPUS_DIR)
    work_dir = Path(args.work or os.getenv("BOOK_KB_WORK") or DEFAULT_WORK_DIR)
    return corpus_dir, work_dir


def _write_jsonl(path: Path, rows: Sequence[dict[str, Any]]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps(row, ensure_ascii=False) + "\n")
    os.replace(temporary, path)


def _write_json(path: Path, payload: dict[str, Any]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    os.replace(temporary, path)


def _build_metadata(corpus: Any, chunks: Sequence[dict[str, Any]], flags: Sequence[Any]) -> dict[str, Any]:
    summary = summarize_chunks(chunks)
    now = datetime.now(timezone.utc).isoformat()
    return {
        "corpus_key": corpus.key,
        "version": corpus.version,
        "title": corpus.title,
        "source_path": corpus.source_path,
        "chapter_count": corpus.chapter_count,
        "chunk_count": len(chunks),
        "embedding_model": os.getenv("RAG__EMBEDDING_MODEL", "gemini-embedding-001"),
        "embedding_dim": _env_int("RAG__EMBEDDING_DIM", 768),
        "chunker_params": {
            "tokens": _env_int("RAG__CHUNK_TOKENS", 1000),
            "overlap": _env_int("RAG__CHUNK_OVERLAP", 150),
        },
        "embedded_at": now,
        "built_at": now,
        "page_count": corpus.page_count,
        "page_flag_count": len(flags),
        "needs_review_chunks": summary["needs_review"],
        "verified_chunks": summary["verified"],
        "chapters": [
            {
                "number": chapter.number,
                "title": chapter.title,
                "source_file": chapter.source_file,
                "book_page_from": chapter.book_page_from,
                "book_page_to": chapter.book_page_to,
                "pdf_page_from": chapter.pdf_page_from,
                "pdf_page_to": chapter.pdf_page_to,
            }
            for chapter in corpus.chapters
        ],
    }


def run_build(args: argparse.Namespace) -> int:
    api_key = os.getenv("GOOGLE_AI__API_KEY", "").strip()
    if not api_key:
        print(
            "LỖI: GOOGLE_AI__API_KEY chưa được đặt; không thể nhúng corpus. "
            "Đặt biến môi trường này rồi chạy lại (cache sẽ tiếp tục từ chỗ dừng).",
            file=sys.stderr,
        )
        return 2

    corpus_dir, work_dir = _paths(args)
    target = _env_int("RAG__CHUNK_TOKENS", 1000)
    overlap = _env_int("RAG__CHUNK_OVERLAP", 150)
    model = os.getenv("RAG__EMBEDDING_MODEL", "gemini-embedding-001")
    dimensions = _env_int("RAG__EMBEDDING_DIM", 768)
    batch_size = _env_int("BOOK_KB_EMBED_BATCH", DEFAULT_BATCH_SIZE)
    corpus_key = os.getenv("BOOK_KB_CORPUS_KEY", DEFAULT_CORPUS_KEY)

    print(f"[build] Đọc corpus: {corpus_dir}")
    corpus = load_corpus(corpus_dir, corpus_key=corpus_key)
    print(
        f"[build] {corpus.chapter_count} chương, {corpus.page_count} trang, "
        f"version={corpus.version[:12]}…"
    )

    flags = load_page_flags(corpus.root, corpus.book_page_for)
    print(f"[build] Cờ chất lượng: {flag_summary(flags)}")

    chunks = build_chunk_records(
        corpus,
        flags,
        target_tokens=target,
        overlap_tokens=overlap,
    )
    summary = summarize_chunks(chunks)
    print(
        f"[build] {summary['chunks']} chunk, {summary['needs_review']} needs_review, "
        f"{summary['verified']} verified; nhúng bằng {model} ({dimensions} chiều)."
    )

    work_dir.mkdir(parents=True, exist_ok=True)
    cache = EmbeddingCache(work_dir / "cache")
    embedder = Embedder(
        client=create_client(api_key),
        model=model,
        dimensions=dimensions,
    )

    def progress(done: int, total: int) -> None:
        print(f"[build] Đã nhúng {done}/{total} chunk còn thiếu (cache được lưu ngay).")

    vectors = embed_texts(
        [str(chunk["content"]) for chunk in chunks],
        embedder,
        cache,
        batch_size=batch_size,
        progress=progress,
    )
    for chunk, vector in zip(chunks, vectors):
        chunk["embedding"] = vector

    metadata = _build_metadata(corpus, chunks, flags)
    if metadata["chunk_count"] != len(chunks):
        raise RuntimeError("Số chunk trong metadata không khớp danh sách chunk.")

    _write_jsonl(work_dir / "chunks.jsonl", chunks)
    _write_jsonl(work_dir / "page_flags.jsonl", page_flag_records(flags))
    _write_json(work_dir / "build.json", metadata)
    print(
        f"[build] Đã ghi {work_dir / 'chunks.jsonl'} và {work_dir / 'build.json'}.",
    )
    return 0


def run_load(args: argparse.Namespace) -> int:
    _, work_dir = _paths(args)
    connection = connect()
    try:
        result = load_build(work_dir, connection)
    finally:
        connection.close()
    print(json.dumps(result, ensure_ascii=False))
    return 0


def run_verify(args: argparse.Namespace) -> int:
    _, work_dir = _paths(args)
    corpus_key = os.getenv("BOOK_KB_CORPUS_KEY", DEFAULT_CORPUS_KEY)
    expected: dict[str, Any] | None = None
    build_path = work_dir / "build.json"
    if build_path.is_file():
        build = json.loads(build_path.read_text(encoding="utf-8"))
        corpus_key = str(build.get("corpus_key") or corpus_key)
        expected = {
            "chapter_count": build.get("chapter_count"),
            "chunk_count": build.get("chunk_count"),
        }

    connection = connect()
    try:
        report = verify_database(connection, corpus_key, expected)
    finally:
        connection.close()

    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report.get("ok") else 1


def run_eval(args: argparse.Namespace) -> int:
    del args
    print(
        "LỖI: lệnh 'eval' chưa triển khai (thuộc Phase 9 — golden set 50 câu).",
        file=sys.stderr,
    )
    return 2


def build_parser() -> argparse.ArgumentParser:
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument(
        "--corpus",
        help=f"Thư mục corpus Markdown (mặc định: {DEFAULT_CORPUS_DIR}).",
    )
    common.add_argument(
        "--work",
        help=f"Thư mục artifact/cache (mặc định: {DEFAULT_WORK_DIR}).",
    )

    parser = argparse.ArgumentParser(
        prog="python -m src.cli",
        description="Pipeline nạp kho tri thức sách vào recommendation_db.",
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    build = subparsers.add_parser(
        "build",
        parents=[common],
        help="Đọc corpus, chunk, nhúng và ghi artifacts.",
    )
    build.set_defaults(handler=run_build)

    load = subparsers.add_parser(
        "load",
        parents=[common],
        help="Nạp artifacts vào kb.* (idempotent).",
    )
    load.set_defaults(handler=run_load)

    verify = subparsers.add_parser(
        "verify",
        parents=[common],
        help="Đối chiếu chương/chunk/trang/cờ trong database.",
    )
    verify.set_defaults(handler=run_verify)

    evaluate = subparsers.add_parser(
        "eval",
        parents=[common],
        help="Chạy golden set (chưa triển khai).",
    )
    evaluate.set_defaults(handler=run_eval)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return int(args.handler(args))
    except Exception as exc:  # noqa: BLE001 - CLI phải báo lỗi rõ ràng
        print(f"LỖI: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
