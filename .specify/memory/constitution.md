<!--
Sync Impact Report
==================
Version change: (template, unversioned) -> 1.0.0
Bump rationale: initial ratification; all placeholders replaced with concrete
project principles. No backward-incompatible change to an existing ratified
constitution because none existed.

Modified principles:
  - [PRINCIPLE_1_NAME] -> I. Service Boundaries & Data Ownership
  - [PRINCIPLE_2_NAME] -> II. Synchronous Inter-Service Calls via gRPC
  - [PRINCIPLE_3_NAME] -> III. Asynchronous Communication via RabbitMQ
  - [PRINCIPLE_4_NAME] -> IV. Contract-First Interfaces
  - [PRINCIPLE_5_NAME] -> V. Test-First & Verified Gates (NON-NEGOTIABLE)

Added sections:
  - Technology & Platform Constraints (was [SECTION_2_NAME])
  - Development Workflow & Quality Gates (was [SECTION_3_NAME])

Removed sections: none.

Deferred TODOs: none. All bracketed tokens were replaced with concrete text.
-->

# Longevia Backend Constitution

## Core Principles

### I. Service Boundaries & Data Ownership

Hệ thống là microservices; mỗi service sở hữu một database riêng.

- Mỗi service sở hữu một database riêng và MUST chỉ đọc/ghi database của chính
  nó. Truy cập chéo database của service khác là vi phạm nghiêm trọng, kể cả chỉ
  để đọc. Service stateless (Recommendation) không sở hữu database nào.
- Service MUST NOT import code nội bộ của service khác. Mọi phụ thuộc liên
  service đi qua contract (API, gRPC, event), không qua source.
- Một thay đổi schema chỉ được thực hiện bởi service sở hữu schema đó.
- Ranh giới service là bất biến kiến trúc; tách/gộp service là quyết định cấp
  hiến pháp, không phải chi tiết triển khai.

Rationale: dữ liệu dùng chung qua DB là nguyên nhân gốc của coupling trong
microservices; sở hữu dữ liệu riêng là điều kiện để service tiến hóa độc lập.

### II. Synchronous Inter-Service Calls via gRPC

Giao tiếp đồng bộ giữa các service MUST dùng gRPC.

- Client bên ngoài (Web/Mobile) truy cập qua API Gateway Nginx bằng HTTP/HTTPS;
  đây là ranh giới công khai, không phải giao tiếp liên service.
- Service gọi service khác theo kiểu request/response MUST qua gRPC, dùng
  contract `.proto` làm nguồn chân lý. HTTP trực tiếp giữa hai service bị cấm.
- gRPC call MUST có deadline/timeout và MUST xử lý lỗi transport một cách tường
  minh. Không giả định service đích luôn sẵn sàng.
- Thay đổi `.proto` là breaking change nếu phá vỡ consumer; xem §IV để version.

Rationale: một cơ chế đồng bộ duy nhất (gRPC) giữ hợp đồng liên service có kiểu,
hiệu năng và khả năng tiến hóa rõ ràng; trộn HTTP ad-hoc làm hợp đồng trôi nổi.

### III. Asynchronous Communication via RabbitMQ

Giao tiếp bất đồng bộ MUST qua RabbitMQ, publish và consume theo contract event.

- Event MUST được publish trong cùng transaction nghiệp vụ với thay đổi dữ liệu
  (transactional outbox); MUST NOT publish trên đường đi trực tiếp sau khi
  transaction đã commit rời rạc.
- Producer và consumer MUST chia sẻ một event contract có version; consumer MUST
  chịu được event lạ và bỏ qua an toàn (idempotent, không vỡ khi field mới xuất
  hiện).
- Timestamp mang ý nghĩa thứ tự xử lý MUST được đóng tại thời điểm xử lý, không
  phải thời điểm tạo/đăng ký bản ghi.
- Consumer MUST idempotent: xử lý trùng event không được tạo hiệu ứng phụ lần hai.

Rationale: event là hợp đồng giữa các service rời rạc; outbox và idempotency bảo
đảm không mất/không nhân đôi hiệu ứng khi có lỗi hoặc retry.

### IV. Contract-First Interfaces

Mọi giao diện liên service MUST được định nghĩa bằng contract trước khi code.

- gRPC contract: file `.proto`. Event contract: schema event có version.
- Contract là nguồn chân lý; consumer MUST NOT phụ thuộc vào chi tiết nội bộ của
  producer (entity, bảng, cấu trúc module).
- Breaking change MUST đi kèm version mới và kế hoạch chuyển đổi; thay đổi tương
  thích ngược (thêm field optional) được khuyến khích.
- Không để lộ kiểu domain/persistence nội bộ ra ngoài contract công khai.

Rationale: contract tường minh cho phép các service deploy độc lập mà không phá
vỡ nhau.

### V. Test-First & Verified Gates (NON-NEGOTIABLE)

Mọi thay đổi hành vi MUST có kiểm thử, và gate MUST xanh trước khi commit.

- Viết test trước hoặc cùng lúc với code; test phải từng thấy đỏ vì đúng lý do
  trước khi thấy xanh (negative control).
- Gate đỏ là hard stop: MUST NOT nới gate, bỏ test, hay lách qua để có commit.
- Mỗi feature MUST có ít nhất một đường end-to-end thật chạy qua hạ tầng thật
  (DB thật, broker thật) và assert giá trị cụ thể, không chỉ "không lỗi".
- Gate chia theo độ lớn thay đổi, theo bảng trong `AGENTS.md` §2 (fast gate
  `make lint`, close-out gate `make check`).

Rationale: gate xanh chỉ chứng minh assertion đã viết đúng, không chứng minh
assertion là đúng cái cần; kiểm chứng thật là điều kiện để tin một feature xong.

## Technology & Platform Constraints

- Stack: ASP.NET Core (.NET 10) cho các service; PostgreSQL làm database.
- Schema do EF Core **Code First** quản lý qua migrations; MUST NOT sửa database
  bằng tay. Mỗi service quản lý migration của database mình sở hữu.
- Gateway: Nginx là điểm vào công khai duy nhất cho client, và chỉ expose các
  service client-facing (Identity, Diet, Progress). Recommendation là service nội
  bộ, gRPC-only, MUST NOT được expose qua gateway.
- Đồng bộ liên service: gRPC (xem §II). Bất đồng bộ: RabbitMQ (xem §III).
- Dịch vụ ngoài: Cloudinary (media), Google AI (AI provider), Brevo (email) — chỉ
  gọi qua adapter, không rải lời gọi trực tiếp khắp domain.
- Background Worker là consumer hạ tầng dùng chung; MUST NOT chứa logic nghiệp vụ
  thuộc riêng một service.
- Secret và cấu hình qua biến môi trường; comment trong mọi file tên bắt đầu bằng
  `.env` MUST viết bằng tiếng Anh.
- Makefile là giao diện lệnh chuẩn; ưu tiên target Makefile hơn lệnh thủ công.

## Development Workflow & Quality Gates

- Theo [agent-workflow.md](docs/development/agent-workflow.md): task lớn/feature
  dùng Spec-kit; task nhỏ làm trực tiếp nhưng vẫn tuân Core Principles.
- Không tự ý thao tác git khi chưa được cho phép; commit message tiếng Anh theo
  Conventional Commits.
- Tài liệu trong `specs/` MUST viết bằng tiếng Việt; đường dẫn tham chiếu MUST là
  tương đối.
- Khi tài liệu mâu thuẫn: Constitution > system-design > development > khác.
- Mọi plan MUST có Constitution Check; vi phạm một `MUST` là CRITICAL, MUST được
  sửa ở tài liệu chứ không được diễn giải lại cho qua.

## Governance

- Hiến pháp này thay thế mọi thực hành, quy ước và tài liệu khác khi có xung đột
  ở ranh giới kiến trúc và nguyên tắc.
- Sửa đổi hiến pháp: thay đổi MUST được ghi lại kèm lý do, cập nhật phiên bản
  theo semver, và cập nhật ngày. Việc sửa hiến pháp là hành động có chủ đích,
  tách khỏi công việc feature.
- Phiên bản: MAJOR khi bỏ/đổi nghĩa một nguyên tắc; MINOR khi thêm nguyên tắc
  hoặc mở rộng hướng dẫn; PATCH khi làm rõ câu chữ.
- Tuân thủ: mọi review và mọi `/speckit-plan` MUST kiểm tra tuân thủ; mọi vi phạm
  MUST được báo cáo và giải quyết, không im lặng bỏ qua.
- Dùng `AGENTS.md` làm hướng dẫn vận hành runtime; hiến pháp là thẩm quyền cao
  nhất khi hai bên mâu thuẫn.

**Version**: 1.0.0 | **Ratified**: 2026-10-05 | **Last Amended**: 2026-10-05
