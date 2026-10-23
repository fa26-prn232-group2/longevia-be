"""Ghép chunk + cờ chất lượng thành bản ghi sẵn sàng nạp (T075).

Quy tắc cờ (data-model.md §4):

- ``V(page)``: có ``manual_verified`` hoặc ``table_verified``;
- ``S(page)``: có cờ nghi vấn **và** không có ``V(page)``;
- ``chunk.needs_review`` khi ít nhất một trang trong khoảng trang của chunk là
  ``S``; ``chunk.verified`` là phủ định của ``needs_review``.
"""

from __future__ import annotations

import hashlib
from typing import Iterable

from .chunk import DEFAULT_OVERLAP_TOKENS, DEFAULT_TARGET_TOKENS, chunk_chapter
from .corpus import Corpus
from .review import PageFlag, suspicious_book_pages


def content_hash(content: str) -> str:
    return hashlib.sha256(content.encode("utf-8")).hexdigest()


def build_chunk_records(
    corpus: Corpus,
    flags: Iterable[PageFlag],
    target_tokens: int = DEFAULT_TARGET_TOKENS,
    overlap_tokens: int = DEFAULT_OVERLAP_TOKENS,
) -> list[dict[str, object]]:
    """Chunk toàn corpus, gắn metadata chương/trang và cờ chất lượng."""

    records: list[dict[str, object]] = []
    for chapter in corpus.chapters:
        for draft in chunk_chapter(
            chapter,
            target_tokens=target_tokens,
            overlap_tokens=overlap_tokens,
        ):
            records.append(
                {
                    "chapter_number": chapter.number,
                    "chapter_title": chapter.title,
                    "source_file": chapter.source_file,
                    "chunk_index": draft.chunk_index,
                    "content": draft.content,
                    "token_count": draft.token_count,
                    "content_hash": content_hash(draft.content),
                    "book_page_from": draft.book_page_from,
                    "book_page_to": draft.book_page_to,
                    "pdf_page_from": draft.pdf_page_from,
                    "pdf_page_to": draft.pdf_page_to,
                }
            )

    return apply_review(records, flags)


def apply_review(
    chunks: Iterable[dict[str, object]],
    flags: Iterable[PageFlag],
) -> list[dict[str, object]]:
    """Gán ``verified`` / ``needs_review`` cho từng chunk theo khoảng trang."""

    suspicious = suspicious_book_pages(flags)
    annotated: list[dict[str, object]] = []

    for chunk in chunks:
        book_from = int(chunk["book_page_from"])  # type: ignore[arg-type]
        book_to = int(chunk["book_page_to"])  # type: ignore[arg-type]
        needs_review = any(page in suspicious for page in range(book_from, book_to + 1))

        item = dict(chunk)
        item["verified"] = not needs_review
        item["needs_review"] = needs_review
        annotated.append(item)

    return annotated


def page_flag_records(flags: Iterable[PageFlag]) -> list[dict[str, object]]:
    """Chuyển :class:`PageFlag` thành dict để ghi JSONL / nạp DB."""

    return [
        {
            "pdf_page": flag.pdf_page,
            "book_page": flag.book_page,
            "kind": flag.kind,
            "source_file": flag.source_file,
            "note": flag.note,
        }
        for flag in flags
    ]


def summarize_chunks(chunks: Iterable[dict[str, object]]) -> dict[str, int]:
    """Đếm chunk và tỉ lệ ``needs_review`` (dùng cho build.json/verify)."""

    items = list(chunks)
    total = len(items)
    needs_review = sum(1 for chunk in items if chunk.get("needs_review"))
    verified = sum(1 for chunk in items if chunk.get("verified"))
    return {
        "chunks": total,
        "needs_review": needs_review,
        "verified": verified,
        "token_count": sum(int(chunk.get("token_count") or 0) for chunk in items),
    }
