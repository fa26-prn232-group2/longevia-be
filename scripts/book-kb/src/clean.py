"""Làm sạch nội dung một trang Markdown (T069).

Quy tắc:
- bỏ placeholder hình ``[[HINH:n]]`` (không ảnh hưởng bảng);
- gộp khoảng trắng thừa **bên trong** một dòng, giữ nguyên bảng Markdown;
- bỏ dòng phân cách ngang đứng riêng;
- giữ ranh giới đoạn bằng đúng một dòng trống.

Corpus hiện tại không có dòng chạy đầu trang (đã đối chiếu ``review/qa.md``);
hàm :func:`clean_text` vẫn có nhánh loại các dòng đó để pipeline chịu được
bản OCR về sau, nhưng chỉ loại khi dòng khớp chính xác một trong các tiêu đề
chạy do người gọi truyền vào.
"""

from __future__ import annotations

import re

from .corpus import PageSlice

PLACEHOLDER_RE = re.compile(r"\[\[HINH(?::\d+)?\]\]", re.IGNORECASE)
HORIZONTAL_RULE_RE = re.compile(r"^\s*([-*_])\1{2,}\s*$")
SPACE_RE = re.compile(r"[ \t\u00a0]+")


def _is_table_line(line: str) -> bool:
    return line.lstrip().startswith("|")


def clean_text(raw: str, running_headers: frozenset[str] = frozenset()) -> str:
    """Trả về nội dung trang đã làm sạch, các đoạn cách nhau bằng ``\\n\\n``."""

    text = raw.replace("\r\n", "\n").replace("\r", "\n")
    text = PLACEHOLDER_RE.sub(" ", text)

    blocks: list[list[str]] = []
    current: list[str] = []

    def flush() -> None:
        nonlocal current
        if current:
            blocks.append(current)
            current = []

    for line in text.split("\n"):
        stripped = line.strip()
        if not stripped:
            flush()
            continue

        if stripped in running_headers:
            continue
        if HORIZONTAL_RULE_RE.match(stripped):
            continue

        if _is_table_line(stripped):
            # Bảng Markdown phải giữ nguyên từng ô và từng dòng.
            current.append(line.strip())
        else:
            current.append(SPACE_RE.sub(" ", stripped).strip())

    flush()

    rendered: list[str] = []
    for block in blocks:
        value = "\n".join(line for line in block if line.strip()).strip()
        if value:
            rendered.append(value)

    return "\n\n".join(rendered)


def clean_pages(pages: tuple[PageSlice, ...], running_headers: frozenset[str] = frozenset()) -> tuple[PageSlice, ...]:
    """Làm sạch từng trang nhưng giữ nguyên cặp số trang."""

    return tuple(
        PageSlice(
            book_page=page.book_page,
            pdf_page=page.pdf_page,
            text=clean_text(page.text, running_headers),
        )
        for page in pages
    )
