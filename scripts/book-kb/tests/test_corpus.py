"""Unit tests cho parser marker trang và manifest chương (T064)."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from src.corpus import CorpusError, load_corpus


def _write_corpus(root: Path, *, body: str, manifest: list[dict[str, object]]) -> None:
    (root / ".manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    source = root / "01-chuong-1.md"
    source.write_text(body, encoding="utf-8")


def _manifest() -> list[dict[str, object]]:
    return [
        {
            "file": "01-chuong-1.md",
            "title": "CHƯƠNG 1 - THỬ NGHIỆM",
            "number": "01",
            "book_pages": "9-11",
            "pdf_pages": "1-3",
            "figures": 2,
        }
    ]


def test_parses_page_markers_in_book_then_pdf_order(tmp_path: Path) -> None:
    _write_corpus(
        tmp_path,
        body=(
            "---\n"
            "title: x\n"
            "---\n"
            "<!-- sách 9 · PDF 1 -->\n"
            "Trang một.\n\n"
            "<!-- sách 10 · PDF 2 -->\n"
            "Trang hai.\n\n"
            "<!-- sách 11 · PDF 3 -->\n"
            "Trang ba.\n"
        ),
        manifest=_manifest(),
    )

    corpus = load_corpus(tmp_path, corpus_key="thu-nghiem")

    assert corpus.chapter_count == 1
    chapter = corpus.chapters[0]
    assert chapter.number == 1
    assert chapter.title == "CHƯƠNG 1 - THỬ NGHIỆM"
    assert chapter.source_file == "01-chuong-1.md"
    assert chapter.book_page_from == 9
    assert chapter.book_page_to == 11
    assert chapter.pdf_page_from == 1
    assert chapter.pdf_page_to == 3
    assert [(page.book_page, page.pdf_page) for page in chapter.pages] == [
        (9, 1),
        (10, 2),
        (11, 3),
    ]
    assert "Trang một." in chapter.pages[0].text
    assert "Trang ba." in chapter.pages[2].text


def test_missing_marker_fails_fast(tmp_path: Path) -> None:
    _write_corpus(
        tmp_path,
        body="Không có marker trang nào.\n",
        manifest=_manifest(),
    )

    with pytest.raises(CorpusError, match="marker"):
        load_corpus(tmp_path, corpus_key="thu-nghiem")


def test_marker_must_match_manifest_start_and_not_exceed_end(tmp_path: Path) -> None:
    _write_corpus(
        tmp_path,
        body=(
            "<!-- sách 10 · PDF 2 -->\n"
            "Sai trang bắt đầu.\n"
            "<!-- sách 12 · PDF 4 -->\n"
            "Vượt quá khoảng của manifest.\n"
        ),
        manifest=_manifest(),
    )

    with pytest.raises(CorpusError):
        load_corpus(tmp_path, corpus_key="thu-nghiem")


def test_corpus_version_is_deterministic_and_content_sensitive(tmp_path: Path) -> None:
    body = "<!-- sách 9 · PDF 1 -->\nTrang một.\n"
    _write_corpus(tmp_path, body=body, manifest=_manifest())

    first = load_corpus(tmp_path, corpus_key="thu-nghiem").version
    second = load_corpus(tmp_path, corpus_key="thu-nghiem").version
    assert first == second
    assert len(first) == 64

    _write_corpus(tmp_path, body=body + "\nThêm nội dung.\n", manifest=_manifest())
    assert load_corpus(tmp_path, corpus_key="thu-nghiem").version != first


def test_missing_manifest_fails(tmp_path: Path) -> None:
    with pytest.raises(CorpusError, match="manifest"):
        load_corpus(tmp_path, corpus_key="thu-nghiem")
