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
docker compose --env-file .env -f deployments/docker-compose.yml up -d
```

> `--env-file .env` là **bắt buộc**: file compose nằm ở `deployments/` nên Compose
> mặc định chỉ đọc `deployments/.env`, không đọc `.env` ở thư mục gốc. Thiếu cờ
> này thì các biến từ `.env` (khoá provider, cổng, mật khẩu) sẽ rỗng và các
> service có kiểm tra cấu hình sẽ **fail fast** lúc khởi động.

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

## 3. Chạy service

Khi đã có project .NET:

```bash
dotnet run --project src/Services/Identity/Identity.Api
```

Connection string trong `.env` (biến `ConnectionStrings__*`) dùng khi chạy ngoài
Docker; trong Docker, compose inject per-service.

## 4. Gate

```bash
make lint                       # fast gate
make test PROJECTS=<path>       # test scoped
make check                      # close-out gate
```

## 5. Cổng & điểm truy cập

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
