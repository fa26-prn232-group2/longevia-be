# ADR 001 - AuthN/AuthZ Architecture

- **Status**: Accepted
- **Decision Date**: 2026-10-07
- **Decision Maker**: Duck

## 1. Context

`docs/system-design/design-pattern.md` mô tả authentication là JWT Bearer với
*cùng issuer/audience/signing key giữa các service client-facing*. Cơ chế đó là
**shared secret đối xứng (HS256)**: mọi service đều giữ **cùng một** key, nên mọi
service đều **ký được** token.

Hệ quả trực tiếp:

- **Leo thang đặc quyền.** Nếu một service (ví dụ Diet) bị xâm nhập, kẻ tấn công
  dùng được key đó để tự phát hành token với `role=Admin` hoặc `sub` bất kỳ, và
  token đó được mọi service khác chấp nhận. Một service chỉ cần bị xâm nhập là
  toàn hệ thống bị chiếm.
- **Xoay vòng key tốn kém.** Đổi key bắt buộc phải deploy lại **tất cả** service
  cùng lúc, và không có cửa sổ chấp nhận đồng thời key cũ và key mới, nên mọi
  lần xoay vòng đều là một lần gián đoạn.

Đồng thời, khi nghiên cứu lại hạ tầng hiện có, phát hiện một điểm cần đính chính
so với cách hiểu phổ biến: các service **không** chạy chung một container. Theo
`deployments/docker-compose.yml`, mỗi service là một container riêng, cùng nằm
trong **default bridge network** của compose project. Vì vậy lý do để không cần
xác thực lời gọi nội bộ là **cùng trust domain** (cùng project, cùng host, không
expose cổng ra ngoài), **không** phải vì chung một process — hai lập luận này dẫn
tới hệ quả bảo mật khác nhau nên cần nói rõ.

Tại thời điểm chốt, dự án **chưa có code auth nào**: `AddAuthentication` /
`AddJwtBearer` không xuất hiện ở bất kỳ file `.cs` nào, `identity.proto` còn rỗng,
`src/Shared` chưa có thư mục `Auth/`, và `docs/decisions/` chưa có ADR nào.

## 2. Decision

### 2.1 Ký JWT — ES256, private key chỉ nằm ở Identity

- Thuật toán ký: **ES256** (ECDSA trên đường cong P-256).
- Chỉ **Identity** giữ **private key** và là bên phát hành token. **Diet** và
  **Progress** chỉ giữ **public key** — chỉ verify, **không ký được**.
- Token có header **`kid`**. Mỗi service chấp nhận **nhiều public key** và tra
  key tương ứng theo `kid`, thay vì chỉ một key cứng.
- **Public key được phân phối qua biến môi trường**, không qua JWKS endpoint và
  không có endpoint nào phục vụ việc này. Hệ quả trực tiếp: Diet/Progress **không**
  phụ thuộc runtime vào Identity.
- **Cửa sổ xoay vòng key**: phát `kid` mới, đồng thời giữ public key cũ ở các biến
  `JWT__PREVIOUS_*` cho tới khi **mọi** access token cũ đã hết hạn, rồi mới gỡ.
  Nhờ vậy xoay vòng không cần deploy đồng thời và không phải hiệu lực ngay lập tức.
- **Private key chỉ được inject vào Identity**, không nằm trong repo, không được
  ghi log (theo `docs/system-design/design-pattern.md` §XIX và
  `docs/development/coding-conventions.md`).
- Mô hình **stateless** giữ nguyên: service client-facing **không** gọi Identity mỗi
  request để validate, mà tin vào chữ ký đã kiểm tra bằng public key định sẵn.
- **Định dạng giá trị biến môi trường:** **base64 của PEM**, trên **một dòng**.
  - Private key ở dạng **PKCS#8** (`-----BEGIN PRIVATE KEY-----`).
  - Public key ở dạng **SPKI** (`-----BEGIN PUBLIC KEY-----`).
  - Vì PEM gốc là nhiều dòng mà biến môi trường không mang được newline, mọi giá
    trị phải là base64 một dòng. App giải mã base64 → PEM → import key; nếu dán
    nhầm PEM thô thì phải **fail sớm với thông báo dễ hiểu**, không được lỗi mập
    mờ kiểu `InvalidOperationException` — dùng `ValidateOnStart()` theo §XVII.

### 2.2 Tin cậy nội bộ — không xác thực lời gọi gRPC

- **Không xác thực caller** cho lời gọi gRPC giữa các service.
- Điều kiện kèm theo bắt buộc phải đúng:
  - Mọi service chạy trong **cùng một compose project**.
  - **Không** expose cổng của service nội bộ ra host.
- **Bù trừ bắt buộc — tách network.** `recommendation` phải nằm ở internal network
  **không** chứa nginx/postgres/rabbitmq; `worker` chỉ cần network của RabbitMQ và
  không nằm ở đó. Lý do cụ thể: container `swagger-ui` (`docker-compose.yml`) có
  DNS nội bộ và cùng join network với nginx, nên nếu không tách network thì nó — cùng
  mọi container khác — gọi được `recommendation:50051`, tức là gọi được luôn đường ra
  Google AI bằng key của dự án.
- Recommendation vẫn bị chặn ở gateway: không route qua Nginx
  (`constitution.md`, nguyên tắc về ranh giới gateway).
- **Non-goals, ghi rõ để không ai hiểu nhầm:** không mTLS, không shared-secret
  header, không phân quyền theo service.

## 3. Consequences

**2.1 — ES256**

- Tích cực: blast radius khi một service bị xâm nhập được giới hạn đúng phần dữ
  liệu của service đó; Diet bị lộ không cho phép tự phát `role=Admin` chấp nhận
  ở Identity/Progress.
- Tích cực: xoay vòng key chỉ đổi private key và bổ sung public key mới, không cần
  deploy đồng thời toàn bộ hệ thống.
- Tích cực: không có endpoint JWKS nên không phát sinh dependency runtime và
  không có bề mặt tấn công mới.
- Tiêu cực: phải quản lý vòng đời keypair và `kid` thay vì một secret tĩnh.
- Tiêu cực: giá trị env là base64(PEM), tức **double encode**. Nhìn không ra là
  PEM; đổi lại hành vi **giống hệt nhau** ở mọi nơi giá trị được khai báo (`.env`,
  compose `env_file`, compose `environment`, GitHub Secrets, Vault, Kubernetes
  secret). Đây là điểm quan trọng với secret: hỏng âm thầm ở đúng chỗ khó phát
  hiện là rủi ro lớn nhất.

**2.2 — Tin cậy nội bộ**

- Tích cực: không ceremony, phù hợp phạm vi assignment hiện tại.
- Tiêu cực: mọi container trong network đều được coi là đáng tin, không có
  attribution cho log gRPC.
- Tiêu cực: an ninh phụ thuộc vào việc giữ đúng điều kiện "cùng compose project,
  không expose cổng" và vào việc network có được tách đúng hay không. Vì vậy phần
  tách network là **bắt buộc**, không phải khuyến nghị.

**Điều kiện bắt buộc phải đánh giá lại quyết định này**

- (a) Với 2.1: xuất hiện bên thứ ba cần xác thực, có service chạy ngoài Docker
  network, hoặc cần tách quyền giữa các service → cân nhắc chuyển sang phân phối
  key qua JWKS và bỏ base64(PEM) trong env.
- (b) Với 2.2: có service chạy ngoài compose, môi trường nhiều host / Kubernetes,
  hoặc cần audit "service nào gọi service nào" → **bắt buộc** phải thêm xác thực
  nội bộ.

## 4. Bổ sung (2026-10-07)

- Quyết định này **thay thế** mô tả "cùng issuer/audience/signing key" từng viết ở
  `docs/system-design/design-pattern.md` §XVI và ở `docs/api/README.md`.
- Công cụ sinh keypair cho local dev: `make jwt-keys` (xem `scripts/jwt-keys.cs`).
  Target này in key ra stdout để dán vào `.env`, **không** ghi secret xuống đĩa.

## 5. TODO — chưa chốt

Các mục dưới đây **chưa có quyết định**, cần chốt riêng:

1. **TTL / refresh / revoke / logout.** Chưa quyết. Hệ quả hiện tại: access token
   bị rò rỉ vẫn hợp lệ tới khi hết hạn; không có cách thu hồi.
2. **Mô hình authorization.** Chưa quyết RBAC hay permission-based, có những role
   nào, tên claim ra sao, roles nhúng thẳng vào JWT hay lấy lại qua gRPC, và mô
   hình ownership cho tài nguyên của Diet/Progress. Phụ thuộc
   `docs/overview/vision.md` và `docs/overview/mvp-scope.md` — hai file này chưa
   chốt được bao nhiêu persona, nên chưa xác định được AuthZ cần tới đâu.
3. **Password hashing.** Chưa chọn thuật toán; `Directory.Packages.props` chưa
   tham chiếu package băm nào.

## Liên quan

- [../system-design/design-pattern.md](../system-design/design-pattern.md) — §XVI
  Auth & Authorization Pattern, §XVII Configuration & Options
- [../system-design/microservices/02-grpc-and-events.md](../system-design/microservices/02-grpc-and-events.md)
- [../api/README.md](../api/README.md)
- [../../.env.example](../../.env.example)