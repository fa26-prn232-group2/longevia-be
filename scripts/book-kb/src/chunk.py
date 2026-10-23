"""Chunk nội dung theo đoạn, không cắt bảng (T070).

Token được ước lượng theo tiếng Việt: ``len(text) / 2.6``. Mục tiêu 1000 token,
overlap 150 token, cắt ở ranh giới đoạn; bảng Markdown là một block nguyên vẹn
nên được phép vượt 1000 token khi bản thân bảng đã dài hơn ngưỡng.
"""

from __future__ import annotations

from dataclasses import dataclass

from .clean import clean_pages
from .corpus import Chapter, PageSlice

CHARS_PER_TOKEN = 2.6
DEFAULT_TARGET_TOKENS = 1000
DEFAULT_OVERLAP_TOKENS = 150


@dataclass(frozen=True)
class Block:
    """Một đoạn văn hoặc một bảng, gắn với trang nơi nó bắt đầu."""

    text: str
    book_page: int
    pdf_page: int


@dataclass(frozen=True)
class ChunkDraft:
    """Một chunk trước khi gắn metadata chương/corpus."""

    blocks: tuple[Block, ...]
    content: str
    token_count: int
    chunk_index: int
    book_page_from: int
    book_page_to: int
    pdf_page_from: int
    pdf_page_to: int


def estimate_tokens(text: str) -> int:
    """Ước lượng token tiếng Việt; luôn trả về ít nhất 1 cho text không rỗng."""

    if not text:
        return 0
    return max(1, int(len(text) / CHARS_PER_TOKEN + 0.5))


def split_blocks(pages: tuple[PageSlice, ...]) -> list[Block]:
    """Tách các trang đã làm sạch thành block theo dòng trống."""

    blocks: list[Block] = []
    for page in clean_pages(pages):
        for paragraph in page.text.split("\n\n"):
            value = paragraph.strip()
            if value:
                blocks.append(
                    Block(
                        text=value,
                        book_page=page.book_page,
                        pdf_page=page.pdf_page,
                    )
                )
    return blocks


def chunk_blocks(
    blocks: list[Block],
    target_tokens: int = DEFAULT_TARGET_TOKENS,
    overlap_tokens: int = DEFAULT_OVERLAP_TOKENS,
) -> list[ChunkDraft]:
    """Gom block thành chunk, overlap theo block gần nhất vừa ngưỡng."""

    if not blocks:
        return []

    target = max(1, target_tokens)
    overlap = max(0, overlap_tokens)

    chunks: list[ChunkDraft] = []
    start = 0
    total = len(blocks)

    while start < total:
        end = start
        token_count = 0

        while end < total:
            block_tokens = estimate_tokens(blocks[end].text)
            if end > start and token_count + block_tokens > target:
                break
            token_count += block_tokens
            end += 1
            if token_count >= target:
                break

        selected = blocks[start:end]
        content = "\n\n".join(block.text for block in selected)
        chunks.append(
            ChunkDraft(
                blocks=tuple(selected),
                content=content,
                token_count=sum(estimate_tokens(block.text) for block in selected),
                chunk_index=len(chunks),
                book_page_from=selected[0].book_page,
                book_page_to=selected[-1].book_page,
                pdf_page_from=selected[0].pdf_page,
                pdf_page_to=selected[-1].pdf_page,
            )
        )

        if end >= total:
            break

        # Overlap: lùi về trước sao cho tổng token các block lấy lại <= ngưỡng.
        next_start = end
        overlap_count = 0
        while next_start > start:
            block_tokens = estimate_tokens(blocks[next_start - 1].text)
            if overlap_count + block_tokens > overlap:
                break
            overlap_count += block_tokens
            next_start -= 1

        if next_start <= start:
            next_start = start + 1

        start = next_start

    return chunks


def chunk_chapter(
    chapter: Chapter,
    target_tokens: int = DEFAULT_TARGET_TOKENS,
    overlap_tokens: int = DEFAULT_OVERLAP_TOKENS,
) -> list[ChunkDraft]:
    """Chunk một chương từ các trang Markdown."""

    return chunk_blocks(
        split_blocks(chapter.pages),
        target_tokens=target_tokens,
        overlap_tokens=overlap_tokens,
    )
