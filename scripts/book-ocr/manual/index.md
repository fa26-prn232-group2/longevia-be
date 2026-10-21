# Bản sửa tay

Các trang ở đây được chép lại **trực tiếp từ ảnh trang gốc** (đọc bằng mắt) thay cho bản
nhận dạng tự động. Cơ chế: `assemble.py` ưu tiên file trong thư mục này so với kết quả
OCR/AI.

- `p00NN.md` — thay toàn bộ nội dung của trang `NN`.
- `p00NN.patches.json` — vá cục bộ trên bản hiện có, dạng:
  ```json
  { "patches": [ { "find": "chuỗi cũ", "replace": "chuỗi mới", "note": "lý do" } ] }
  ```
  Chỉ áp dụng khi `find` khớp **đúng một lần**; nếu không khớp hoặc khớp nhiều lần thì
  ghi cảnh báo vào `review/assemble.md` và giữ nguyên bản gốc.

| Trang | Lý do | Nguồn |
| --- | --- | --- |
| 27 | Gemini chặn `RECITATION` khi đọc ảnh; bản khôi phục qua OCR đã được đối chiếu lại toàn trang | ảnh gốc, trang sách 35 |
| 28 | Gemini chặn `RECITATION`; bản khôi phục có dấu chấm thừa trong tiêu đề `CHƯƠNG 2.` | ảnh gốc, trang sách 36 |
| 103 | Gemini chặn `RECITATION`; bản khôi phục sót header chạy, có placeholder `[vận động]`, sai dấu chấm | ảnh gốc, trang sách 110 |
| 104 | Gemini chặn `RECITATION`; bản khôi phục đã được đối chiếu lại toàn trang | ảnh gốc, trang sách 111 |

## Đối chiếu số liệu trong bảng

12 trang có bảng đã được so **từng ô** với ảnh gốc: 142, 259, 260, 261, 263, 264, 265,
266, 267, 268, 269, 271. Tất cả khớp 100%, gồm cả các bảng có số phần trăm và hàm lượng
vi chất mà Longevia sẽ dùng. Không cần vá ô số nào trong nhóm này.

## Vá lỗi khác (tìm ra bằng kiểm tra độ đầy đủ `coverage`)

| Trang | Lỗi | Cách sửa |
| --- | --- | --- |
| 34 | Đầu trang ghi "thế các bộ phận" thay vì "thể các bộ phận" (nối từ trang trước) | `p0034.patches.json` |
| 114 | Thiếu chú thích "Biểu đồ 6.4" của biểu đồ | `p0114.patches.json` |
| 135 | Thiếu chú thích "Biểu đồ 7.1" và "Biểu đồ 7.2" | `p0135.patches.json` |

Các trang 127, 137, 239, 246 bị cờ nhưng kiểm tra lại thấy bản Markdown **đúng**; nguyên
nhân là model đọc nhầm ký hiệu footnote siêu chỉ (`¹`, `⁶`) khi mô tả cấu trúc trang.


