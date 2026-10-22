# Contract của feature `duck/001/recommendation-rag`

## `recommendation.proto` — bản thiết kế

Đây là **bản thiết kế** của contract gRPC, sinh ra ở giai đoạn plan, dùng làm nguồn cho
contract authoritative trong repo:

```
contracts/recommendation/v1/recommendation.proto
```

| Nội dung | Nguồn chân lý |
| --- | --- |
| Contract gRPC | `contracts/recommendation/v1/recommendation.proto` (tạo ở giai đoạn implement) |
| Đặc tả ghi chú | [`docs/api/api-spec.md`](../../../docs/api/api-spec.md) §5 |
| Yêu cầu nghiệp vụ | [`docs/requirements/prd.md`](../../../docs/requirements/prd.md) §2.4 |
| Đặc tả feature | [`../spec.md`](../spec.md) |

Khi contract authoritative được tạo, file này **không** được commit lại trừ khi có
khác biệt — mọi thay đổi phải sửa ở `contracts/recommendation/v1/recommendation.proto`
rồi đồng bộ ngược lại `docs/api/api-spec.md` §5.

## Ghi chú thiết kế quan trọng

- `Citation` mang **cả** `book_page` (trang bản in — nguồn chuẩn để người dùng tự tra
  sách) và `pdf_page` (trang file scan — để đối chiếu ảnh gốc).
- `CandidateFood` do **bên gọi** gửi kèm: service chỉ được chọn/pha trong danh mục này,
  không sinh món mới và không tự ước lượng dinh dưỡng.
- Mọi response có `Disclaimer` (BR-04).
- `bool verified` trong `Citation` = trang nguồn đã đối chiếu với bản in hay chưa
  (chưa đối chiếu **vẫn** được trả trích dẫn — BR-06).
- `abstained` = corpus không đủ căn cứ; **không** dùng cho nguồn chưa đối chiếu.
