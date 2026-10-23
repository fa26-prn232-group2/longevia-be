# Local Setup

## Yêu cầu

- .NET SDK **10**
- Docker + Docker Compose
- GNU Make (Windows: `choco install make`)

## 1. Cấu hình môi trường

```bash
cp .env.example .env
# sửa giá trị nếu cần; KHÔNG commit .env
```

Mọi comment trong file `.env*` phải viết bằng tiếng Anh.

## 2. Bật hạ tầng

```bash
make up
```

`make up` chạy tuần tự: dựng PostgreSQL → chạy job một lần `kb-bootstrap`
(EF migration + nạp seed, container tự xoá) → dựng các service còn lại.
Không dùng `docker compose up -d` cho lần chạy đầu vì lệnh đó không nạp corpus.

Nếu muốn chỉ tạo schema/nạp seed mà không dựng service:

```bash
make bootstrap
```

> `--env-file .env` là **bắt buộc** khi gọi Compose trực tiếp: file compose nằm ở
> `deployments/` nên Compose mặc định chỉ đọc `deployments/.env`, không đọc `.env`
> ở thư mục gốc. Makefile đã thêm cờ này; nếu gọi tay phải tự thêm.

Khởi động:
- **PostgreSQL** (`localhost:5432`) — tạo sẵn `identity_db`, `diet_db`,
  `progress_db`, `recommendation_db` cùng role riêng mỗi DB (xem
  `deployments/postgres/init/`).
- **RabbitMQ** (`localhost:5672`, UI `http://localhost:15672`, `guest/guest`).
- **Nginx** gateway (`http://localhost:8080`).
- **Recommendation** (nội bộ, gRPC-only) — nghe `h2c` ở `localhost:50051` **trong
  mạng nội bộ `internal_net`**, không publish ra host và không route qua Nginx;
  `GET /health` ở `localhost:8080` **bên trong container**.

Lần đầu chạy, init script Postgres chỉ chạy khi volume còn trống. Muốn tạo lại
DB: `docker compose --env-file .env -f deployments/docker-compose.yml down -v` rồi
`up` lại.

## 3. Tri thức sách cho Recommendation

`recommendation_db` **bắt đầu rỗng**; service vẫn khởi động bình thường nhưng mọi
câu hỏi sẽ trả `abstained = true` cho tới khi corpus được nạp. `make up` đã tự
chạy job một lần:

| Service | Việc |
| --- | --- |
| `kb-bootstrap` | chạy EF migration tạo schema `kb` rồi nạp corpus từ seed đã commit (`--skip-if-loaded --verify`); container bị xoá sau khi xong |

Nạp corpus **không cần `GOOGLE_AI__API_KEY`** — seed đã chứa embedding.

```bash
make kb-ensure     # nạp/cập nhật seed bằng .NET bootstrap (bỏ qua nếu đã active)
make kb-verify     # đối chiếu chương/chunk/trang/embedding (pipeline Python)
make kb-smoke      # chứng minh "DB rỗng -> kb-bootstrap tự nạp" trong project riêng
make down          # dừng service + xoá container orphan
```

Xử lý sự cố:

- `docker compose --env-file .env -f deployments/docker-compose.yml --profile bootstrap logs kb-bootstrap` —
  xem lý do nạp lỗi (ví dụ seed lệch số chiều so với cột `vector(n)`).
- `GET /health` của service có check `kb-corpus`: `Degraded` nghĩa là corpus rỗng.
- Chỉ nạp lại **khi sửa nội dung sách hoặc đổi model/số chiều embedding**:
  `make kb-build` (cần API key) → `make kb-seed` → commit seed mới. Quy trình đầy
  đủ: [`rag-ingestion.md`](rag-ingestion.md).

## 4. Chạy service

Khi đã có project .NET:

```bash
dotnet run --project src/Services/Identity/Identity.Api
```

Connection string trong `.env` (biến `ConnectionStrings__*`) dùng khi chạy ngoài
Docker; trong Docker, compose inject per-service.

## 5. Gate

```bash
make lint                       # fast gate
make test PROJECTS=<path>       # test scoped
make check                      # close-out gate
```

## 6. Cổng & điểm truy cập

| Thứ | Địa chỉ |
| --- | --- |
| Gateway (Nginx) | http://localhost:8080 |
| Gateway health | http://localhost:8080/healthz |
| RabbitMQ UI | http://localhost:15672 |
| PostgreSQL | localhost:5432 |

## Xử lý sự cố

- Nginx trả 502: service backend chưa chạy — bình thường khi chỉ mới bật hạ tầng.
- Postgres không tạo DB: volume cũ đã tồn tại → `down -v` rồi `up` lại.
- Port bận: đổi qua biến trong `.env` (`POSTGRES_PORT`, `GATEWAY_PORT`).
