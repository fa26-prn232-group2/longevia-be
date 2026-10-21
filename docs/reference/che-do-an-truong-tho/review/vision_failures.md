# Ghi chú lỗi trong quá trình nhận dạng

## 1. Gemini chặn `RECITATION` (4 trang)

Khi đọc ảnh chép nguyên văn, mô hình trả về `FinishReason.RECITATION` (coi là bản
quyền) và không sinh nội dung. Các trang bị ảnh hưởng: **27, 28, 103, 104**.

Xử lý: chép tay lại từ ảnh gốc, lưu ở `scripts/book-ocr/manual/p00NN.md`. Trang 28 và 103
ban đầu được khôi phục qua OCR Tesseract + sửa lỗi bằng model, nhưng bản đó còn sót
header chạy, placeholder và sai dấu chấm nên đã thay bằng bản chép tay.

## 2. Hạn mức free tier của Gemini

Free tier giới hạn **20 request/ngày/model**. Ba model đã dùng hết hạn mức trong lúc
chạy (`gemini-3.5-flash`, `gemini-3.5-flash-lite`, `gemini-3.6-flash`), nên pipeline
tự chuyển sang model khác và ghi lỗi 429 vào đây. Bản dịch vẫn hoàn tất 274/274 trang
nhờ các model còn hạn mức.

## 3. Lỗi kỹ thuật đã xử lý trong quá trình làm

- PaddleOCR `latin_PP-OCRv5_mobile_rec` **mất dấu tiếng Việt** → không dùng làm engine.
- PaddleOCR server detect dùng ~8 GB RAM và bị OOM trên máy này → bỏ.
- Tesseract đọc sai dấu và sai số ở các trang cong/ố, nhưng là nguồn đối chiếu QA tốt.
- Trang nằm ngang cần xoay: dùng Tesseract OSD (`--psm 0`) để tự xoay.
