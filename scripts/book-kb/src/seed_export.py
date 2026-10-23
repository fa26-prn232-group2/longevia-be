"""Xuất seed tri thức đã nhúng sang ``resources/knowledge/<corpus_key>/`` (T121).

Seed là đầu ra của pipeline ``build`` (chunk + embedding) được nén lại và commit
vào repo, để môi trường mới nạp được kho tri thức mà **không cần gọi provider**
và **không cần API key** (xem `docs/decisions/002-recommendation-knowledge-base.md`).

Ghi file theo kiểu tất định: ``mtime = 0``, thứ tự dòng giữ nguyên như artifact
nguồn, để lần xuất lại không tạo diff rác.
"""

from __future__ import annotations

import gzip
import json
import os
from pathlib import Path
from typing import Any, Iterable, Sequence

DEFAULT_SEED_ROOT = "resources/knowledge"
COMPRESS_LEVEL = 9


class SeedError(RuntimeError):
    """Seed không hợp lệ hoặc artifact nguồn thiếu."""


def _read_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise SeedError(f"Không tìm thấy file build: {path}.")
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SeedError(f"{path.name} không phải JSON hợp lệ.") from exc
    if not isinstance(payload, dict):
        raise SeedError(f"{path.name} phải là một JSON object.")
    return payload


def _read_jsonl(path: Path) -> list[dict[str, Any]]:
    if not path.is_file():
        raise SeedError(f"Không tìm thấy file artifact: {path}.")
    rows: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            if not line.strip():
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise SeedError(f"{path.name} dòng {line_number} không phải JSON.") from exc
    return rows


def _write_gzip_jsonl(path: Path, rows: Iterable[dict[str, Any]]) -> int:
    """Ghi JSONL nén tất định; trả kích thước file đã ghi."""

    payload = "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows)
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("wb") as handle:
        with gzip.GzipFile(
            filename="",
            mode="wb",
            fileobj=handle,
            mtime=0,
            compresslevel=COMPRESS_LEVEL,
        ) as archive:
            archive.write(payload.encode("utf-8"))
    os.replace(temporary, path)
    return path.stat().st_size


def _validate(build: dict[str, Any], chunks: Sequence[dict[str, Any]]) -> int:
    """Kiểm tra seed khớp metadata của chính nó; trả số chiều embedding."""

    chunk_count = build.get("chunk_count")
    if not isinstance(chunk_count, int) or chunk_count <= 0:
        raise SeedError("build.json thiếu 'chunk_count' hợp lệ.")
    if chunk_count != len(chunks):
        raise SeedError(
            f"build.json ghi {chunk_count} chunk nhưng artifact có {len(chunks)}."
        )

    dimensions = build.get("embedding_dim")
    if not isinstance(dimensions, int) or dimensions <= 0:
        raise SeedError("build.json thiếu 'embedding_dim' hợp lệ.")

    for index, chunk in enumerate(chunks):
        embedding = chunk.get("embedding")
        if not isinstance(embedding, list) or len(embedding) != dimensions:
            raise SeedError(
                f"Chunk #{index} không có embedding {dimensions} chiều; "
                "seed và số chiều khai báo không khớp."
            )

    if not str(build.get("version") or "").strip():
        raise SeedError("build.json thiếu 'version' (hash corpus).")
    return dimensions


def export_seed(work_dir: Path, seed_root: Path) -> dict[str, Any]:
    """Xuất seed từ ``work_dir`` sang ``seed_root``; trả báo cáo ngắn."""

    work_dir = Path(work_dir)
    build = _read_json(work_dir / "build.json")
    chunks = _read_jsonl(work_dir / "chunks.jsonl")
    flags = _read_jsonl(work_dir / "page_flags.jsonl")
    dimensions = _validate(build, chunks)

    corpus_key = str(build.get("corpus_key") or "").strip()
    if not corpus_key:
        raise SeedError("build.json thiếu 'corpus_key'.")

    target = Path(seed_root) / corpus_key
    target.mkdir(parents=True, exist_ok=True)

    build_target = target / "build.json"
    build_target.write_text(
        json.dumps(build, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    chunks_bytes = _write_gzip_jsonl(target / "chunks.jsonl.gz", chunks)
    flags_bytes = _write_gzip_jsonl(target / "page_flags.jsonl.gz", flags)

    return {
        "corpus_key": corpus_key,
        "version": str(build["version"]),
        "embedding_model": str(build.get("embedding_model") or ""),
        "embedding_dim": dimensions,
        "chunks": len(chunks),
        "page_flags": len(flags),
        "target": str(target),
        "bytes": {
            "build.json": build_target.stat().st_size,
            "chunks.jsonl.gz": chunks_bytes,
            "page_flags.jsonl.gz": flags_bytes,
        },
    }
