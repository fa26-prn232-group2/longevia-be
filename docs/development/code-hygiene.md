# Code Hygiene

Giới hạn **cấu trúc** mà code phải nằm trong. Đây là nguồn chân lý duy nhất cho
ngưỡng cấu trúc; [`../system-design/design-pattern.md`](../system-design/design-pattern.md)
quy định *cách viết*, tài liệu này quy định *giới hạn*.

## Ngưỡng

| Mục | Giới hạn mềm | Ghi chú |
| --- | --- | --- |
| File | ~300 dòng | Vượt → tách theo trách nhiệm |
| Hàm/method | ~40 dòng | Vượt → trích helper (có tên nghĩa) |
| Tham số hàm | ≤ 5 | Nhiều hơn → gói thành request/options object |
| Độ sâu lồng | ≤ 4 | Dùng early return / guard clause |
| Class | một trách nhiệm | Không "God class" |

Ngưỡng là **mềm**: vượt phải có lý do rõ, không phải luật cứng máy móc.

## Quy tắc file & type

- Một type công khai mỗi file; tên file = tên type.
- Không file "orphan" chỉ chứa một struct lẻ — gộp vào file feature gần nhất.
- Mapper tách theo nhóm nghiệp vụ khi dài; helper một use case để cùng package,
  hậu tố `*Helper`.
- Không comment code chết; xóa. Comment chỉ giải thích "tại sao".

## Ranh giới module (tự soát)

- Không `using` namespace nội bộ của service khác.
- Không `.Include`/`.Join` sang entity của service khác.
- Không trả entity domain ra ngoài API/contract.
- Không hard-code giá trị enum — tham chiếu constant.

## Cách soát

- `make lint` (format-check + build warnings-as-errors) là gate tối thiểu.
- Review thủ công các ngưỡng trên trước khi kết thúc một phase.
- Vi phạm ranh giới service là lỗi nghiêm trọng — xem
  [`../../.specify/memory/constitution.md`](../../.specify/memory/constitution.md) §I.
