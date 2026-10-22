"""Unit tests cho chunker (T063).

Mọi token trong test được tạo từ chuỗi ký tự có độ dài chia hết cho 2.6 nên
``estimate_tokens`` trả về đúng giá trị mong đợi.
"""

from __future__ import annotations

from src.chunk import Block, chunk_blocks, estimate_tokens


def _text(tokens: int, prefix: str = "x") -> str:
    """Sinh text có ước lượng đúng ``tokens`` token (1 token = 2.6 ký tự)."""

    length = int(round(tokens * 2.6))
    filler = prefix * max(0, length - len(prefix))
    return filler[:length]


def _block(tokens: int, book_page: int, pdf_page: int, prefix: str = "x") -> Block:
    return Block(
        text=_text(tokens, prefix),
        book_page=book_page,
        pdf_page=pdf_page,
    )


def test_estimate_tokens_uses_vietnamese_ratio() -> None:
    assert estimate_tokens(_text(50)) == 50
    assert estimate_tokens(_text(1000)) == 1000


def test_chunk_cuts_at_paragraph_boundary_and_respects_target() -> None:
    blocks = [
        _block(50, 10 + index // 4, 1 + index // 4, prefix=f"{index}-")
        for index in range(10)
    ]

    chunks = chunk_blocks(blocks, target_tokens=200, overlap_tokens=50)

    assert chunks, "phải sinh được chunk"
    for chunk in chunks:
        assert chunk.token_count <= 200
        # Nội dung của chunk là ghép nguyên các block, không cắt giữa đoạn.
        assert chunk.content.split("\n\n") == [block.text for block in chunk.blocks]

    covered = [block.text for chunk in chunks for block in chunk.blocks]
    assert covered[0] == blocks[0].text
    assert covered[-1] == blocks[-1].text


def test_chunk_keeps_markdown_table_intact_even_when_oversized() -> None:
    rows = "\n".join(f"| hàng {index} | giá trị {index} |" for index in range(220))
    table = "\n".join(
        [
            "| Thực phẩm | Giá trị |",
            "| --- | --- |",
            rows,
        ]
    )
    blocks = [Block(text=table, book_page=142, pdf_page=134)]

    chunks = chunk_blocks(blocks, target_tokens=1000, overlap_tokens=150)

    assert len(chunks) == 1
    assert chunks[0].content == table
    assert chunks[0].token_count > 1000


def test_chunk_overlap_is_about_150_tokens() -> None:
    blocks = [
        _block(50, 20 + index // 4, 12 + index // 4, prefix=f"{index}:")
        for index in range(12)
    ]

    chunks = chunk_blocks(blocks, target_tokens=200, overlap_tokens=100)

    for previous, current in zip(chunks, chunks[1:]):
        previous_texts = [block.text for block in previous.blocks]
        current_texts = [block.text for block in current.blocks]
        shared = [text for text in previous_texts if text in current_texts]
        assert shared, "hai chunk liên tiếp phải có overlap"
        overlap_tokens = sum(estimate_tokens(text) for text in shared)
        assert overlap_tokens == 100

    for chunk in chunks:
        assert chunk.token_count <= 200


def test_chunk_page_range_follows_block_markers() -> None:
    blocks = [
        _block(50, 18, 10, prefix="a:"),
        _block(50, 19, 11, prefix="b:"),
        _block(50, 20, 12, prefix="c:"),
    ]

    chunks = chunk_blocks(blocks, target_tokens=1000, overlap_tokens=150)

    assert len(chunks) == 1
    chunk = chunks[0]
    assert chunk.book_page_from == 18
    assert chunk.book_page_to == 20
    assert chunk.pdf_page_from == 10
    assert chunk.pdf_page_to == 12


def test_chunk_index_is_sequential() -> None:
    blocks = [_block(50, 1, 1, prefix=f"{index}-") for index in range(9)]

    chunks = chunk_blocks(blocks, target_tokens=100, overlap_tokens=50)

    assert [chunk.chunk_index for chunk in chunks] == list(range(len(chunks)))
