# Bằng chứng xác minh Diet độc lập

Môi trường: Windows, .NET SDK 10.0.402, Docker Desktop, PostgreSQL 17.
Nhánh feat/diet-service, owner ivorlee. Developer đã cho phép push và tạo PR.

## Gate đã chạy

Máy không có make; dùng các lệnh dotnet tương đương target Makefile.

| Kiểm tra | Kết quả |
| --- | --- |
| dotnet format longevia-be.slnx --verify-no-changes --verbosity quiet | Exit 0 |
| dotnet build longevia-be.slnx --no-restore -warnaserror | 0 warning, 0 error |
| dotnet build longevia-be.slnx -c Release --no-restore -warnaserror | 0 warning, 0 error |
| dotnet test longevia-be.slnx -c Release --no-restore | 537 passed, 0 failed, 0 skipped |
| dotnet test longevia-be.slnx -c Release (gate trước push) | 537 passed, 0 failed, 0 skipped |
| dotnet list longevia-be.slnx package --vulnerable --include-transitive | 21 project không có package dễ tổn thương theo nguồn hiện tại |
| EF has-pending-model-changes (Diet Infrastructure, startup Diet API) | Không có thay đổi chưa migration |
| pwsh scripts/speckit/team-feature.ps1 check | 5 feature, owner ivorlee, layout hợp lệ |
| pwsh scripts/smoke/diet-smoke.ps1 | 12 kiểm tra qua trên image vừa rebuild |
| pwsh scripts/smoke/infra-smoke.ps1 | 6/6 qua |

537 test gồm Diet unit 11 + integration 16, Identity unit 52 + integration 6,
Recommendation unit 372 + integration 64, Worker unit 16. Integration Diet dùng
PostgreSQL thật qua Testcontainers và token ES256 ký bằng key test riêng.

## Những hành vi được kiểm chứng

- Catalog chung/custom, paging, không dùng được món riêng của tài khoản khác.
- Sáu operation đều yêu cầu đăng nhập; sub không hợp lệ trả 401.
- Target nhập tay, macro ±5%, cập nhật và concurrent upsert giữ nguyên ID.
- Meal nhiều lần/ngày được cộng; batch có food sai không lưu một phần.
- Lịch sử giữ snapshot dinh dưỡng dù catalog đổi; user khác không đọc được log.
- 400 validation có errors/traceId, null items và macro sai bị từ chối.
- Swagger: 6 operation, 13 DTO nghiệp vụ, tổng 73 property khai báo.
- Gateway thật → Diet → PostgreSQL: 150g món mẫu lưu 247,5 kcal / 46,5g protein.
- Prometheus job Diet up; Grafana localhost:3001 healthy; Mailpit localhost:8025
  nhận email thử qua SMTP localhost:1025. Mỗi lần chạy Diet smoke gửi một email
  local tới example.invalid, không dùng relay bên ngoài.

## Kiểm tra đối chứng

- Test tính khẩu phần ban đầu thất bại trước khi có implementation, sau đó qua.
- Tạm bỏ điều kiện owner ở GetVisibleAsync: test isolation thất bại đúng với
  expected 404 / actual 201. Đã khôi phục điều kiện, chạy lại toàn suite xanh.
- Infra smoke với ForceFail thất bại một assertion như dự kiến; chạy thường qua.

## Giới hạn

Giới hạn ở giai đoạn độc lập trước đây: smoke dùng JWT ký bằng key local.
Giai đoạn 2026-10-25 bên dưới đã bổ sung kiểm chứng login Identity thật.
Chưa triển khai BMR/TDEE, AI, digest hay weekly plan phía Diet; consumer Streak
Progress còn thiếu, Diet producer đã có ở giai đoạn bên dưới;
xem [deferred.md](deferred.md). Không đổi contract gRPC/event. Đã chạy đầy đủ các
lệnh tương đương make check trước push, bao gồm vulnerability scan.
Seed ba món là dữ liệu minh họa, chưa phải catalog dinh dưỡng sản xuất.

## Giai đoạn cung cấp tích hợp — 2026-10-25

Base main 2e249a2. Feature vẫn open vì còn các dependency đã ghi rõ.

| Kiểm tra | Kết quả hiện tại |
| --- | --- |
| dotnet format longevia-be.slnx --verify-no-changes --verbosity quiet | Exit 0 |
| dotnet build longevia-be.slnx --no-restore -warnaserror | 0 warning, 0 error |
| dotnet test longevia-be.slnx -c Release --no-restore | 560 passed, 0 failed, 0 skipped |
| EF has-pending-model-changes, Diet | Không có model chưa migration |
| team-feature.ps1 check | 6 feature, owner ivorlee, layout consistent |
| Docker Diet image rebuild + diet-smoke.ps1 | 12/12 assertion qua |
| Startup replay với durable probe queue đã bind | Đúng 3 event lịch sử, distinct EventId=MealLogId, có BusinessDate |
| identity-diet-smoke.ps1 trên Compose config đã sửa | 6/6 assertion qua |

560 test gồm Diet unit 12 + integration 20, Identity unit 67 + integration 9,
Recommendation unit 372 + integration 64, Worker unit 16. Không tính source
Progress ở PR #11 chưa merge. Không chạy vulnerability scan lại trong phase này;
gate trước push tương lai phải theo AGENTS.md.

### Evidence event và phục hồi

- Contract round-trip BusinessDate, đúng 6 property, bỏ qua unknown field.
- HTTP meal → diet_db/outbox → RabbitMQ thật → receiver đúng EventId/UserId/
  MealLogId/BusinessDate; replay giữ EventId và CreatedAt gốc.
- Rollback xóa cả meal và outbox, receiver không nhận event.
- 101 legacy meal qua hai batch replay, đủ 101 ID distinct và đúng ngày cũ.
- Ngắt dịch vụ RabbitMQ bằng rabbitmqctl stop_app: HTTP vẫn commit meal/outbox;
  start_app phục hồi, receiver nhận đúng log/ngày. Queue durable, auto-delete=false.
- Negative control bỏ PublishAsync trong MealUseCases: tạo meal thành công nhưng
  test delivery đỏ vì không có event; khôi phục publish và suite xanh.
- Test phát hiện precision .NET ticks khác PostgreSQL microseconds; đã chuẩn hóa
  CreatedAt của MealLog để original/replay không lệch timestamp.
- Fixture outage từng đỏ khi restart container/queue; cuối cùng mô phỏng ngắt
  dịch vụ trên cùng node/port với durable queue để kiểm tra đúng reconnect.

### Token Identity thật và email local

Sửa mapping JWT và RabbitMQ credentials trong Identity Compose, không sửa source
Identity/Shared auth policy. Smoke chạy qua Nginx: register, email từ Worker vào
Mailpit, verify 204, login token thật, Diet catalog và target write 2000 kcal.
Không in key/token/password; chỉ SMTP local. Script có readiness polling và lọc
recipient rõ từng phần tử (không dùng property Address trực tiếp trên array).

### Giới hạn bàn giao

Receiver test không phải consumer/projection Progress. Chưa READY_FOR_BACKEND cho
Streak đầy đủ; cần Bảo inbox/dedup/reconciliation, Duy queue/DLQ review. Startup
replay phải bind queue trước; queued count không chứng minh projection complete.
Profile/weight provider, JWT missing-kid agreement và AI/digest còn chờ.
[Bốn file gửi từng người](handoffs/README.md).

## Hoàn tất nghĩa vụ Diet không bị block — 2026-10-25

- Full Release suite: **563 passed, 0 failed, 0 skipped**. Diet unit 12 +
  integration 23; Identity 67 + 9; Recommendation 372 + 64; Worker 16.
- Whole-solution format verify exit 0; Debug build warnings-as-errors:
  0 warning, 0 error. Make không được cài, dùng lệnh dotnet tương đương.
- PostgreSQL snapshot test: empty export, 101 meal nhiều user/ngày, checksum,
  identity/timestamp đúng, không overwrite và không để file partial.
- Metadata test: null khác [], trim/lowercase/dedup/sort được persist, isolation;
  reject tag trắng/null/quá dài/quá số lượng, không persist request lỗi.
- Negative control mapper đổi null thành [] làm test đỏ; khôi phục mapper và
  toàn suite xanh. Swagger counted sync: 6 operation / 13 DTO / **75 field**.
- Migration AddFoodAllergenMetadata: Up chỉ thêm nullable text[] và giữ seed
  unknown; không xóa dữ liệu hiện có. Docker CLI áp dụng migration thành công.
- Rebuild image Diet thành công; CLI export trên DB local xuất **3 distinct
  meal** trước khi chạy smoke mới, HTTP/bus không khởi động trong lệnh export.
  Verifier file gốc PASS; sửa businessDate không sửa checksum bị reject đúng
  checksum mismatch; file gốc vẫn PASS. Artifact nằm ở artifacts/diet (ignored).
- Recreate Diet trên image mới; diet-smoke **12/12**, infra-smoke **6/6**.
  Grafana localhost:3001 và Mailpit localhost:8025 hoạt động, email chỉ local.
- Registry: 6 feature, owner ivorlee, layout consistent; git diff --check sạch.

Snapshot là file operator để đối soát, không thêm HTTP/gRPC. Checksum kiểm tra
integrity, không xác thực nguồn gửi; chuyển file bằng kênh được nhóm cho phép.
Không claim consumer Streak, BMR/TDEE hoặc meal AI hoàn tất. Dependency còn chờ
ở [remaining-blockers.md](handoffs/remaining-blockers.md); file gửi từng người ở
[handoffs/README.md](handoffs/README.md). Gate trước push được ghi riêng bên dưới.

## Gate trước push — 2026-10-25

Developer cho phép commit/push lên nhánh feature. Dùng nhánh đã bind
feat/diet-service (một feature, một nhánh), không tạo nhánh trùng feature.
Do máy không có make, chạy đủ các lệnh tương đương make check:

- dotnet format longevia-be.slnx --verify-no-changes --verbosity quiet: exit 0.
- dotnet build longevia-be.slnx -c Release --no-restore -warnaserror:
  0 warning, 0 error.
- Full Release suite chạy lại với --no-build --no-restore trên source không đổi:
  563 passed, 0 failed, 0 skipped (Diet integration 23).
- dotnet list longevia-be.slnx package --vulnerable --include-transitive:
  không phát hiện vulnerable package ở mọi project theo nguồn NuGet hiện tại.
- team-feature check: 6 feature consistent; git diff --check sạch.

Không stage .env, owner.json hoặc snapshot dữ liệu trong artifacts/diet.
Các dependency ngoài Diet vẫn giữ nguyên trạng thái open ở handoffs.

## Q10 và snapshot target — 2026-11-02

- Diet API build warnings-as-errors: 0 warning/error.
- Diet integration suite: 27 passed, 0 failed (PostgreSQL/RabbitMQ testcontainers).
- Sau thay đổi error mapping cuối: 6 scoped client/snapshot tests passed; timeout
  504 và unavailable 503 khác missing explicit. Client test dùng CallInvoker,
  không claim gọi Progress HTTP/2 thật.
- PostgreSQL snapshot test cả nam/nữ: 70kg/175cm/30 tuổi/factor1.55, BMR
  1648.75/1482.75 và TDEE2555.5625/2298.2625; clear/change-tracking bằng scope
  mới, đọc lại input/result/source; manual replacement giữ ID và clear snapshot.
- Migration Up chỉ thêm 11 nullable columns vào daily_diet_targets; không backfill
  số cân nặng giả cho target cũ. Snapshot generator EF được review.
- EF has-pending-model-changes: none; format đã apply ở Diet project;
  git diff --check exit0. Chưa chạy whole-solution lint/check cho push.
- Chưa có public calculate endpoint: Identity chỉ có GetUserHeight; full profile
  và cơ chế map activity enum vẫn cần canonical contract. Internal CalculateAsync
  nhận profile đã resolve, không expose dữ liệu tự khai từ public HTTP.
- Chưa commit/push, chưa triển khai migration vào môi trường dùng chung.

## Meal AI adapter — 2026-11-02

- Diet build --no-restore -warnaserror: 0 warning, 0 error.
- Full Diet integration suite: **32 passed, 0 failed, 0 skipped**.
- Ba test adapter mới dùng CallInvoker kiểm tra request wire: chỉ known allergen
  metadata + ownership hợp lệ, food ID, serving100g, calorie130 từ catalog thay
  vì999 do response AI gửi; unknown response ID bị502. Verified=false giữ nguyên.
- Unavailable503 và deadline504 có code riêng, không biến thành empty success.
- Format scoped Diet apply exit0, git diff --check exit0; Compose config --quiet exit0.
- Không claim full server Recommendation/GoogleAI E2E hay latency<3s. Chưa có
  public meal-AI endpoint; profile provider Identity vẫn là dependency thật.
- Không commit/push trong phase này; source mới cùng các thay đổi Q10/snapshot
  đang ở working tree feat/diet-service. Handoff Đức đã ghi mapping hiện thực.

## Kiểm chứng theo yêu cầu developer — 2026-11-02

Release: Diet integration32/32, unit12/12, không failed/skipped.
Probe riêng artifacts/diet-weight-probe/Probe.csproj (ignored) tham chiếu client
Diet qua assembly alias và fixture server Progress hiện có. Probe không thêm
import liên service vào source sản phẩm. PostgreSQL17 testcontainer riêng,
ProgressGrpcService thật trên Kestrel HTTP/2 loopback; Diet ProgressWeightProvider
thật gọi qua GrpcChannel. PASS1/1 (13s):

- Chưa có metric -> null (no_record_found).
- 70.50kg rồi72kg -> trả72kg; metric tương lai99kg không được chọn, dù asOf tương lai.
- asOf lịch sử ->70.50kg và đúng sourceMetricId.
- Metric90kg của user khác không lọt; user chưa ghi và asOf trước record đều null.

Tổng lượt báo cáo45 test/probe passed. Không chạy full solution hay GoogleAI thật.
Compose hiện không chạy service; probe xác minh client/server trên HTTP/2 thật
và DB container, không chứng minh network/rollout Compose production hoặc full
Identity → Diet → target HTTP flow. Profile trong probe metric seed là test data,
không tạo profile giả cho API production. File probe giữ trong artifacts để tái lập.

## Hoàn thiện profile/calculate/meal AI — 2026-11-02

Baseline đã merge origin/main tới 66cd1fb (PR#15 Identity). Thay đổi phase này
chưa commit/push. Không chạy Spec-kit orchestrator, không gửi tin nhắn thay Huy.

### Negative control và scoped tests

- Ba ProfileFlowsTests trước khi có endpoint: 3 failed vì HTTP404, đúng lý do.
- Sau triển khai: ProfileClientTests + ProfileFlowsTests 8/8; sau bổ sung test
  budget, metadata, ownership/allergen và auth mới, toàn Diet integration44/44.
- Release Diet.UnitTests12/12, Identity.IntegrationTests21/21. Không skip.
- Swagger: 8 operation / 6 path / 16 DTO / 100 field; target calculate tái dùng
  UpdateTargetRequest/TargetResponse, ba DTO AI mới 7+11+7 field.
- Tests assert snapshot BMR1648.75/TDEE2555.5625, sourceMetricId/70kg/age30 đã
  materialize từ PostgreSQL; thiếu weight trả422 và không tạo target; summary
  meal130kcal → protein remaining122; loại unknown/foreign-owned/allergen foods.
- Metadata legacy absent →503 diet.profile_incomplete. Dependency chậm bị hủy
  theo budget và trả504 diet.ai_timeout. Client profile phân biệt missing422,
  timeout504, unavailable503, malformed502. Auth áp dụng cả hai route mới.

### Probe xuyên service thật, tách khỏi production references

Lệnh local: dotnet test artifacts/diet-weight-probe/Probe.csproj -c Release
--no-restore -m:1 --filter FullyQualifiedName~ProfileProbe.

1/1 passed: HTTP Diet (WebApplicationFactory/JWT) gọi IdentityGrpcService và
ProgressGrpcService thật qua Kestrel HTTP/2, PostgreSQL riêng từng service;
RecommendationStartup thật dùng pgvector PostgreSQL riêng. Profile/cân nặng
được seed qua DbContext của đúng service trong test, không truy cập DB chéo trong
runtime. POST calculate assert BMR/TDEE chính xác; GET AI assert đúng foodId,
130kcal từ catalog, nonempty citations, verified=false và disclaimer.

AI/embedding test doubles tất định, corpus fixture tổng hợp không phải tài liệu
y tế đã kiểm chứng. Đo request AI370.8567ms (<3s), không suy ra SLA Google AI thật.
Fixture Recommendation sẵn có sinh citation rỗng bị Diet từ chối502; probe dùng
fixture grounded để kiểm chứng đường hợp lệ, không nới validation production.
Probe nằm artifacts/ gitignored, không thêm project reference chéo vào source
production hoặc solution. Log tại TEMP/diet-profile-realgrpc-grounded.log.

Tổng phiên cuối: 44 Diet integration +12 Diet unit +21 Identity integration
+1 real gRPC probe =78 passed,0 failed,0 skipped. Các lần build/probe bị lỗi
fixture path/SDK resource đã sửa hoặc chạy lại; không tính là kết quả cuối.

### Gate và giới hạn

Máy không có make, dùng lệnh dotnet tương đương. dotnet build longevia-be.slnx
-warnaserror sau restore:0 warning/0 error. Lần --no-restore đầu fail Progress
unit do assets cũ sau pull; restore giải quyết, không sửa source Progress.
Compose config --quiet và git diff --check thành công. Format-check lần đầu
báo EOL CRLF từ checkout Windows và formatting Diet; được chuẩn hóa theo
.editorconfig, kết quả chốt ghi bên dưới.

Chưa chạy full make check (toàn test solution/vulnerability scan), chưa push,
chưa rollout Compose hay gọi Google AI thật. Deploy Identity với field5/6 trước
AI; server cũ vẫn tính BMR nhưng AI fail closed. Nguyên review mapping bổ sung,
Đức review latency/mapping; xem handoffs/README.md.

Ghi chú build tooling: project Worker Web SDK hiện chưa loại tests/** khỏi Content,
đã thấy copy test bin/obj lồng nhiều cấp và MSB3026 khi chạy lại Release. Không sửa
Worker trong phạm vi Diet; đã ghi yêu cầu Duy review ở handoffs/dev5-duy.md.
Các test cuối phía trên đều đã kết thúc xanh. Khi kiểm chứng nhiều project trên
máy16GB, chạy tuần tự hoặc -m:1 để tránh MSBuild hết tài nguyên.

Kết quả chốt lint: dotnet format longevia-be.slnx --no-restore exit0;
dotnet format longevia-be.slnx --verify-no-changes --no-restore exit0.
Chỉ chuẩn hóa EOL/format theo .editorconfig; git diff nội dung ngoài các path
Diet/Identity mapping/contract/tài liệu đã khai báo không có thay đổi nghiệp vụ.
Build toàn solution sau restore đã0 warning/0 error; fast gate xanh.

## Close-out trước push theo yêu cầu developer

Dùng nhánh feature đã bind feat/diet-service theo quy định một feature/một
nhánh; không tạo feature/nhánh song song cho cùng bộ tasks. Stage đúng57 file
Diet, profile contract/Identity mapping và tài liệu; không stage thay đổi EOL
thuần của service khác, không stage .env hay artifacts.

Máy thiếu make, hoàn tất đúng bốn bước tương đương make check:

- dotnet format longevia-be.slnx --verify-no-changes --no-restore: exit0.
- dotnet build longevia-be.slnx -c Release -warnaserror -m:1: exit0,
  0 warning/0 error.
- dotnet test longevia-be.slnx -c Release --no-build -m:1: 9 project,
  855 passed/0 failed/0 skipped. TRX nằm artifacts/diet-closeout (gitignored).
  Diet44+12, Identity21+67, Progress151+108, Recommendation64+372, Worker16.
- dotnet list longevia-be.slnx package --vulnerable --include-transitive:
  exit0, không phát hiện package dễ tổn thương theo sources hiện tại.

team-feature check và git diff --cached --check đều qua. Giới hạn live Google AI
và rollout đã nêu ở trên vẫn giữ nguyên; gate xanh không thay kiểm chứng đó.
