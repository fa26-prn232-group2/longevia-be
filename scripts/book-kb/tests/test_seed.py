"""Test cho seed export và quyết định skip khi nạp (T121/T123)."""

from __future__ import annotations

import gzip
import json
from pathlib import Path

import pytest

from src.loader import LoadError, _read_jsonl, is_loaded
from src.seed_export import SeedError, export_seed

CORPUS_KEY = "che-do-an-truong-tho"
VERSION = "a" * 64


def _chunk(index: int, dimensions: int = 3) -> dict[str, object]:
    return {
        "chapter_number": 0,
        "chunk_index": index,
        "content": f"đoạn {index}",
        "embedding": [0.1] * dimensions,
    }


def _work_dir(root: Path, *, chunks: int = 2, dimensions: int = 3, declared: int | None = None) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    build = {
        "corpus_key": CORPUS_KEY,
        "version": VERSION,
        "embedding_model": "gemini-embedding-001",
        "embedding_dim": dimensions,
        "chunk_count": chunks if declared is None else declared,
        "embedded_at": "2026-10-22T00:00:00+00:00",
    }
    (root / "build.json").write_text(json.dumps(build), encoding="utf-8")
    lines = [json.dumps(_chunk(index, dimensions)) for index in range(chunks)]
    (root / "chunks.jsonl").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (root / "page_flags.jsonl").write_text(
        json.dumps({"pdf_page": 1, "book_page": 9, "kind": "manual_verified", "source_file": "README.md"})
        + "\n",
        encoding="utf-8",
    )
    return root


class FakeCursor:
    def __init__(self, loaded: bool) -> None:
        self._loaded = loaded
        self.executed: list[tuple[str, tuple[object, ...]]] = []

    def execute(self, sql: str, parameters: tuple[object, ...]) -> None:
        self.executed.append((sql, parameters))

    def fetchone(self) -> tuple[int] | None:
        return (1,) if self._loaded else None

    def close(self) -> None:
        return None


class FakeConnection:
    def __init__(self, loaded: bool) -> None:
        self.cursor_instance = FakeCursor(loaded)

    def cursor(self) -> FakeCursor:
        return self.cursor_instance


def test_export_seed_writes_deterministic_gzip(tmp_path: Path) -> None:
    work = _work_dir(tmp_path / "work")

    first = export_seed(work, tmp_path / "seed-a")
    second = export_seed(work, tmp_path / "seed-b")

    first_bytes = (tmp_path / "seed-a" / CORPUS_KEY / "chunks.jsonl.gz").read_bytes()
    second_bytes = (tmp_path / "seed-b" / CORPUS_KEY / "chunks.jsonl.gz").read_bytes()

    assert first["chunks"] == 2
    assert first["embedding_dim"] == 3
    assert first_bytes == second_bytes, "seed phải tất định để không tạo diff rác"
    assert gzip.decompress(first_bytes).count(b"\n") == 2


def test_export_seed_rejects_chunk_count_mismatch(tmp_path: Path) -> None:
    work = _work_dir(tmp_path / "work", chunks=2, declared=5)

    with pytest.raises(SeedError, match="chunk"):
        export_seed(work, tmp_path / "seed")


def test_export_seed_rejects_wrong_dimensions(tmp_path: Path) -> None:
    work = _work_dir(tmp_path / "work", chunks=1, dimensions=3)
    rows = [json.dumps(_chunk(0, dimensions=2))]
    (work / "chunks.jsonl").write_text("\n".join(rows) + "\n", encoding="utf-8")

    with pytest.raises(SeedError, match="chiều"):
        export_seed(work, tmp_path / "seed")


def test_read_jsonl_accepts_gzip_and_plain(tmp_path: Path) -> None:
    plain = tmp_path / "rows.jsonl"
    plain.write_text('{"a": 1}\n', encoding="utf-8")
    assert _read_jsonl(plain) == [{"a": 1}]

    compressed = tmp_path / "rows2.jsonl"
    with gzip.open(str(compressed) + ".gz", "wt", encoding="utf-8") as handle:
        handle.write('{"b": 2}\n')
    assert _read_jsonl(compressed) == [{"b": 2}]


def test_read_jsonl_reports_missing_artifact(tmp_path: Path) -> None:
    with pytest.raises(LoadError, match="Không tìm thấy"):
        _read_jsonl(tmp_path / "missing.jsonl")


def test_is_loaded_matches_only_active_version() -> None:
    loaded = FakeConnection(True)
    empty = FakeConnection(False)

    assert is_loaded(loaded, CORPUS_KEY, VERSION) is True
    assert is_loaded(empty, CORPUS_KEY, VERSION) is False

    sql, parameters = loaded.cursor_instance.executed[0]
    assert "is_active" in sql
    assert parameters == (CORPUS_KEY, VERSION)
