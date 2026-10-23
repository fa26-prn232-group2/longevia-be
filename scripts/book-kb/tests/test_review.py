"""Unit tests cho parser review/qa.md, review/numbers.md và README.md (T065)."""

from __future__ import annotations

from pathlib import Path

from src.assemble import apply_review
from src.review import (
    load_page_flags,
    overridden_book_pages,
    suspicious_book_pages,
)

QA_TABLE = """# QA đối chiếu Gemini với Tesseract

| Trang | Kind | Chars | OCR chars | Recall | Ratio | Flags |
| --- | --- | --- | --- | --- | --- | --- |
| 2 | text | 1 | 1 | 1 | 1 | missing_numbers |
| 10 | mixed | 1 | 1 | 1 | 1 | low_word_recall |
| 11 | table | 1 | 1 | 1 | 1 | missing_numbers, low_word_recall |
| 20 | text | 1 | 1 | 1 | 1 | missing_numbers |
| 30 | text | 1 | 1 | 1 | 1 |  |
"""

NUMBERS = """# Số liệu cần đối chiếu

- Trang 2: `10`
- Trang 11: `43`
- Trang 20: `814`
"""

README = """# Chế độ ăn trường thọ

## Đã kiểm chứng thủ công

- 4 trang được chép tay từ ảnh gốc (mô hình bị chặn khi chép nguyên văn):
  2, 27, 28, 34 — bản sửa tay lưu ở `scripts/book-ocr/manual/`.
- 3 trang vá lỗi sau khi đối chiếu ảnh: 114, 135, 136.
- 12 trang có bảng đã so **từng ô số** với ảnh gốc: 11, 20, 142, 259, 260, 261,
  263, 264, 265, 266, 267, 268 — khớp 100%.
"""


def _write_review(root: Path) -> None:
    (root / "review").mkdir(parents=True, exist_ok=True)
    (root / "review" / "qa.md").write_text(QA_TABLE, encoding="utf-8")
    (root / "review" / "numbers.md").write_text(NUMBERS, encoding="utf-8")
    (root / "README.md").write_text(README, encoding="utf-8")


def test_parses_flags_by_pdf_page_and_keeps_number_values(tmp_path: Path) -> None:
    _write_review(tmp_path)

    flags = load_page_flags(tmp_path, book_page_for=lambda pdf_page: pdf_page + 8)

    by_page = {(flag.pdf_page, flag.kind): flag for flag in flags}
    assert by_page[(10, "low_word_recall")].book_page == 18
    missing = by_page[(2, "missing_numbers")]
    assert missing.source_file == "review/qa.md"
    assert missing.note is not None and "10" in missing.note
    assert by_page[(11, "missing_numbers")].note is not None
    assert "43" in by_page[(11, "missing_numbers")].note


def test_manual_and_table_verified_override_suspicious_flags(tmp_path: Path) -> None:
    _write_review(tmp_path)

    flags = load_page_flags(tmp_path, book_page_for=lambda pdf_page: pdf_page + 8)

    # PDF 2 (sách 10) chép tay, PDF 11 + 20 (sách 19, 28) đã so bảng.
    assert suspicious_book_pages(flags) == {18}
    assert overridden_book_pages(flags) == {10, 19, 28}


def test_chunk_flags_follow_suspicious_page_union(tmp_path: Path) -> None:
    _write_review(tmp_path)
    flags = load_page_flags(tmp_path, book_page_for=lambda pdf_page: pdf_page + 8)

    chunks = [
        {"book_page_from": 18, "book_page_to": 19},
        {"book_page_from": 19, "book_page_to": 20},
        {"book_page_from": 20, "book_page_to": 21},
    ]

    annotated = apply_review(chunks, flags)

    # Chỉ trang bản in 18 (PDF 10) còn cờ low_word_recall.
    assert annotated[0]["needs_review"] is True
    assert annotated[0]["verified"] is False
    assert annotated[1]["needs_review"] is False
    assert annotated[1]["verified"] is True
    # Trang 20 (sách 28) đã được table_verified nên không còn nghi vấn.
    assert annotated[2]["needs_review"] is False
    assert annotated[2]["verified"] is True
