# Recommendation contracts (v1)

gRPC contract do **Recommendation** sở hữu. Recommendation là service **nội bộ**:
chỉ giao tiếp đồng bộ qua gRPC trên `internal_net`, **không** route qua Nginx
(Constitution §I).

- File: `recommendation.proto` — package `longevia.recommendation.v1`,
  `csharp_namespace = Longevia.Recommendation.Contracts.V1`.
- Codegen: `Grpc.Tools` sinh client/server lúc build. `Recommendation.Api` dùng
  `GrpcServices="Server"`, `Recommendation.IntegrationTests` dùng
  `GrpcServices="Client"`.
- Quy ước contract: không lộ entity/domain type; chỉ dữ liệu + method.
- Xem [`docs/system-design/contract-purity-pattern.md`](../../../docs/system-design/contract-purity-pattern.md).

## Quy ước đặt tên field

| Quy tắc | Ý nghĩa |
| --- | --- |
| `snake_case` | Tên field trong `.proto` luôn `snake_case`; code C# sinh ra dùng `PascalCase` (theo `Grpc.Tools`). |
| Không đổi số/thứ tự field đã phát hành | Số field (`= 1`, `= 2`, …) là hợp đồng vĩnh viễn. Thêm field mới → dùng số lớn hơn cuối cùng và **giữ nguyên thứ tự**; không bao giờ tái sử dụng số cũ. |
| Field bắt buộc → không `optional` | Enum/message không phải `repeated` thì mặc định có giá trị mặc định (`0`, `""`); `repeated` trả mảng rỗng, không trả `null`. |
| Đơn vị đo kèm hậu tố | `_grams`, `_cm`, `_kg`, `_hours`, `_minutes`, `_page`, `_pages`, `_dim`. |
| Cờ boolean dùng tiền tố `is_`/`has_`/`verified` | `is_active`, `verified`, `needs_review`, `calorie_cap_reached`. |
| Tiền tố `target_` cho mục tiêu | `target_calories`, `target_protein_remaining`. |

## Quy tắc trang sách: `book_page` vs `pdf_page`

Hai hệ số trang khác nhau và **không được quy đổi qua lại**:

| Field | Nguồn | Ý nghĩa |
| --- | --- | --- |
| `book_page` | Trang **bản in** (sách thật) | **Nguồn chuẩn** để người dùng tự mở sách tra. Mọi trích dẫn phải dùng trang bản in. |
| `pdf_page` | Trang **file PDF scan** | Chỉ để đối chiếu nội bộ với ảnh gốc; khớp trực tiếp với `pdf_page` của chunk trong `kb.chunk`. |

- `chapter_number = 0` là phần đầu sách (không phải chương).
- Marker trang trong file Markdown (`<!-- sách N · PDF M -->`) là nguồn chuẩn khi nạp
  corpus; xem [`docs/development/rag-ingestion.md`](../../../docs/development/rag-ingestion.md).
- Trích dẫn cho nguồn **chưa** đối chiếu bản in vẫn được trả, kèm
  `verified = false` — không được bỏ trích dẫn (BR-06).

## Nguồn chân lý

- Contract: file này (`contracts/recommendation/v1/recommendation.proto`).
- Bản thiết kế của feature: `specs/duck-001-recommendation-rag/contracts/recommendation.proto`
  — nếu hai bên lệch nhau, **file trong `contracts/` là nguồn chân lý** và phải cập
  nhật lại bản thiết kế.
- Mô hình dữ liệu phía sau trích dẫn:
  `specs/duck-001-recommendation-rag/data-model.md` (schema `kb`).
