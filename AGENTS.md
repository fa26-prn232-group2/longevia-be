# Agent Rules & Navigation

Dự án: **Longevia Backend** — hệ microservices ASP.NET Core (.NET 10), PostgreSQL
(EF Core Code First). Client vào qua Nginx (HTTP/HTTPS); các service gọi nhau
đồng bộ qua gRPC; sự kiện bất đồng bộ qua RabbitMQ.

## 1. Startup & Navigation

- Đọc hết `AGENTS.md` này trước khi bắt đầu.
- Chỉ đọc tài liệu liên quan tới task. Không quét toàn bộ `docs/`.
- Chọn tài liệu theo loại task:
    - Kiến trúc: `docs/system-design/`
    - Quy trình phát triển: `docs/development/`
- **Skills** là workflow nạp được, không phải tài liệu:
    - Nằm ở `.opencode/skills/`, gọi bằng `@<skill-name>`.
    - Chạy trọn một feature đầu-cuối thì dùng `@speckit-orchestrate`. Nó điều
      khiển analyze → implement → converge và tự quản git, gate, verification.
    - Các skill `@speckit-*` lẻ vẫn dùng được cho từng bước một.

## 2. Workflow Compliance

- **BẮT BUỘC** — Tuân thủ đầy đủ
  [agent-workflow.md](docs/development/agent-workflow.md):
    - **Task lớn / feature mới**: đi theo quy trình Spec-kit từng bước.
    - **Task nhỏ** (sửa lỗi đơn giản, sửa doc, config): Spec-kit là tùy chọn,
      nhưng vẫn **phải** theo các nguyên tắc cốt lõi ở §4.
    - **Tự đề xuất và làm bước tiếp theo** thay vì trả việc lại. Chỉ dừng ở
      các stop condition mà workflow đang chạy quy định.
- **Chỉ chạy speckit khi được yêu cầu**: nếu dev không gọi `@speckit-*` thì
  không cần chạy workflow speckit, nhưng vẫn phải theo `docs/development/` và
  `docs/system-design/`.
- **BẮT BUỘC** — Git:
    - **Không tự ý thao tác git**: Agent **KHÔNG ĐƯỢC** chạy bất kỳ lệnh thay
      đổi git nào (`commit`, `push`, `tag`, `branch`, `checkout`, `merge`,
      `rebase`, `reset`, `amend`, viết lại lịch sử) khi chưa có yêu cầu/cho
      phép rõ ràng của dev. Chỉ được đọc (`git status/log/diff`). Điều này áp
      dụng cả khi dev đã duyệt một plan có nhắc commit — mỗi thao tác git vẫn
      cần chỉ thị riêng. NGOẠI LỆ: gọi một skill tự khai báo git policy (ví dụ
      `@speckit-orchestrate`) là sự cho phép thường trực cho lần chạy đó; policy
      của skill đó chi phối, gồm cả nhịp commit và danh sách phải hỏi trước.
    - Commit message tiếng Anh, theo Conventional Commits.
    - **Gate chia theo độ lớn thay đổi.** Bảng dưới là nguồn chân lý duy nhất
      cho gate; đừng lặp lại gate ở tài liệu khác.

        | Thay đổi | Gate |
        | --- | --- |
        | Sửa nhỏ (typo, doc, config, một task) | `make lint` + test cho project vừa chạm |
        | Một phase của feature | `make lint` + `make test PROJECTS=<path>` scoped theo project đã chạm |
        | Trước `push` / `tag` | `make check` chạy đầy đủ |

        `make lint` = format-check + build (warnings as errors) — đây là **fast
        gate**. `make check` gồm format-check, build Release, toàn bộ test và
        quét package lỗ hổng — đây là **close-out gate**. Cả hai KHÔNG thuộc
        vòng lặp mỗi typo.

## 3. Code Compliance

- **BẮT BUỘC** — Tuân thủ tài liệu trong:
    - `docs/development/` — quy trình, convention, chuẩn handler, validation,
      routing, testing, migration.
    - `docs/system-design/` — kiến trúc tổng thể, design pattern, event-driven
      design, ranh giới module.
- Khi viết code, **phải** đọc và đối chiếu tài liệu liên quan tới task trước
  khi làm.
- **Ranh giới service** (theo `docs/system-design/diagrams/png/c4-model-lv2.png`):
    - Mỗi service sở hữu database riêng; không truy cập chéo DB của service khác.
      Service stateless (Recommendation) không sở hữu DB nào.
    - **Client-facing (qua Nginx)**: Identity, Diet, Progress. Recommendation là
      **service nội bộ, gRPC-only**, không expose REST và không route qua Nginx.
    - Giao tiếp đồng bộ **giữa các service qua gRPC**; client bên ngoài vào qua
      Nginx (HTTP/HTTPS). Không dùng HTTP trực tiếp giữa hai service, không gọi
      xuyên module bằng import nội bộ.
    - Giao tiếp bất đồng bộ qua RabbitMQ; publish event và consume qua contract.
    - Schema do EF Core **Code First** quản lý (migrations), không sửa DB bằng tay.
- **BẮT BUỘC** — File môi trường: mọi comment trong file có tên bắt đầu bằng
  `.env` (ví dụ `.env.example`) **phải viết bằng tiếng Anh**.
- Nếu tài liệu mâu thuẫn, thứ tự ưu tiên:
  Constitution > system-design > development > tài liệu khác.

## 4. Core Principles

- **Tuân thủ Constitution (BẮT BUỘC)**: với **mọi hành động** — dù có theo
  quy trình Spec-kit hay không — phải đọc và tuân thủ
  [`.specify/memory/constitution.md`](.specify/memory/constitution.md). Đây là
  văn bản kiến trúc có thẩm quyền cao nhất của dự án. Khi constitution mâu
  thuẫn với tài liệu khác, constitution thắng ở ranh giới kiến trúc và nguyên
  tắc.
- **Giải quyết xung đột**: nếu tài liệu và source code mâu thuẫn về kiến trúc,
  data model hoặc hành vi người dùng thấy được, DỪNG, giải thích, và hỏi cái
  nào là chuẩn. Với chi tiết mà tài liệu để ngỏ, tự quyết, rồi ghi lại quyết
  định và lý do trong báo cáo.
- **Makefile**: luôn ưu tiên target Makefile hơn lệnh thủ công nếu có target
  tương đương.
- **Ngôn ngữ giao tiếp**: dùng tiếng Việt khi trao đổi với dev.
- **Hỏi bằng question UI, không hỏi bằng văn xuôi**: khi cần dev quyết định,
  dùng công cụ `question` với các lựa chọn cụ thể, mỗi lựa chọn có nhãn ngắn và
  mô tả hậu quả, đánh dấu lựa chọn ưu tiên bằng "(Recommended)". Chỉ dùng văn
  xuôi cho câu hỏi mở mà lựa chọn sẽ làm mất thông tin.
- **Tài liệu trong `specs/`**: viết bằng tiếng Việt theo
  agent-workflow.md §IV.7.
- **Đường dẫn tương đối**: luôn dùng đường dẫn tương đối khi tham chiếu file
  trong doc, spec và code; tránh đường dẫn tuyệt đối kiểu `file:///C:/...`.
