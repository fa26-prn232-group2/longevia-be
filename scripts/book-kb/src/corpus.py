"""Đọc corpus Markdown của sách và marker trang (T068).

Nguồn chuẩn của số trang là marker trong từng file:

    <!-- sách 35 · PDF 27 -->

Số **bản in** đứng trước, số **trang PDF** đứng sau. File nào thiếu marker
đều bị coi là lỗi và dừng pipeline ngay — không suy đoán số trang.
"""

from __future__ import annotations

import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path

MANIFEST_NAME = ".manifest.json"
MARKER_RE = re.compile(r"<!--\s*sách\s+(\d+)\s*·\s*PDF\s+(\d+)\s*-->")
RANGE_RE = re.compile(r"^\s*(\d+)\s*-\s*(\d+)\s*$")

DEFAULT_CORPUS_TITLE = "Chế độ ăn trường thọ"
DEFAULT_SOURCE_PATH = "docs/reference/che-do-an-truong-tho"


class CorpusError(RuntimeError):
    """Corpus không đúng định dạng hoặc thiếu dữ liệu bắt buộc."""


@dataclass(frozen=True)
class PageSlice:
    """Một trang Markdown kèm cặp số trang bản in / PDF."""

    book_page: int
    pdf_page: int
    text: str


@dataclass(frozen=True)
class Chapter:
    """Một chương trong `.manifest.json` và các trang đã tách."""

    number: int
    title: str
    source_file: str
    book_page_from: int
    book_page_to: int
    pdf_page_from: int
    pdf_page_to: int
    pages: tuple[PageSlice, ...]
    figure_count: int = 0


@dataclass(frozen=True)
class Corpus:
    """Toàn bộ corpus đã đọc, kèm version SHA-256."""

    key: str
    title: str
    root: Path
    chapters: tuple[Chapter, ...]
    version: str
    source_path: str = DEFAULT_SOURCE_PATH

    @property
    def chapter_count(self) -> int:
        return len(self.chapters)

    @property
    def page_count(self) -> int:
        return sum(len(chapter.pages) for chapter in self.chapters)

    def book_page_for(self, pdf_page: int) -> int:
        """Ánh xạ trang PDF -> trang bản in từ marker; fallback theo quy tắc PDF.

        Marker là nguồn chuẩn. Chỉ khi trang không có marker (trang PDF 91 đã bị
        loại khỏi bản Markdown) mới dùng quy tắc +8 trước PDF 91 và +7 từ PDF 91.
        """

        for chapter in self.chapters:
            for page in chapter.pages:
                if page.pdf_page == pdf_page:
                    return page.book_page

        if pdf_page < 91:
            return pdf_page + 8
        return pdf_page + 7


def _parse_range(value: object, field: str, source_file: str) -> tuple[int, int]:
    if not isinstance(value, str):
        raise CorpusError(f"{source_file}: '{field}' phải là chuỗi dạng 'a-b'.")

    match = RANGE_RE.match(value)
    if match is None:
        raise CorpusError(f"{source_file}: '{field}' không đúng dạng 'a-b': {value!r}.")

    start, end = int(match.group(1)), int(match.group(2))
    if start <= 0 or end < start:
        raise CorpusError(f"{source_file}: khoảng '{field}' không hợp lệ: {value!r}.")
    return start, end


def _split_pages(source_file: str, text: str) -> tuple[PageSlice, ...]:
    markers = list(MARKER_RE.finditer(text))
    if not markers:
        raise CorpusError(
            f"{source_file}: thiếu marker trang dạng '<!-- sách N · PDF M -->'."
        )

    pages: list[PageSlice] = []
    for index, marker in enumerate(markers):
        end = markers[index + 1].start() if index + 1 < len(markers) else len(text)
        pages.append(
            PageSlice(
                book_page=int(marker.group(1)),
                pdf_page=int(marker.group(2)),
                text=text[marker.end() : end],
            )
        )
    return tuple(pages)


def _validate_pages(source_file: str, pages: tuple[PageSlice, ...]) -> None:
    previous: PageSlice | None = None
    for page in pages:
        if page.book_page <= 0 or page.pdf_page <= 0:
            raise CorpusError(f"{source_file}: số trang phải dương ({page.book_page}, {page.pdf_page}).")

        if previous is not None:
            if page.book_page != previous.book_page + 1:
                raise CorpusError(
                    f"{source_file}: trang bản in không liên tục "
                    f"({previous.book_page} -> {page.book_page})."
                )
            pdf_step = page.pdf_page - previous.pdf_page
            # PDF 91 là ảnh trùng đã bị loại, nên có đúng một bước nhảy 2.
            if pdf_step not in (1, 2):
                raise CorpusError(
                    f"{source_file}: trang PDF không liên tục "
                    f"({previous.pdf_page} -> {page.pdf_page})."
                )
            if pdf_step == 2 and previous.pdf_page != 90:
                raise CorpusError(
                    f"{source_file}: bước nhảy trang PDF chỉ hợp lệ ở 90 -> 92 "
                    f"(gặp {previous.pdf_page} -> {page.pdf_page})."
                )
        previous = page


def _build_version(chapters: tuple[Chapter, ...]) -> str:
    digest = hashlib.sha256()
    entries: list[tuple[str, int, str]] = []
    for chapter in chapters:
        for page in chapter.pages:
            entries.append((chapter.source_file, page.pdf_page, page.text))

    for source_file, pdf_page, text in sorted(entries):
        digest.update(f"{source_file}#{pdf_page}\n".encode("utf-8"))
        digest.update(text.encode("utf-8"))

    return digest.hexdigest()


def load_corpus(root: Path, corpus_key: str, source_path: str = DEFAULT_SOURCE_PATH) -> Corpus:
    """Đọc manifest + các file chương; fail fast nếu marker/manifest sai."""

    root = Path(root)
    manifest_path = root / MANIFEST_NAME
    if not manifest_path.is_file():
        raise CorpusError(f"Không tìm thấy manifest: {manifest_path}.")

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise CorpusError(f"Manifest không phải JSON hợp lệ: {manifest_path}.") from exc

    if not isinstance(manifest, list) or not manifest:
        raise CorpusError("Manifest phải là mảng không rỗng.")

    chapters: list[Chapter] = []
    for entry in manifest:
        if not isinstance(entry, dict):
            raise CorpusError("Mỗi phần tử manifest phải là object.")

        source_file = str(entry.get("file") or "").strip()
        if not source_file:
            raise CorpusError("Manifest thiếu trường 'file'.")

        source = root / source_file
        if not source.is_file():
            raise CorpusError(f"Thiếu file nguồn trong manifest: {source_file}.")

        book_from, book_to = _parse_range(entry.get("book_pages"), "book_pages", source_file)
        pdf_from, pdf_to = _parse_range(entry.get("pdf_pages"), "pdf_pages", source_file)
        pages = _split_pages(source_file, source.read_text(encoding="utf-8"))
        _validate_pages(source_file, pages)

        if pages[0].book_page != book_from or pages[0].pdf_page != pdf_from:
            raise CorpusError(
                f"{source_file}: marker đầu ({pages[0].book_page}/{pages[0].pdf_page}) "
                f"không khớp manifest ({book_from}/{pdf_from})."
            )
        if pages[-1].book_page > book_to or pages[-1].pdf_page > pdf_to:
            raise CorpusError(
                f"{source_file}: marker cuối ({pages[-1].book_page}/{pages[-1].pdf_page}) "
                f"vượt quá manifest ({book_to}/{pdf_to})."
            )

        number_text = str(entry.get("number") or "").strip()
        if not number_text.isdigit():
            raise CorpusError(f"{source_file}: 'number' phải là số, nhận {number_text!r}.")

        chapters.append(
            Chapter(
                number=int(number_text, 10),
                title=str(entry.get("title") or "").strip(),
                source_file=source_file,
                book_page_from=book_from,
                book_page_to=book_to,
                pdf_page_from=pdf_from,
                pdf_page_to=pdf_to,
                pages=pages,
                figure_count=int(entry.get("figures") or 0),
            )
        )

    chapters.sort(key=lambda chapter: chapter.number)
    numbers = [chapter.number for chapter in chapters]
    if len(set(numbers)) != len(numbers):
        raise CorpusError("Manifest có số chương trùng nhau.")

    return Corpus(
        key=corpus_key,
        title=DEFAULT_CORPUS_TITLE,
        root=root,
        chapters=tuple(chapters),
        version=_build_version(tuple(chapters)),
        source_path=source_path,
    )
