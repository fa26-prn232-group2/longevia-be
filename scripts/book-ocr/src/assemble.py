from __future__ import annotations

import json
import re
import shutil
import unicodedata
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import cv2
import numpy as np

from .common import dirs, page_id, setup_logging, update_status
from .manual import apply_patches, manual_page
from .pagemap import book_page, book_range, is_duplicate, marker
from .render import page_png
from .vision import load_vision

log = setup_logging("assemble")

CHAPTER_RE = re.compile(
    r"^(?P<hashes>#{1,4})\s*\**\s*(?P<title>(?:CHƯƠNG|Chương|PHẦN|Phần)\s+"
    r"(?P<num>[0-9]+|[IVXLC]+)\b[^\n]*?)\**\s*$",
    re.MULTILINE,
)
BARE_CHAPTER_RE = re.compile(
    r"^\**\s*(?P<title>(?:CHƯƠNG|Chương)\s+(?P<num>[0-9]+)\b[^\n]{0,120}?)\**\s*$",
    re.MULTILINE,
)
PLACEHOLDER_RE = re.compile(r"\[\[HINH:(\d+)\]\]")
MARKER_RE = re.compile(r"<!--\s*p\.(\d+)\s*-->")


@dataclass
class Section:
    title: str
    number: str
    start_page: int
    end_page: int
    body: list[str] = field(default_factory=list)
    figures: int = 0


def _find_chapter(markdown: str) -> re.Match[str] | None:
    match = CHAPTER_RE.search(markdown)
    if match:
        return match
    return BARE_CHAPTER_RE.search(markdown)


SUPERSCRIPT_MARKS = "⁰¹²³⁴⁵⁶⁷⁸⁹ⁱ⁺"


def _strip_marks(text: str) -> str:
    return text.rstrip(SUPERSCRIPT_MARKS).strip()


def _clean_title(raw: str) -> str:
    title = re.sub(r"^#{1,4}\s*", "", raw).strip()
    title = re.sub(r"\s+", " ", title)
    return _strip_marks(title.rstrip(" .:-"))


def _with_subtitle(title: str, rest: str) -> str:
    for line in rest.splitlines()[1:]:
        text = re.sub(r"^#{1,4}\s*", "", line.strip()).strip("* ").strip()
        if not text:
            continue
        text = _strip_marks(text)
        if len(text) <= 140 and text.isupper():
            return f"{title} - {text}"
        return title
    return title


def _slug(text: str, fallback: str) -> str:
    text = text.replace("đ", "d").replace("Đ", "D")
    text = unicodedata.normalize("NFD", text)
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    text = re.sub(r"[^a-zA-Z0-9]+", "-", text).strip("-").lower()
    return text[:60].rstrip("-") or fallback


def _chapter_number(title: str, index: int) -> str:
    match = re.search(r"(?:CHƯƠNG|Chương|PHẦN|Phần)\s+([0-9]+|[IVXLC]+)", title)
    raw = match.group(1) if match else str(index)
    if raw.isdigit():
        return f"{int(raw):02d}"
    return raw.lower()


def out_dir_of() -> Path:
    return dirs()["out"]


def _crop_figures(page: int, record: dict[str, Any], review: list[str]) -> tuple[str, int]:
    markdown = record.get("markdown") or ""
    figures = record.get("figures") or []
    if not figures and "[[HINH:" not in markdown:
        return markdown, 0

    image = cv2.imread(str(page_png(page)), cv2.IMREAD_GRAYSCALE)
    if image is None:
        review.append(f"- Trang {page}: không đọc được ảnh trang để crop hình.")
        return markdown, 0

    height, width = image.shape[:2]
    saved = 0
    for figure in figures:
        try:
            figure_id = int(figure.get("id"))
            ymin, xmin, ymax, xmax = [float(v) for v in figure.get("box", [])]
        except (TypeError, ValueError):
            continue

        x1 = int(round(xmin / 1000 * width))
        x2 = int(round(xmax / 1000 * width))
        y1 = int(round(ymin / 1000 * height))
        y2 = int(round(ymax / 1000 * height))
        if x2 <= x1 or y2 <= y1:
            review.append(f"- Trang {page}: box hình {figure_id} không hợp lệ {figure.get('box')}.")
            continue

        pad = 6
        x1, y1 = max(0, x1 - pad), max(0, y1 - pad)
        x2, y2 = min(width, x2 + pad), min(height, y2 + pad)
        crop = image[y1:y2, x1:x2]
        if crop.size == 0 or crop.shape[0] < 30 or crop.shape[1] < 30:
            review.append(f"- Trang {page}: vùng hình {figure_id} quá nhỏ, đã bỏ.")
            continue
        if float(np.std(crop)) < 6:
            review.append(f"- Trang {page}: vùng hình {figure_id} gần như trắng, đã bỏ.")
            continue

        name = f"{page_id(page)}-fig-{figure_id:02d}.webp"
        out = out_dir_of() / "images" / name
        out.parent.mkdir(parents=True, exist_ok=True)
        ok = cv2.imwrite(str(out), crop, [cv2.IMWRITE_WEBP_QUALITY, 85])
        if not ok:
            review.append(f"- Trang {page}: không ghi được {name}.")
            continue

        link = f"![Hình trang {page}](images/{name})"
        pattern = re.compile(rf"\[\[HINH:{figure_id}\]\]")
        if pattern.search(markdown):
            markdown = pattern.sub(link, markdown, count=1)
        else:
            markdown = markdown.rstrip() + "\n\n" + link + "\n"
            review.append(f"- Trang {page}: hình {figure_id} không có placeholder, đã chèn cuối trang.")
        saved += 1

    leftovers = PLACEHOLDER_RE.findall(markdown)
    if leftovers:
        review.append(f"- Trang {page}: placeholder không có hình tương ứng: {leftovers}.")
        markdown = PLACEHOLDER_RE.sub("", markdown)

    return markdown, saved


def _page_block(page: int, markdown: str) -> str:
    body = markdown.strip()
    return f"{marker(page)}\n{body}".strip()


def build(pages: list[int] | None = None) -> dict[str, Any]:
    total = int((dirs()["meta"] / "page_count.txt").read_text(encoding="utf-8").strip())
    target = pages or list(range(1, total + 1))
    review: list[str] = []
    sections: list[Section] = []
    front = Section(title="Phần đầu sách", number="00", start_page=target[0], end_page=0)
    current = front

    out_dir = out_dir_of()
    if out_dir.exists():
        shutil.rmtree(out_dir, ignore_errors=True)
    out_dir.mkdir(parents=True, exist_ok=True)
    images_dir = out_dir / "images"
    images_dir.mkdir(parents=True, exist_ok=True)

    models: dict[str, int] = {}
    recovered: list[int] = []

    for page in target:
        if is_duplicate(page):
            review.append(
                f"- Bỏ trang PDF {page}: ảnh chụp trùng của trang sách "
                f"{book_page(page)} (đã có ở PDF {page - 1})."
            )
            continue
        override = manual_page(page)
        if override is not None:
            markdown = override
            figure_count = 0
            model = "manual"
        else:
            record = load_vision(page)
            if record is None:
                review.append(f"- Trang {page}: chưa có kết quả Gemini.")
                continue
            model = str(record.get("model") or "unknown")
            markdown, figure_count = _crop_figures(page, record, review)
            markdown, patch_problems = apply_patches(markdown, page)
            review.extend(patch_problems)
            if patch_problems:
                model = f"{model}+patched"

        models[model] = models.get(model, 0) + 1
        if "ocr-recovery" in model or model == "tesseract-raw":
            recovered.append(page)

        current.figures += figure_count
        current.end_page = page
        match = _find_chapter(markdown)

        if match:
            before = markdown[: match.start()].strip()
            after = markdown[match.start() :].strip()
            if before:
                current.body.append(_page_block(page, before))
            if current.body:
                sections.append(current)

            title = _clean_title(match.group("title"))
            title = _with_subtitle(title, after[match.end() - match.start() :])
            number = _chapter_number(title, len(sections) + 1)
            current = Section(
                title=title,
                number=number,
                start_page=page,
                end_page=page,
            )
            current.body.append(_page_block(page, after))
        else:
            current.body.append(_page_block(page, markdown))

    if current.body:
        sections.append(current)

    chapters = list(sections)
    if not any(s.number not in {"00"} for s in sections):
        merged = Section(
            title="Toàn bộ nội dung",
            number="01",
            start_page=target[0],
            end_page=target[-1],
        )
        for section in chapters:
            merged.body.extend(section.body)
            merged.figures += section.figures
        chapters = [merged]
        review.append(
            "- Không phát hiện tiêu đề CHƯƠNG; đã gộp toàn bộ vào một file. "
            "Cần kiểm tra lại cách chia chương."
        )

    generated_at = datetime.now(timezone.utc).isoformat(timespec="seconds")
    primary_model = max(models, key=models.get) if models else "unknown"
    manifest: list[dict[str, Any]] = []
    used_stems: set[str] = set()

    for section in chapters:
        is_front = section.number == "00"
        stem = (
            "00-front-matter"
            if is_front
            else f"{section.number}-{_slug(section.title, 'chuong-' + section.number)}"
        )
        if stem in used_stems:
            stem = f"{stem}-{len(used_stems) + 1}"
        used_stems.add(stem)

        path = out_dir / f"{stem}.md"
        yaml_title = json.dumps(section.title, ensure_ascii=False)
        pages_label = (
            f"{section.start_page}-{section.end_page}"
            if section.end_page > section.start_page
            else str(section.start_page)
        )
        header = [
            "---",
            f"title: {yaml_title}",
            "source: CheDoAnTruongTho (reduced).pdf",
            f"book_pages: \"{book_range(section.start_page, section.end_page)}\"",
            f"pdf_pages: \"{pages_label}\"",
            f"engine: {primary_model}",
            f"generated_at: {generated_at}",
            "reviewed: false",
            "---",
            "",
        ]
        content = "\n\n".join(section.body).strip()
        path.write_text("\n".join(header) + content + "\n", encoding="utf-8")
        manifest.append(
            {
                "file": path.name,
                "title": section.title,
                "number": section.number,
                "book_pages": book_range(section.start_page, section.end_page),
                "pdf_pages": pages_label,
                "figures": section.figures,
            }
        )
        log.info("wrote %s (%s pages, %s figures)", path.name, pages_label, section.figures)

    _write_index(out_dir, manifest, total)
    _write_readme(
        out_dir, manifest, total, generated_at, models, recovered, book_range(target[0], target[-1])
    )
    _write_glossary(out_dir, chapters)
    _write_review(review)
    (out_dir / ".manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    update_status("assemble", state="done", chapters=len(manifest), pages=len(target))
    return {"chapters": len(chapters), "pages": len(target), "review": len(review)}


def _write_index(out_dir: Path, manifest: list[dict[str, Any]], total: int) -> None:
    lines = [
        "---",
        'title: "Mục lục"',
        "source: CheDoAnTruongTho (reduced).pdf",
        "reviewed: false",
        "---",
        "",
        "# Mục lục",
        "",
        "Số trang sách gốc đánh từ 9 (bản in). File PDF bắt đầu ở trang sách 9 nên "
        "lệch +8; từ PDF 91 trở đi lệch +7 vì PDF 91 là ảnh chụp trùng của trang sách 98.",
        "",
        "| # | Chương | Trang sách | Trang PDF | Hình |",
        "| --- | --- | --- | --- | --- |",
    ]
    for item in manifest:
        lines.append(
            f"| {item['number']} | [{item['title']}]({item['file']}) | "
            f"{item['book_pages']} | {item['pdf_pages']} | {item['figures']} |"
        )
    (out_dir / "00-index.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def _write_readme(
    out_dir: Path,
    manifest: list[dict[str, Any]],
    total: int,
    generated_at: str,
    models: dict[str, int] | None = None,
    recovered: list[int] | None = None,
    book_pages: str = "",
) -> None:
    lines = [
        "---",
        'title: "Chế độ ăn trường thọ - bản Markdown"',
        "source: CheDoAnTruongTho (reduced).pdf",
        f"pdf_pages: \"1-{total}\"",
        f"generated_at: {generated_at}",
        "reviewed: false",
        "---",
        "",
        "# Chế độ ăn trường thọ - bản Markdown",
        "",
        "Bản chuyển từ PDF scan 274 trang (ảnh thuần, không có text layer) sang Markdown",
        "để tra cứu khi triển khai Longevia.",
        "",
        "## Nguồn và phương pháp",
        "",
        "- Nguồn: `CheDoAnTruongTho (reduced).pdf` (140,7 MB, 274 trang ảnh).",
        "- Render 2000 px, khử nghiêng, xoay trang ngang, cắt viền, cân bằng nền để giảm show-through.",
        "- Nhận dạng bằng Gemini 3.5 Flash (vision), mỗi request 2 trang, có retry và fallback model.",
        "- Tesseract vie chạy độc lập để đối chiếu QA; không dùng làm nguồn chính.",
        "- Không có mục lục gốc trong PDF: chương được nhận diện từ heading in trong sách.",
        "",
        "## Cấu trúc",
        "",
        "- `00-index.md` - mục lục và liên kết chương.",
        "- `00-front-matter.md` - phần đầu sách (lời giới thiệu, lời mở đầu...).",
        "- `NN-<slug>.md` - mỗi chương một file, có marker `<!-- sách N · PDF M -->`.",
        "- `images/` - hình cắt từ sách, WebP quality 85.",
        "- `glossary.md` - thuật ngữ/viết tắt trích tự động, chưa có định nghĩa.",
        "",
        "## Đánh số trang",
        "",
        "- Marker trong mỗi file: `<!-- sách 35 · PDF 27 -->` — số trang trên bản in",
        "  trước, số trang trong file PDF scan.",
        "- Bản in đánh số từ trang 9. PDF bắt đầu đúng ở trang 9 (lệch +8) và từ",
        "  PDF 91 trở đi lệch +7, vì **PDF 91 là ảnh chụp trùng của trang sách 98**;",
        "  trang trùng đã bị loại khỏi bản Markdown.",
        "- Quy tắc ánh xạ nằm trong `scripts/book-ocr/src/pagemap.py`.",
        "",
        "## Cảnh báo",
        "",
        "- `reviewed: false`: nội dung chưa được đối chiếu thủ công toàn bộ.",
        "- Trang bị ố, nhòe hoặc cong có thể còn sai sót; xem `review/qa.md` trong",
        "  thư mục này.",
        "- Số liệu, liều lượng, khuyến nghị sức khỏe PHẢI đối chiếu lại với bản in trước",
        "  khi dùng trong sản phẩm hoặc báo cáo.",
        "",
        "## Thống kê",
        "",
        f"- Số chương: {len(manifest)}",
        f"- Tổng số hình đã cắt: {sum(m['figures'] for m in manifest)}",
        f"- Số trang PDF: {total}",
        f"- Số trang sách gốc: {book_pages}",
    ]
    if models:
        lines.append("- Model dùng để nhận dạng:")
        for model, count in sorted(models.items(), key=lambda kv: -kv[1]):
            lines.append(f"  - `{model}`: {count} trang")
    if recovered:
        lines.append(
            "- Trang bị Gemini chặn (RECITATION) và được khôi phục qua OCR Tesseract "
            "rồi sửa lỗi bằng model: "
            + ", ".join(str(p) for p in recovered)
            + ". Cần đối chiếu bản in."
        )
    lines.append(
        "- Báo cáo QA đối chiếu Tesseract nằm trong `review/` (thiếu trang, số liệu lệch, "
        "ghi chú của model)."
    )
    lines.extend(
        [
            "",
            "## Đã kiểm chứng thủ công",
            "",
            "- 4 trang được chép tay từ ảnh gốc (mô hình bị chặn khi chép nguyên văn):",
            "  27, 28, 103, 104 — bản sửa tay lưu ở `scripts/book-ocr/manual/`.",
            "- 3 trang vá lỗi sau khi đối chiếu ảnh: 34, 114, 135.",
            "- 12 trang có bảng đã so **từng ô số** với ảnh gốc: 142, 259, 260, 261, 263,",
            "  264, 265, 266, 267, 268, 269, 271 — khớp 100%.",
            "- Kiểm tra độ đầy đủ 274 trang (`review/coverage.md`): không trang nào thiếu đoạn;",
            "  6 trang bị nghi là do ký hiệu footnote siêu chỉ, đã kiểm lại và bản Markdown đúng.",
        ]
    )
    (out_dir / "README.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def _write_glossary(out_dir: Path, sections: list[Section]) -> None:
    entries: dict[str, set[int]] = {}
    pattern = re.compile(
        r"([A-Z][A-Za-z0-9\-]+(?:\s+[A-Za-z0-9\-]+){0,4})\s*"
        r"\(([A-Z][A-Z0-9\-]{1,10})\)"
    )
    for section in sections:
        for block in section.body:
            marker = MARKER_RE.match(block)
            page = int(marker.group(1)) if marker else section.start_page
            for match in pattern.finditer(block):
                term = match.group(1).strip()
                abbr = match.group(2).strip()
                entries.setdefault(f"{term} ({abbr})", set()).add(page)

    lines = [
        "---",
        'title: "Thuật ngữ và viết tắt"',
        "source: CheDoAnTruongTho (reduced).pdf",
        "reviewed: false",
        "---",
        "",
        "# Thuật ngữ và viết tắt",
        "",
        "Danh sách trích tự động từ các cụm `Thuật ngữ (VIẾT TẮT)` trong sách.",
        "Chưa có định nghĩa; bổ sung thủ công khi cần.",
        "",
        "| Thuật ngữ | Trang PDF |",
        "| --- | --- |",
    ]
    for key in sorted(entries):
        pages = ", ".join(str(p) for p in sorted(entries[key]))
        lines.append(f"| {key} | {pages} |")
    (out_dir / "glossary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def _write_review(review: list[str]) -> None:
    review_dir = dirs()["review"]
    review_dir.mkdir(parents=True, exist_ok=True)
    path = review_dir / "assemble.md"
    header = "# Assemble review\n\n"
    body = "\n".join(review) if review else "Không có vấn đề."
    path.write_text(header + body + "\n", encoding="utf-8")
