# Nghiên cứu và sổ quyết định

## Cập nhật Phase 2 và Quyết định Xác nhận từ Nguyên — 2026-10-25

PR #9 MERGED, merge commit `2addfea063cbe5753dcf0ea4b925e0edfcda3acc`. Tiếp đó PR #10 của Identity đã MERGED vào origin/main tại `2e249a29531bee383402970d2f85c279a292366e`, hoàn thiện profile contract phase. Nhánh `feat/progress-service` độc lập, sạch, mergeable và không có xung đột.

### Quyết định xác nhận từ Nguyên (Owner Identity Service)

1. **Q08 — JWT / Authentication Policy: APPROVED (Bảo + Nguyên)**
   - Phản hồi xác nhận từ Nguyên: *"Đồng ý thống nhất policy strict như Progress: token phải có kid hợp lệ, sub phải là GUID và service phải fail-start nếu thiếu public key. Giữ guard hiện tại ở Progress; sửa Shared Auth trong PR riêng."*
   - **Yêu cầu bảo mật đã thống nhất**:
     - Token JWT dùng ES256, bắt buộc `kid` hợp lệ (missing kid hoặc unknown kid đều trả 401).
     - Claim `sub` bắt buộc phải là một GUID hợp lệ khác rỗng (Guid.Empty, chuỗi không phải GUID hoặc thiếu sub đều bị từ chối 401).
     - Signing public key SPKI PEM P-256 là bắt buộc; thiếu cấu hình public key phải fail-start ngay khi khởi động.
     - Progress chỉ dùng public key; private key chỉ do Identity nắm giữ (cấu hình private key vào Progress sẽ fail-start).
   - **Phân định triển khai và trạng thái**:
     - *Q08 Policy Decision*: **APPROVED** bởi Bảo + Nguyên.
     - *Progress Local Guard*: **HOÀN TẤT & ĐÃ KIỂM CHỨNG** qua 14 kịch bản `AuthTests.cs` và `FoundationOptionsTests.cs` (100% pass trên cả local và CI).
     - *Shared Auth Remediation*: **EXTERNAL FOLLOW-UP** do Nguyên sở hữu và triển khai trong PR riêng; không sửa `src/Shared/Auth/**` trong PR #11 của Progress.
     - *Cross-service Compatibility*: Token sinh bởi `Es256JwtTokenGenerator` của Identity tương thích hoàn toàn với Progress guard (ES256, có `kid`, `sub` là user Guid).

2. **Q02 — Height for BMI & Reconciliation PR #10: APPROVED HƯỚNG KIẾN TRÚC gRPC READ**
   - Phản hồi xác nhận từ Nguyên: *"Chọn hướng gRPC read profile/height từ Identity để Progress lấy chiều cao hợp lệ khi tính BMI và lưu snapshot. Identity đã hoàn thành phần của mình; có thể chốt contract này để Bảo triển khai phía liên quan."*
   - **Thống nhất kiến trúc**:
     - Progress sẽ lấy chiều cao hồ sơ người dùng từ Identity thông qua giao thức gRPC read nội bộ đồng bộ khi ghi nhận chỉ số cơ thể.
     - Chiều cao nhận được dùng để tính BMI và lưu snapshot bất biến (`HeightUsedCm`, `Bmi`, `BmiCategory`) trong `BodyMetricLog`. Chiều cao profile thay đổi sau này trong Identity KHÔNG làm thay đổi BMI lịch sử (tuân thủ BR-05).
     - Kiểu dữ liệu chiều cao: `decimal`, khớp với PR #10 của Identity (`HeightCm` dạng `decimal`, cột DB `numeric(5,2)`, validation 50.00–250.00 cm).
     - **Hiện trạng & Giới hạn**: Canonical RPC schema trong `contracts/identity/v1/identity.proto` hiện chưa được định nghĩa/triển khai. Schema cụ thể (protobuf message, deadline, xử lý profile chưa có chiều cao) sẽ được thống nhất trước khi triển khai Phase 5. KHÔNG tạo RPC giả hay adapter trong Phase 2.
     - Sự kiện `UserProfileUpdatedEvent` (PR #10) chỉ mang `UserId` và `OccurredOn`, không mang chiều cao và không thay thế cho gRPC read.

Nguồn runtime của foundation được mô tả ở
[progress-service.md](../../docs/system-design/progress-service.md).

## Lịch sử Phase 1 / corrective

Ngày: 2026-10-24. Corrective đã fetch và đối chiếu remote HEAD/main:
`418d41b82c4eca846995ed3d505736f0ba7b0c5e` (PR #8 đã merge) tại
[repository](https://github.com/fa26-prn232-group2/longevia-be).
Nhánh `feat/progress-service` ở `70d4faa2cf9d527333f0f9431ff0eec0de99dfb3`,
ancestor base local vẫn `6d3f7550`; working tree đầu corrective sạch. Đọc source Diet
bằng `git show origin/main:<path>`; không merge/rebase hoặc sửa source trong corrective.
Evidence lịch sử trước Diet và CI merge ref được tách ở verification.md.

## Kết quả xác minh

| Bằng chứng | Kết luận |
| --- | --- |
| [identity.proto](../../contracts/identity/v1/identity.proto) | Service rỗng, chưa có RPC lấy chiều cao. |
| [ProfileResponse](../../src/Services/Identity/Identity.Application/Common/Dtos/ProfileResponse.cs), [UpdateProfileRequest](../../src/Services/Identity/Identity.Application/Features/Users/UpdateProfileRequest.cs) | Đã reconcile theo PR #10: HeightCm là decimal (DB numeric(5,2), 50.00–250.00 cm); chỉ có REST user profile, không được Progress gọi HTTP nội bộ. |
| [UserProfileUpdatedEvent](../../src/Shared/Events/UserProfileUpdatedEvent.cs) | Đã cập nhật theo PR #10: Chỉ UserId và OccurredOn (SchemaVersion 1); không có height. Không thay thế gRPC read profile/height. |
| [event index](../../contracts/events/v1/README.md), [Shared Events](../../src/Shared/Events) | Canonical index mới có registration/password-reset và UserProfileUpdatedEvent; không có meal/profile-height/weekly-digest contract. |
| `src/Services/Diet/` trên origin/main; `docs/system-design/diet-service.md`; `specs/ivorlee-001-diet-service/backend-integration-guide.md` | Diet độc lập đã có Food Catalog, Manual Nutrition Targets, Meal Logging, Daily Summary, EF/migrations diet_db, authentication, tests và Docker/metrics. Chưa có source Progress. Các path Diet thuộc base mới, chưa nằm trên checkout branch cũ. |
| `Diet.Domain/Entities/MealLog.cs`, `Diet.Application/Features/Meals/LogMealRequest.cs`, `MealUseCases.cs`, `DailySummaryUseCase.cs`, `Diet.Api/Endpoints/MealEndpoints.cs` dưới `src/Services/Diet/` ở origin/main | MealLog.Date là DateOnly client cung cấp; CreatedAt từ server UTC. POST /meals tạo log mới; summary không truyền date dùng ngày UTC. Không có REST update/delete meal hoặc canonical meal activity event. |
| `src/Services/Diet/Diet.Application/Features/Targets/TargetResponse.cs`, `specs/ivorlee-001-diet-service/deferred.md` ở origin/main | Bmr/Tdee nullable, mapper hiện trả null; tích hợp profile Identity + cân nặng Progress còn hoãn. Diet chưa có producer outbox cho streak, Progress chưa có consumer/projection/reconciliation, bootstrap/backfill hoặc E2E Diet → Progress. |
| [recommendation.proto](../../contracts/recommendation/v1/recommendation.proto) | Đã có GenerateWorkoutPlan và EvaluateLongevityHabits; RPC tồn tại không đồng nghĩa Progress được gọi nếu chưa có ADR/contract direction được duyệt. |
| [Nginx](../../deployments/nginx/nginx.conf) | Đã có route /api/progress/ với prefix stripping; không cần mặc định sửa route. |
| [Compose](../../deployments/docker-compose.yml), [Prometheus](../../deployments/prometheus/prometheus.yml) | Progress đã có service thực chiseled/scrape job static_configs; kiểm chứng smoke pass 100%. |
| [WeeklyDigestData](../../src/Services/Worker/Templates/WeeklyDigestData.cs), [Worker Program](../../src/Services/Worker/Program.cs) | Đã có template dữ liệu email tuần, nhưng chưa có weekly consumer/binding/canonical event; không import template Worker thành contract Progress. |

## Quyết định có căn cứ

- D01: Chỉ `progress_db`, không đọc identity_db/diet_db, không import source nội bộ service khác. Lý do: Constitution I. Loại phương án dùng shared DB/join.
- D02: REST qua Nginx, liên service đồng bộ chỉ gRPC, async chỉ RabbitMQ + versioned contract + transactional outbox + consumer idempotent. Lý do: Constitution II–IV. RPC direction mới còn cần ADR theo service-boundaries.
- D03: BMI snapshot bất biến theo BR-05; lưu chiều cao sử dụng cùng kết quả, không tính lại lúc đọc. Loại cách luôn dùng chiều cao profile mới.
- D04: Streak = hợp ngày meal log và metric log (PRD US-PROG-04 AC-2). Không thêm nước/ngủ/fasting vào điều kiện.
- D05: Weight delta so tuần trước theo PRD, không so initial theo ví dụ API. Chưa đủ căn cứ chọn chính xác bản ghi mốc tuần trước.
- D06: Pagination nội bộ dùng PagedResult với page/totalItems theo system-design VI (thẩm quyền cao hơn API). Shared khớp pattern; API nói pageNumber/totalCount. Không sửa Shared để khớp ví dụ thấp hơn; wire adapter đề xuất ở Q07 cần review, không âm thầm đổi contract.
- D07: Validation input theo system-design III–IV trả 400 validation.failed; API ví dụ chung 422 không tự ghi đè chuẩn cao hơn. Business 422 riêng nếu được duyệt; errors dùng AppException/Problem Details, có traceId, không lộ stack.
- D08: UTC DateTimeOffset cho instant; chưa có quy tắc calendar day/timezone được duyệt. Không lấy Asia/Saigon của máy làm product default.
- D09: JWT ES256 verify tại Progress bằng public key/kid, không private key, không gọi Identity để validate token từng request. Quyền truy cập chỉ chủ thể user của dữ liệu theo yêu cầu feature.

## Sổ quyết định và các câu hỏi

Các quyết định đã được duyệt ghi nhận rõ người duyệt; các câu hỏi liên quan phase sau tiếp tục giữ PENDING để review trước triển khai tương ứng. Không có Technical Lead được chỉ định; thẩm quyền thuộc về service owners (Bảo, Nguyên, Huy, Duy).

| ID | Câu hỏi cụ thể và phương án đề xuất | Phương án khác / trade-off | Người xác nhận / Trạng thái |
| --- | --- | --- | --- |
| Q01 | Ngày theo timezone nào, đổi timezone có đổi quá khứ không? Diet hiện nhận DateOnly business date từ client và summary mặc định UTC; CreatedAt UTC riêng. Streak dùng meal business date, không suy từ publish/receive/processed timestamp. Đề xuất body metric snapshot ngày/zone, tương thích business date Diet và không tự re-bucket lịch sử; history nhận YYYY-MM-DD, đầu fromDate inclusive và đầu ngày sau toDate exclusive theo zone duyệt. | User timezone IANA cần nguồn zone và quyết định tương thích DateOnly đã có; UTC default của Diet là implementation evidence, chưa phải product policy approved cho mọi service. Asia/Ho_Chi_Minh hoặc dynamic re-bucket đều cần review/migration semantics. | Bảo + Huy + Nguyên; PENDING, không chặn skeleton/foundation, chặn day logic Phase 4–6. |
| Q02 | **APPROVED HƯỚNG KIẾN TRÚC gRPC READ PROFILE/HEIGHT (Nguyên + Bảo)**. Progress lấy chiều cao qua gRPC read nội bộ đồng bộ từ Identity khi tính BMI; height kiểu `decimal` (khớp PR #10 `numeric(5,2)`, 50.00–250.00 cm). Snapshot BMI bất biến theo BR-05, thay đổi profile sau này không đổi BMI lịch sử. Schema RPC cụ thể trong `contracts/identity/v1/identity.proto` PENDING trước Phase 5 (cần định nghĩa message, deadline, profile thiếu chiều cao). Không gọi HTTP nội bộ, không đọc DB chéo. | Event snapshot không chọn vì eventual consistency và có thể stale; gRPC read fresh được chọn. UserProfileUpdatedEvent (PR #10) không mang chiều cao. Historical backdate dùng height lúc ghi có provenance. | Nguyên (Identity provider) + Bảo (Progress consumer); **APPROVED hướng gRPC, schema PENDING trước Phase 5**. |
| Q03 | Custom target, timestamp và retry dùng policy nào? Đề xuất 1–24 giờ, cố định 16/18/20 phải khớp target, không nhận future start/end, end >= start; elapsed/remaining tính giây, response giờ/phút có precision thống nhất. Không tự complete khi quá target. | Cho target dài hơn cần product review; timer client tự tính giảm traffic nhưng phải server làm nguồn thời gian. Đề xuất retry key scoped user+operation, cùng key/cùng payload replay, khác payload 409; key retention 24 giờ chưa duyệt. Start mới khi active 409; end đã completed cùng end replay 200, khác end 409; no current 200 null chưa duyệt. | Bảo; Fasting Phase 3. |
| Q04 | Giới hạn biomarkers, BMI category và precision là gì? Đề xuất fat >0 đến 100%, waist >0 đến 300 cm; huyết áp đi cặp, systolic 50–250, diastolic 30–150 mmHg, systolic > diastolic; RHR 25–250 bpm. Đề xuất bảng BMI <18.5 Underweight, <25 NormalWeight, <30 Overweight, còn lại Obese; phân loại giá trị chưa làm tròn, hiển thị 2 chữ số. | Các ngưỡng chỉ là policy đề xuất để review, không phải tư vấn/chuẩn y khoa đã được repo phê duyệt. Có thể chọn bộ ngưỡng khác; cấm hardcode lúc implement trước duyệt. Đề xuất không nhận loggedAt tương lai, cho backdate có snapshot provenance. | Bảo/product; validation/category Phase 5. |
| Q05 | Target nước, giới hạn log và sleep policy nào? Đề xuất default 2500 ml cố định MVP, chưa có endpoint chỉnh target; amountMl integer 1–5000, percent không cap 100. Sleep end > start, tối đa 24 giờ, quality đúng enum, gán ngày wake; cho nhiều giấc nhưng không overlap, timestamp không tương lai. | Default 2000 cũng nằm trong PRD; user target cần persistence/endpoint mới. Gán sleep day theo start khác wake; overlap cho phép có thể double-count, cần aggregation policy khác. | Bảo/product; Water/Sleep Phase 4. |
| Q06 | Initial/current/baseline tuần trước, current streak và cửa sổ thống kê chọn thế nào? Đề xuất initial metric sớm nhất, current mới nhất (tie Id), baseline mới nhất tại/trước now-7 calendar days, thiếu baseline trả delta null. Current streak chạy đến hôm nay nếu active, nếu không neo hôm qua, gap hơn trả 0; best dài nhất mọi lịch sử. | Initial profile weight không có trong API profile hiện tại; gần nhất cùng ngày tuần trước khác cutoff. Đề xuất week từ thứ Hai 00:00 theo Q01, completed = mọi phiên đã end (không chỉ goal reached), count theo endedAt; sleep mean giấc kết thúc trong 7 ngày lịch gồm hôm nay, mẫu rỗng null. Cần duyệt số giấc hay ngày làm mẫu số. | Bảo/product; Dashboard Phase 6. |
| Q07 | Wire pagination giữ pageNumber/totalCount hay dùng page/totalItems? Đề xuất Progress-local DTO adapter giữ wire API cũ và query pageNumber, nội bộ PagedResult; không đổi Shared. Normalize page >=1, pageSize 1..100, default 20 theo system-design. | Dùng thẳng PagedResult đổi client field, cần contract update được duyệt. Count phải thật trước Skip/Take, order loggedAt desc + Id desc. | Bảo + frontend/lead; history Phase 5. |
| Q08 | **APPROVED (Bảo + Nguyên)**. Thống nhất policy strict như Progress: token ES256 bắt buộc `kid` hợp lệ (missing/unknown kid -> 401), `sub` bắt buộc là GUID hợp lệ khác rỗng (sai/thiếu/empty -> 401), service bắt buộc fail-start nếu thiếu public key hoặc cấu hình private key. Bảo sở hữu Progress local guard (đã hoàn tất & pass 100% tests); Nguyên sở hữu remediation chung trong Shared Auth (external follow-up trong PR riêng). | Tái sử dụng Shared cũ bị từ chối vì thiếu kid vẫn pass và thiếu public key không fail-start. Q08 policy đã chốt; Progress guard độc lập đã đạt yêu cầu bảo mật Phase 2. | Bảo + Nguyên; **APPROVED**. |
| Q09 | BLOCKED/PENDING INTEGRATION: Diet đã có create meal, chưa có activity contract/outbox cho streak. Đề xuất contract versioned cho create + bootstrap ban đầu; dùng MealLog.Date, tách CreatedAt UTC và ProcessedAt UTC. Future update/delete/change-date cần revision/tombstone/reconciliation trước mở lifecycle; không giả endpoints đã có. | Event projection rẻ lúc đọc nhưng cần outbox producer, dedup và completeness; gRPC active dates coupling availability và cần ADR direction. Create-only không đủ nếu thiếu bootstrap log cũ; future lifecycle phải có version/migration/replay, không silently drop delete. Dashboard 503 hoặc explicit completeness field vẫn chờ duyệt. | Huy + Bảo + Duy; chặn full streak Phase 6, không chặn skeleton. |
| Q10 | PENDING APPROVAL — Latest Weight Contract for Diet BMR/TDEE. Đề xuất gRPC read do Progress sở hữu, Diet là consumer: chọn metric mới nhất có LoggedAt <= asOf, order LoggedAt/Id ổn định; response đề xuất weightKg, measuredAt (map LoggedAt), sourceMetricId, observedAt/provenance, presence. Cần quyết latest tại thời điểm đọc hay weight tại effectiveAt của target; đề xuất Diet snapshot lúc tính target, cập nhật weight không tự tính lại target cũ. | Không có metric: presence rõ, Diet giữ Bmr/Tdee null hoặc báo trạng thái được duyệt; không weight=0/default. Future measurement không thành latest, backdate chỉ thắng theo measuredAt, tie/provenance cần thống nhất. Đề xuất deadline tổng 2s, bounded retry read tối đa 1; unavailable là transport error, không đồng nhất missing; stale threshold/retention và Diet có dùng snapshot cũ không còn chờ duyệt. Event projection giảm coupling nhưng eventual/stale, cần outbox/version/bootstrap/reconcile, chưa chọn. Diet → Progress và Diet → Identity chưa được whitelist, cần lead ADR + canonical contract trước adapter; không DB/HTTP/import chéo. | Bảo + Huy; Nguyên cung cấp tuổi/ngày sinh, giới tính, chiều cao, mức vận động; T038 theo dõi quyết định; provider implementation feature sau khi approved, không chặn BMI core và không đưa thuật toán BMR/TDEE sang Progress. |

## Phân loại công việc

| Nhóm | Phần việc | Điều kiện |
| --- | --- | --- |
| A — độc lập liên service | Skeleton Progress, Clean Architecture, test scaffolding và wiring cấu trúc; business timer/water/sleep có policy riêng | Skeleton T002/T003 không chờ Q01/Q08; cần human giao phase và shared-file slot. Security acceptance vẫn chờ Q08; business date chờ Q01. |
| B — thống nhất hợp đồng trước | BMI height, meal streak, cung cấp weight cho Diet, pagination wire, DTO thiếu, ownership/kid, timezone | Q01–Q10 PENDING; không biến proposal thành schema chính thức. Q10 chỉ planning provider, thuật toán BMR/TDEE thuộc Diet. |
| C — ngoài phạm vi hiện tại | AI workout/longevity, gửi digest, sửa Identity/Diet/Shared/Gateway/Worker | Xem deferred; Phase 1 không sửa source nào. |

## Làm rõ và chất lượng spec

Đã hỏi và nhận hai quyết định trong phiên sau khi có owner; cả hai được ghi ở spec.
Functional scope rõ; data/interaction/edge cases/integration còn PENDING có ID,
owner, lựa chọn và phase bị chặn; nonfunctional target có căn cứ PRD nhưng môi trường
benchmark chưa đo. Security ownership là bắt buộc, shape error chờ duyệt.
Không tuyên bố spec không còn ambiguity. Theo yêu cầu trực tiếp của developer,
tiếp tục lập kế hoạch có điều kiện; không dùng readiness của tài liệu để mở Phase 2.
