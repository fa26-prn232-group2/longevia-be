# Identity contracts (v1)

gRPC contract do **Identity** sở hữu. Đây là nguồn chân lý cho giao tiếp đồng bộ
liên service (service khác gọi Identity qua gRPC).

- File: `identity.proto` — package `longevia.identity.v1`.
- Codegen: `Grpc.Tools` sinh client/server lúc build (khi project tham chiếu).
- Quy ước contract: không lộ entity/domain type; chỉ dữ liệu + method.
- Xem [`docs/system-design/contract-purity-pattern.md`](../../../docs/system-design/contract-purity-pattern.md).
