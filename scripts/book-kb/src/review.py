"""Đọc cờ chất lượng từ ``review/qa.md``, ``review/numbers.md``, ``README.md``.

Mọi file review khoá theo **số trang PDF**; ``book_page`` được suy ra từ marker
trang của corpus. ``manual_verified`` / ``table_verified`` ghi đè cờ nghi vấn
của cùng trang khi tính ``verified`` / ``needs_review``.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

SUSPICIOUS_KINDS = frozenset(
    {"missing_numbers", "low_word_recall", "longer_than_ocr", "shorter_than_ocr"}
)
VERIFIED_KINDS = frozenset({"manual_verified", "table_verified"})

NUMBERS_LINE_RE = re.compile(r"^-\s*Trang\s+(\d+)\s*:\s*`([^`]+)`")
MANUAL_RE = re.compile(r"chép tay[^\n]*?:\s*([\d,\s]+)")
PATCH_RE = re.compile(r"trang vá lỗi[^:]*:\s*([\d,\s]+)")
TABLE_RE = re.compile(r"có bảng đã so[^:]*:\s*([\d,\s]+)")


class ReviewError(RuntimeError):
    """File review không đúng định dạng hoặc thiếu mục bắt buộc."""


@dataclass(frozen=True)
class PageFlag:
    """Một cờ chất lượng của một trang PDF."""

    pdf_page: int
    book_page: int
    kind: str
    source_file: str
    note: str | None = None


def _read(path: Path) -> str:
    if not path.is_file():
        raise ReviewError(f"Không tìm thấy file review: {path}.")
    return path.read_text(encoding="utf-8")


def parse_qa(path: Path) -> dict[int, tuple[str, ...]]:
    """Trả về ``{pdf_page: (kind, ...)}`` từ bảng của ``review/qa.md``."""

    flags: dict[int, tuple[str, ...]] = {}
    for line in _read(path).splitlines():
        if not line.strip().startswith("|"):
            continue

        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) < 2 or not cells[0].isdigit():
            continue

        pdf_page = int(cells[0])
        if pdf_page in flags:
            raise ReviewError(f"qa.md có dòng trùng cho trang PDF {pdf_page}.")

        raw_flags = cells[-1]
        kinds = tuple(kind.strip() for kind in raw_flags.split(",") if kind.strip())
        unknown = set(kinds) - SUSPICIOUS_KINDS
        if unknown:
            raise ReviewError(
                f"qa.md trang {pdf_page}: cờ không nhận diện được {sorted(unknown)}."
            )

        if kinds:
            flags[pdf_page] = kinds

    if not flags:
        raise ReviewError("qa.md không có dòng cờ nào.")
    return flags


def parse_numbers(path: Path) -> dict[int, tuple[str, ...]]:
    """Trả về ``{pdf_page: (giá trị, ...)}`` từ ``review/numbers.md``."""

    values: dict[int, list[str]] = {}
    for line in _read(path).splitlines():
        match = NUMBERS_LINE_RE.match(line.strip())
        if match is None:
            continue
        values.setdefault(int(match.group(1)), []).append(match.group(2).strip())

    if not values:
        raise ReviewError("numbers.md không có dòng '- Trang N: `giá trị`' nào.")
    return {page: tuple(items) for page, items in values.items()}


def _pages_from_group(group: str) -> set[int]:
    return {int(value) for value in re.findall(r"\d+", group)}


def parse_readme_verified(path: Path) -> tuple[set[int], set[int]]:
    """Trả về ``(manual_pdf_pages, table_pdf_pages)`` từ ``README.md``."""

    text = _read(path)
    manual_match = MANUAL_RE.search(text)
    patch_match = PATCH_RE.search(text)
    table_match = TABLE_RE.search(text)

    if manual_match is None or patch_match is None or table_match is None:
        raise ReviewError(
            "README.md thiếu mục 'được chép tay', 'trang vá lỗi' hoặc "
            "'có bảng đã so từng ô số'."
        )

    manual = _pages_from_group(manual_match.group(1)) | _pages_from_group(
        patch_match.group(1)
    )
    table = _pages_from_group(table_match.group(1))
    if not manual or not table:
        raise ReviewError("README.md không liệt kê được trang đã kiểm chứng.")
    return manual, table


def load_page_flags(
    root: Path,
    book_page_for: Callable[[int], int],
) -> list[PageFlag]:
    """Hợp nhất mọi nguồn review thành danh sách :class:`PageFlag`."""

    root = Path(root)
    qa = parse_qa(root / "review" / "qa.md")
    numbers = parse_numbers(root / "review" / "numbers.md")
    manual, table = parse_readme_verified(root / "README.md")

    qa_missing = {page for page, kinds in qa.items() if "missing_numbers" in kinds}
    if qa_missing != set(numbers):
        only_numbers = sorted(set(numbers) - qa_missing)
        only_qa = sorted(qa_missing - set(numbers))
        print(
            "[review] Cảnh báo: missing_numbers trong qa.md và numbers.md không trùng "
            f"nhau (chỉ numbers: {only_numbers}; chỉ qa: {only_qa}).",
        )

    flags: list[PageFlag] = []
    seen: set[tuple[int, str, str]] = set()

    def add(pdf_page: int, kind: str, source_file: str, note: str | None) -> None:
        if kind not in SUSPICIOUS_KINDS and kind not in VERIFIED_KINDS:
            raise ReviewError(f"Cờ không hợp lệ: {kind}.")
        key = (pdf_page, kind, source_file)
        if key in seen:
            return
        seen.add(key)
        flags.append(
            PageFlag(
                pdf_page=pdf_page,
                book_page=book_page_for(pdf_page),
                kind=kind,
                source_file=source_file,
                note=note,
            )
        )

    for pdf_page, kinds in sorted(qa.items()):
        for kind in kinds:
            note = None
            if kind == "missing_numbers" and pdf_page in numbers:
                note = ", ".join(numbers[pdf_page])
            add(pdf_page, kind, "review/qa.md", note)

    for pdf_page, values in sorted(numbers.items()):
        if pdf_page not in qa_missing:
            add(pdf_page, "missing_numbers", "review/numbers.md", ", ".join(values))

    for pdf_page in sorted(manual):
        add(pdf_page, "manual_verified", "README.md", "Đã đối chiếu thủ công")
    for pdf_page in sorted(table):
        add(pdf_page, "table_verified", "README.md", "Đã so từng ô số với ảnh gốc")

    return sorted(flags, key=lambda flag: (flag.pdf_page, flag.kind, flag.source_file))


def _book_pages(flags: Iterable[PageFlag], kinds: frozenset[str]) -> set[int]:
    return {flag.book_page for flag in flags if flag.kind in kinds}


def suspicious_book_pages(flags: Iterable[PageFlag]) -> set[int]:
    """Trang bản in còn cờ nghi vấn sau khi đã trừ các trang được ghi đè."""

    items = list(flags)
    suspicious = _book_pages(items, SUSPICIOUS_KINDS)
    verified = _book_pages(items, VERIFIED_KINDS)
    return suspicious - verified


def overridden_book_pages(flags: Iterable[PageFlag]) -> set[int]:
    """Trang bản in vừa có cờ nghi vấn vừa đã được đối chiếu thủ công."""

    items = list(flags)
    return _book_pages(items, SUSPICIOUS_KINDS) & _book_pages(items, VERIFIED_KINDS)


def flag_summary(flags: Iterable[PageFlag]) -> dict[str, int]:
    """Đếm số cờ theo kind và số trang PDF / bản in liên quan."""

    items = list(flags)
    by_kind: dict[str, int] = {}
    for flag in items:
        by_kind[flag.kind] = by_kind.get(flag.kind, 0) + 1

    summary = dict(sorted(by_kind.items()))
    summary["pdf_pages"] = len({flag.pdf_page for flag in items})
    summary["suspicious_book_pages"] = len(suspicious_book_pages(items))
    summary["overridden_book_pages"] = len(overridden_book_pages(items))
    return summary
