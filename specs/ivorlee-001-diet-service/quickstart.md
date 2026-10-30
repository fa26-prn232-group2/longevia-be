# Chạy Diet local

.NET SDK 10, Docker Desktop; owner ivorlee. Không commit .env hoặc private key.

1. Copy .env.example thành .env nếu chưa có, tạo key local theo Makefile jwt-keys.
   Giữ SMTP__HOST=mailpit, SMTP__PORT=1025, SMTP__USE_SSL=false cho compose.
2. Chạy phần độc lập (không cần provider AI hoặc Identity login):
   `docker compose --env-file .env -f deployments/docker-compose.yml up -d --build diet nginx swagger-ui mailpit prometheus grafana`
   Đây là subset Diet, không khởi tạo corpus Recommendation. Khi chạy toàn hệ
   thống, dùng make up theo README để bootstrap corpus đúng quy trình.
3. Swagger: http://localhost:8080/swagger/ hoặc
   http://localhost:8080/api/diet/swagger/index.html.
4. Grafana http://localhost:3001, Mailpit http://localhost:8025,
   Prometheus http://localhost:9090.
5. Test: make test PROJECTS=src/Services/Diet/tests/Diet.UnitTests;
   make test PROJECTS=src/Services/Diet/tests/Diet.IntegrationTests.
6. E2E: `pwsh scripts/smoke/diet-smoke.ps1`. Script đọc key local trong .env,
   không in token/key, dùng test user mới và assert số liệu qua Nginx/Postgres.
   Nó gửi đúng một email vào localhost SMTP Mailpit, không dùng provider relay.
   Không coi token ký local là bằng chứng login Identity đã hoạt động.
7. Gate theo AGENTS.md §2. Không có make thì dùng lệnh dotnet tương đương Makefile.

Khi chạy ngoài Docker, export ConnectionStrings__Diet và Jwt__SigningPublicKey,
Jwt__KeyId, Jwt__Issuer, Jwt__Audience theo property options; .NET không tự đổi
SIGNING_PUBLIC_KEY thành SigningPublicKey. Compose Diet đã map tên biến này.

Diet hiện cần RabbitMQ; compose tự khởi động dependency và truyền RabbitMQ__Host/
Port/User/Password. Messaging:UseInMemory=true chỉ dùng trong host test riêng.
Khi Progress đã bind queue và cần backfill: đặt DIET_REPLAY_MEAL_ACTIVITIES=true,
recreate Diet, xác nhận count queued, đợi consumer drain/reconcile rồi đặt lại
false. Không xem queued count là số active day hoặc delivery receipt.

Kiểm chứng Identity token thật: chạy thêm identity worker bằng compose up -d
--build, sau đó pwsh scripts/smoke/identity-diet-smoke.ps1. Script chỉ chấp nhận
SMTP local mailpit:1025/SSL=false, dùng account example.invalid mới, không in
password/token. Sáu assertion gồm email verification thật và đọc/ghi Diet.

## Xuất snapshot cho Bảo đối soát

Chạy từ repo root, sau khi bind queue consumer và nâng tất cả Diet writer:

```powershell
New-Item -ItemType Directory -Force artifacts/diet | Out-Null
$exports = (Resolve-Path artifacts/diet).Path
docker compose --env-file .env -f deployments/docker-compose.yml run --rm --no-deps -v "${exports}:/exports" diet --export-meal-activities /exports/meal-activities.json
pwsh scripts/diet/verify-meal-activity-snapshot.ps1 -Path artifacts/diet/meal-activities.json
```

Build image Diet mới trước khi chạy. Mỗi lần dùng tên file mới; không ghi đè.
Lệnh one-shot chỉ migrate/query diet_db rồi thoát, không start HTTP/bus. Không
commit snapshot (artifacts gitignored); chỉ chuyển qua operator được phép.
Reconcile toàn bộ IDs/ngày trong file sau replay, đồng thời giữ queue live.
Snapshot có count/checksum hợp lệ không chứng minh Progress đã xử lý.
