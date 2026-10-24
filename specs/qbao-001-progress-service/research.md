# Nghiên cứu và sổ quyết định

Ngày: 2026-10-24. Base đã fetch và đối chiếu remote HEAD/main:
`6d3f7550a531b6a3a02cbd327361648d34a61968` tại
[repository](https://github.com/fa26-prn232-group2/longevia-be).
Nhánh có sẵn `feat/progress-service` cùng base, working tree ban đầu sạch;
không cần merge/rebase hoặc tạo branch khác. Registry chưa có Progress; helper cấp `qbao-001`.

## Kết quả xác minh

| Bằng chứng | Kết luận |
| --- | --- |
| [identity.proto](../../contracts/identity/v1/identity.proto) | Service rỗng, chưa có RPC lấy chiều cao. |
| [ProfileResponse](../../src/Services/Identity/Identity.Application/Common/Dtos/ProfileResponse.cs), [UpdateProfileRequest](../../src/Services/Identity/Identity.Application/Features/Users/UpdateProfileRequest.cs) | HeightCm là int; chỉ có REST user profile, không được Progress gọi HTTP nội bộ. |
| [UserProfileUpdatedIntegrationEvent](../../src/Services/Identity/Identity.Application/Common/Events/UserProfileUpdatedIntegrationEvent.cs) | Chỉ UserId và UpdatedAt; không có height hoặc schema version. Đây không phải event snapshot chiều cao đã duyệt. |
| [event index](../../contracts/events/v1/README.md), [Shared Events](../../src/Shared/Events) | Canonical index mới có registration/password-reset; không có meal/profile-height/weekly-digest contract. |
| [src/Services](../../src/Services) | Chưa có thư mục Diet hoặc Progress ở base đã xác minh. |
| [recommendation.proto](../../contracts/recommendation/v1/recommendation.proto) | Đã có GenerateWorkoutPlan và EvaluateLongevityHabits; RPC tồn tại không đồng nghĩa Progress được gọi nếu chưa có ADR/contract direction được duyệt. |
| [Nginx](../../deployments/nginx/nginx.conf) | Đã có route /api/progress/ với prefix stripping; không cần mặc định sửa route. |
| [Compose](../../deployments/docker-compose.yml), [Prometheus](../../deployments/prometheus/prometheus.yml) | Progress chưa có service thực/scrape job; Phase 2 phải phối hợp Duy. |
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

## Câu hỏi và đề xuất chưa được phê duyệt

Tất cả hàng Q dưới đây là **PENDING**, kể cả phương án khuyến nghị. User đã chọn giữ
chúng để team review trước triển khai; không yêu cầu invent default để làm gate spec xanh.
Khi duyệt phải ghi người duyệt, ngày, phiên bản contract và cập nhật spec/plan/tasks
cùng lúc. Không có xác nhận nào của Nguyên/Huy/Duy/Đức được giả định trong Phase 1.

| ID | Câu hỏi cụ thể và phương án đề xuất | Phương án khác / trade-off | Người cần xác nhận / phần bị chặn |
| --- | --- | --- | --- |
| Q01 | Ngày theo timezone nào, đổi timezone có đổi quá khứ không? Đề xuất timezone IANA user, snapshot ngày/zone lúc ghi, không tự re-bucket lịch sử. History nhận ngày YYYY-MM-DD với khoảng đầu ngày inclusive, đầu ngày sau toDate exclusive. | UTC đơn giản nhưng lệch ngày user; Asia/Ho_Chi_Minh toàn sản phẩm đơn giản nhưng giới hạn thị trường; dynamic re-bucket sửa quá khứ và phức tạp meal sync. Chưa có nguồn zone approved. | Bảo + Huy + Nguyên; water/day, sleep/day, filters, streak và weekly windows Phase 4–6. |
| Q02 | Lấy height thế nào và khi thiếu thì sao? Đề xuất RPC đọc profile tối thiểu do Nguyên sở hữu, height int theo lựa chọn developer (chờ Nguyên reconcile API số thực); snapshot giá trị tại lần ghi. Đề xuất thiếu height reject 422 với mã Progress riêng, timeout/unavailable 503, không lưu metric dở. | Event snapshot cho đọc độc lập nhưng eventual consistency, backfill/version/delete cần contract mới. RPC fresh nhưng coupling availability. Không lấy qua HTTP/DB, không thêm HeightCm vào request hiện hành. Historical backdate dùng height lúc ghi hay effective-time phải được duyệt; đề xuất height lúc ghi có provenance. | Nguyên + Bảo + lead ADR; BMI/metrics Phase 5. |
| Q03 | Custom target, timestamp và retry dùng policy nào? Đề xuất 1–24 giờ, cố định 16/18/20 phải khớp target, không nhận future start/end, end >= start; elapsed/remaining tính giây, response giờ/phút có precision thống nhất. Không tự complete khi quá target. | Cho target dài hơn cần product review; timer client tự tính giảm traffic nhưng phải server làm nguồn thời gian. Đề xuất retry key scoped user+operation, cùng key/cùng payload replay, khác payload 409; key retention 24 giờ chưa duyệt. Start mới khi active 409; end đã completed cùng end replay 200, khác end 409; no current 200 null chưa duyệt. | Bảo; Fasting Phase 3. |
| Q04 | Giới hạn biomarkers, BMI category và precision là gì? Đề xuất fat >0 đến 100%, waist >0 đến 300 cm; huyết áp đi cặp, systolic 50–250, diastolic 30–150 mmHg, systolic > diastolic; RHR 25–250 bpm. Đề xuất bảng BMI <18.5 Underweight, <25 NormalWeight, <30 Overweight, còn lại Obese; phân loại giá trị chưa làm tròn, hiển thị 2 chữ số. | Các ngưỡng chỉ là policy đề xuất để review, không phải tư vấn/chuẩn y khoa đã được repo phê duyệt. Có thể chọn bộ ngưỡng khác; cấm hardcode lúc implement trước duyệt. Đề xuất không nhận loggedAt tương lai, cho backdate có snapshot provenance. | Bảo/product; validation/category Phase 5. |
| Q05 | Target nước, giới hạn log và sleep policy nào? Đề xuất default 2500 ml cố định MVP, chưa có endpoint chỉnh target; amountMl integer 1–5000, percent không cap 100. Sleep end > start, tối đa 24 giờ, quality đúng enum, gán ngày wake; cho nhiều giấc nhưng không overlap, timestamp không tương lai. | Default 2000 cũng nằm trong PRD; user target cần persistence/endpoint mới. Gán sleep day theo start khác wake; overlap cho phép có thể double-count, cần aggregation policy khác. | Bảo/product; Water/Sleep Phase 4. |
| Q06 | Initial/current/baseline tuần trước, current streak và cửa sổ thống kê chọn thế nào? Đề xuất initial metric sớm nhất, current mới nhất (tie Id), baseline mới nhất tại/trước now-7 calendar days, thiếu baseline trả delta null. Current streak chạy đến hôm nay nếu active, nếu không neo hôm qua, gap hơn trả 0; best dài nhất mọi lịch sử. | Initial profile weight không có trong API profile hiện tại; gần nhất cùng ngày tuần trước khác cutoff. Đề xuất week từ thứ Hai 00:00 theo Q01, completed = mọi phiên đã end (không chỉ goal reached), count theo endedAt; sleep mean giấc kết thúc trong 7 ngày lịch gồm hôm nay, mẫu rỗng null. Cần duyệt số giấc hay ngày làm mẫu số. | Bảo/product; Dashboard Phase 6. |
| Q07 | Wire pagination giữ pageNumber/totalCount hay dùng page/totalItems? Đề xuất Progress-local DTO adapter giữ wire API cũ và query pageNumber, nội bộ PagedResult; không đổi Shared. Normalize page >=1, pageSize 1..100, default 20 theo system-design. | Dùng thẳng PagedResult đổi client field, cần contract update được duyệt. Count phải thật trước Skip/Take, order loggedAt desc + Id desc. | Bảo + frontend/lead; history Phase 5. |
| Q08 | Ownership error, current empty và input status chốt ra sao? Đề xuất userId không có/invalid → 401; phiên khác user → 404; auth thiếu/sai/expired/issuer/audience/algorithm/kid không hợp lệ → 401; validation 400 theo D07. | 403 bộc lộ tồn tại resource; ADR còn TODO mô hình AuthZ chung. Shared hiện fallback current key nếu thiếu kid và nhánh thiếu public key không fail-start; không mặc định tái sử dụng mà claim đáp ứng stricter contract. Chờ lead xác nhận remediation thuộc Shared, không sửa ở feature này. | Bảo + Nguyên/lead; auth foundation và ownership tests. |
| Q09 | Diet cung cấp active day/add-edit-delete/backfill thế nào; dashboard khi thiếu data trả gì? Đề xuất versioned meal lifecycle event + snapshot/backfill query do Huy sở hữu; Progress lưu per-log revision+tombstone, union ngày với metric. | gRPC query active dates đơn giản projection nhưng synchronous availability/latency và cần ADR direction; event rẻ khi đọc nhưng cần ordering/backfill/retention. Đề xuất dashboard lỗi 503 hoặc explicit completeness field qua contract review, không trả streak 0 như đủ dữ liệu. | Huy + Bảo + lead; full streak Phase 6. |

## Phân loại công việc

| Nhóm | Phần việc | Điều kiện |
| --- | --- | --- |
| A — độc lập liên service | Skeleton Progress, Clean Architecture, database riêng, health/metrics, invariant một active, timer arithmetic, water/sleep persistence | Không cần hợp đồng Identity/Diet; task vẫn chờ policy Q tương ứng và hạ tầng Duy. |
| B — thống nhất hợp đồng trước | BMI height, meal streak, pagination wire, DTO thiếu, ownership/kid, timezone | PENDING; không biến proposal thành schema chính thức. |
| C — ngoài phạm vi hiện tại | AI workout/longevity, gửi digest, sửa Identity/Diet/Shared/Gateway/Worker | Xem deferred; Phase 1 không sửa source nào. |

## Làm rõ và chất lượng spec

Đã hỏi và nhận hai quyết định trong phiên sau khi có owner; cả hai được ghi ở spec.
Functional scope rõ; data/interaction/edge cases/integration còn PENDING có ID,
owner, lựa chọn và phase bị chặn; nonfunctional target có căn cứ PRD nhưng môi trường
benchmark chưa đo. Security ownership là bắt buộc, shape error chờ duyệt.
Không tuyên bố spec không còn ambiguity. Theo yêu cầu trực tiếp của developer,
tiếp tục lập kế hoạch có điều kiện; không dùng readiness của tài liệu để mở Phase 2.
