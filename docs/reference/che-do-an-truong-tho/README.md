---
title: "Chế độ ăn trường thọ - bản Markdown"
source: CheDoAnTruongTho (reduced).pdf
pdf_pages: "1-274"
generated_at: 2026-10-22T06:08:15+00:00
reviewed: false
---

# Chế độ ăn trường thọ - bản Markdown

Bản chuyển từ PDF scan 274 trang (ảnh thuần, không có text layer) sang Markdown
để tra cứu khi triển khai Longevia.

## Nguồn và phương pháp

- Nguồn: `CheDoAnTruongTho (reduced).pdf` (140,7 MB, 274 trang ảnh).
- Render 2000 px, khử nghiêng, xoay trang ngang, cắt viền, cân bằng nền để giảm show-through.
- Nhận dạng bằng Gemini 3.5 Flash (vision), mỗi request 2 trang, có retry và fallback model.
- Tesseract vie chạy độc lập để đối chiếu QA; không dùng làm nguồn chính.
- Không có mục lục gốc trong PDF: chương được nhận diện từ heading in trong sách.

## Cấu trúc

- `00-index.md` - mục lục và liên kết chương.
- `00-front-matter.md` - phần đầu sách (lời giới thiệu, lời mở đầu...).
- `NN-<slug>.md` - mỗi chương một file, có marker `<!-- sách N · PDF M -->`.
- `images/` - hình cắt từ sách, WebP quality 85.
- `glossary.md` - thuật ngữ/viết tắt trích tự động, chưa có định nghĩa.

## Đánh số trang

- Marker trong mỗi file: `<!-- sách 35 · PDF 27 -->` — số trang trên bản in
  trước, số trang trong file PDF scan.
- Bản in đánh số từ trang 9. PDF bắt đầu đúng ở trang 9 (lệch +8) và từ
  PDF 91 trở đi lệch +7, vì **PDF 91 là ảnh chụp trùng của trang sách 98**;
  trang trùng đã bị loại khỏi bản Markdown.
- Quy tắc ánh xạ nằm trong `scripts/book-ocr/src/pagemap.py`.

## Cảnh báo

- `reviewed: false`: nội dung chưa được đối chiếu thủ công toàn bộ.
- Trang bị ố, nhòe hoặc cong có thể còn sai sót; xem `review/qa.md` trong
  thư mục này.
- Số liệu, liều lượng, khuyến nghị sức khỏe PHẢI đối chiếu lại với bản in trước
  khi dùng trong sản phẩm hoặc báo cáo.

## Thống kê

- Số chương: 13
- Tổng số hình đã cắt: 35
- Số trang PDF: 274
- Số trang sách gốc: 9-281
- Model dùng để nhận dạng:
  - `gemini-3.5-flash`: 227 trang
  - `gemini-3.5-flash-lite`: 34 trang
  - `gemini-3.1-flash-lite`: 6 trang
  - `manual`: 4 trang
  - `gemini-3.7-flash`: 2 trang
- Báo cáo QA đối chiếu Tesseract nằm trong `review/` (thiếu trang, số liệu lệch, ghi chú của model).

## Đã kiểm chứng thủ công

- 4 trang được chép tay từ ảnh gốc (mô hình bị chặn khi chép nguyên văn):
  27, 28, 103, 104 — bản sửa tay lưu ở `scripts/book-ocr/manual/`.
- 3 trang vá lỗi sau khi đối chiếu ảnh: 34, 114, 135.
- 12 trang có bảng đã so **từng ô số** với ảnh gốc: 142, 259, 260, 261, 263,
  264, 265, 266, 267, 268, 269, 271 — khớp 100%.
- Kiểm tra độ đầy đủ 274 trang (`review/coverage.md`): không trang nào thiếu đoạn;
  6 trang bị nghi là do ký hiệu footnote siêu chỉ, đã kiểm lại và bản Markdown đúng.
