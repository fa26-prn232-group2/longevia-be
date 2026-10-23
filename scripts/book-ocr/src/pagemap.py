"""Ánh xạ trang trong PDF scan sang trang sách gốc.

Sách in đánh số từ trang 9. File PDF bắt đầu đúng ở trang sách 9, nên
trang PDF đầu lệch +8. Từ PDF 91 trở đi lệch là +7, vì **PDF 91 là ảnh chụp
lần hai của trang sách 98** (trang 98 xuất hiện ở cả PDF 90 và PDF 91).

Cơ sở:
- PDF 1..90  -> sách 9..98   (lệch +8)
- PDF 91     -> sách 98      (ảnh chụp trùng, bỏ khi dựng Markdown)
- PDF 92..274 -> sách 99..281 (lệch +7)

Đã đối chiếu bằng số trang in trên đầu trang (OCR header) ở các trang 2, 8,
12, 27, 34, 87-93, 100-120, 142, 246, 263-271 và bằng ảnh gốc.
"""

from __future__ import annotations

import re
from typing import Any

OFFSET_BEFORE_DUPLICATE = 8
OFFSET_AFTER_DUPLICATE = 7
DUPLICATE_PAGE = 91
DUPLICATE_OF = 90
FIRST_BOOK_PAGE = 9
LAST_BOOK_PAGE = 281

HEADER_RECT = re.compile(
    r"Chế độ ăn trường thọ\s*([TD\d]{1,4})|([\d]{1,4})\s*Tiến sĩ Valter Long",
    re.IGNORECASE,
)


def is_duplicate(pdf_page: int) -> bool:
    """True khi PDF page là ảnh chụp trùng của một trang sách đã có."""
    return pdf_page == DUPLICATE_PAGE


def book_page(pdf_page: int) -> int:
    """Trang sách gốc tương ứng với trang PDF."""
    if pdf_page <= DUPLICATE_PAGE - 1:
        return pdf_page + OFFSET_BEFORE_DUPLICATE
    return pdf_page + OFFSET_AFTER_DUPLICATE


def marker(pdf_page: int) -> str:
    return f"<!-- sách {book_page(pdf_page)} · PDF {pdf_page} -->"


def book_range(start_pdf: int, end_pdf: int) -> str:
    start = book_page(start_pdf)
    if is_duplicate(end_pdf):
        end_pdf = DUPLICATE_OF
    end = book_page(end_pdf)
    return f"{start}-{end}" if end > start else str(start)


def header_number(record: dict[str, Any]) -> int | None:
    """Số trang in trên đầu trang, đọc từ kết quả OCR của Tesseract.

    Tesseract hay đọc chữ "T" (số 1 viết kiểu serif) thành "T" hoặc "7";
    mọi ký tự chữ cái trong phần số được đổi thành "1".
    """
    for line in record.get("lines", [])[:2]:
        text = str(line.get("text") or "")
        match = HEADER_RECT.search(text)
        if not match:
            continue
        raw = match.group(1) or match.group(2) or ""
        digits = "".join("1" if ch.isalpha() else ch for ch in raw)
        if digits.isdigit():
            return int(digits)
    return None


def verify(pages: list[int], tesseract_records: dict[int, dict[str, Any]]) -> dict[str, Any]:
    """So số trang in trên đầu trang với mô hình ánh xạ.

    Tesseract hay nhầm chữ số (6 -> 8, 5 -> 8, 1 -> 7), nên lệch đơn lẻ được
    coi là nhiễu OCR. Chỉ khi một **cụm từ 3 trang liên tiếp** cùng lệch mới coi
    là dấu hiệu thật (thiếu hoặc thừa một trang trong bản scan).
    """
    rows: list[dict[str, Any]] = []
    for page in pages:
        if is_duplicate(page):
            continue
        record = tesseract_records.get(page)
        if not record:
            continue
        read = header_number(record)
        if read is None:
            continue
        expected = book_page(page)
        rows.append(
            {
                "pdf": page,
                "read": read,
                "expected": expected,
                "offset": read - page,
                "model_offset": expected - page,
            }
        )

    anomalies: list[dict[str, Any]] = []
    noise: list[dict[str, Any]] = []
    index = 0
    while index < len(rows):
        run = [rows[index]]
        while (
            index + 1 < len(rows)
            and rows[index + 1]["pdf"] == run[-1]["pdf"] + 1
            and rows[index + 1]["offset"] == run[0]["offset"]
        ):
            index += 1
            run.append(rows[index])
        index += 1

        if run[0]["offset"] == run[0]["model_offset"]:
            continue

        # Một lệch do đọc nhầm ký tự sẽ tự trở lại đúng mô hình ở các trang
        # sau; lệch thật (thiếu/thừa trang) kéo dài tới cuối vùng đọc được.
        restored = any(
            later["offset"] == later["model_offset"] for later in rows[index:]
        )
        if len(run) >= 3 and not restored:
            anomalies.extend(run)
        else:
            noise.extend(run)

    return {"rows": rows, "anomalies": anomalies, "noise": noise}
