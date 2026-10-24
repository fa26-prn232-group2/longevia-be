# Đặc tả tính năng: Progress Service

**Nhánh**: `feat/progress-service`
**Feature**: `qbao-001-progress-service`; owner: `qbao`; nghiệp vụ: Bảo
**Ngày tạo**: 2026-10-24
**Trạng thái**: Phase 1 đã merge PR #9; Phase 2 được giao, triển khai phần foundation độc lập; Q08 vẫn PENDING Bảo + Nguyên, chưa nghiệm thu toàn Phase 2.
**Đầu vào**: Đặc tả bốn story Progress và kế hoạch Phase 2–6; không tạo mã ứng dụng, migration hoặc sửa hợp đồng service khác.

## Bối cảnh

Nguồn: [PRD §2.3](../../docs/requirements/prd.md), [BRD BR-05](../../docs/requirements/brd.md),
[MVP](../../docs/overview/mvp-scope.md), [Constitution](../../.specify/memory/constitution.md).
Đây là đặc tả kết quả của Progress core; Phase 1 chỉ tạo tài liệu. Phân biệt yêu cầu
có căn cứ và đề xuất chưa duyệt trong [research.md](research.md). DTO/API chi tiết ở
[đề xuất hợp đồng](contracts/README.md); không sao chép thành canonical contract.

## Làm rõ

Corrective theo base `418d41b8` (PR #8): Diet đã có Food Catalog, Manual Targets,
Meal Logging create-only, Daily Summary, EF diet_db, auth/tests/Docker. Chưa có
meal activity contract/outbox và tích hợp projection/bootstrap/E2E với Progress.
Diet cần cân nặng Progress + profile Identity cho BMR/TDEE: Q10 chỉ lập kế hoạch
weight provider sau duyệt; thuật toán và consumer thuộc Diet, không thuộc core Progress.

### Phiên 2026-10-24

- Hỏi: Giữ quyết định thiếu căn cứ PENDING để team review trước Phase 2? → Đáp: Có; hoàn thiện kế hoạch, chặn task liên quan đến khi duyệt.
- Hỏi: Chiều cao số thực trong API và số nguyên trong source Identity xử lý thế nào? → Đáp: Đề xuất theo source hiện tại, chiều cao nguyên, vẫn chờ Nguyên duyệt thay đổi contract. Không sửa tài liệu chuẩn; đây chưa phải quyết định thay thế contract hiện hành.

## Kịch bản người dùng và kiểm thử

### US1 — US-PROG-01: Theo dõi chỉ số cơ thể (P1)

Người dùng ghi cân nặng và số đo, xem lịch sử cùng BMI tại thời điểm đo để theo dõi tiến độ.
Kiểm thử độc lập sau khi duyệt hợp đồng chiều cao: 70 kg và 175 cm cho BMI
xấp xỉ 22.86, theo precision chờ duyệt; đổi profile không đổi BMI đã lưu.

**Nghiệm thu**:

1. Cho A đã xác thực, khi ghi cân nặng 25–300 kg, `loggedAt` và chỉ số tùy chọn hợp lệ thì lưu đúng giá trị, trả định danh; ngoài khoảng không tạo bản ghi.
2. Cho chiều cao hợp lệ từ nguồn đã duyệt, khi ghi thì tính `weightKg / (heightCm/100)^2` và lưu snapshot chiều cao/BMI/category. Khi chiều cao profile đổi, bản ghi cũ không bị tính lại (BR-05).
3. Cho nhiều bản ghi, khi xem lịch sử thì mới nhất trước, lọc đúng khoảng ngày được duyệt và phân trang; cùng timestamp có thứ tự ổn định, bộ dữ liệu tĩnh không trùng/mất giữa các trang.
4. Cho B xác thực, ghi/xem chỉ tác động dữ liệu B; payload không được chọn user A.
5. Thiếu profile/chiều cao và lỗi transport là hai trường hợp riêng; không dùng chiều cao giả. Reject hay BMI rỗng chờ Q02, không tự chốt.

**Validation và edge cases**: cân nặng thiếu/không hữu hạn/ngoài biên; timestamp sai,
tương lai hoặc nhập lùi ngày; `fromDate > toDate`; trang rỗng; cùng timestamp.
Body fat, vòng eo, hai huyết áp và resting heart rate tùy chọn; khoảng giá trị,
quy tắc cặp huyết áp, bảng BMI category và làm tròn chờ Q04.
**Phụ thuộc**: Nguyên, Q01/Q02/Q04/Q07. **API/DTO**: ghi metrics và metrics/history,
request không có HeightCm; xem đề xuất hợp đồng.
**Test**: 25/300 và sát ngoài, hai user, BR-05, filter/date/page, profile thiếu/provider lỗi.

### US2 — US-PROG-02: Bộ đếm nhịn ăn (P1)

Người dùng start, xem tiến trình và end một phiên 16:8, 18:6, 20:4 hoặc Custom.
Giá trị: theo dõi thời lượng thực kể cả qua đêm; độc lập Identity/Diet.
Kiểm thử độc lập: start 20:00Z, target 16 giờ, end 12:15Z hôm sau cho 975 phút,
16.25 giờ, `Completed`, đạt mục tiêu.

**Nghiệm thu**:

1. Chưa có active, start hợp lệ tạo đúng một `InProgress`, mốc target tính từ start + duration.
2. Có 20 start đồng thời cùng user thì chỉ một active; không khóa nhầm user khác. Kết quả các request còn lại theo Q03, không chỉ kiểm tra trước rồi ghi thiếu bảo vệ cạnh tranh.
3. Sau 15 giờ của target 16: elapsed 15 giờ, remaining 1 giờ; vượt target remaining không âm. Không reset ở nửa đêm; tự kết thúc khi đạt target là đề xuất không áp dụng nếu chưa duyệt.
4. End trước target vẫn ghi thời lượng thực, chưa đạt; bằng/sau target đạt. End không được trước start.
5. B không end được phiên A hoặc đọc dữ liệu A; status che giấu 404 hay 403 chờ Q08.
6. Retry start/end không nhân tác dụng phụ theo Q03; test cùng request, khác payload, end hai lần và cạnh tranh end/start.

**Validation và edge cases**: protocol lạ, target Custom thiếu/0/âm/quá lớn,
protocol cố định không khớp target, timestamp tương lai, current rỗng, end đã hoàn tất.
Giới hạn Custom, retry key và response lỗi/current rỗng chờ Q03.
**Phụ thuộc**: foundation, Q03/Q08. **API/DTO**: start/end/current trong đề xuất hợp đồng.
**Test**: ví dụ 975 phút, 20 start, hai user, retry, target sát dưới/bằng/trên, qua ngày.

### US3 — US-PROG-03: Nước và giấc ngủ (P2)

Người dùng ghi lượng nước, xem tổng ngày và ghi giờ ngủ/thức cùng chất lượng.
Kiểm thử độc lập: cùng ngày đã duyệt ghi 350 + 650 ml cho 1000 ml;
23:00 đến 07:00 hôm sau với cùng offset cho 8 giờ ngủ.

**Nghiệm thu**:

1. Water log hợp lệ chỉ cộng vào ngày của user; ghi đồng thời không mất cập nhật, user khác không ảnh hưởng tổng.
2. Today trả ngày, consumed, target và phần trăm; chưa log consumed = 0. Target cụ thể chờ Q05 (PRD 2000–2500 ml, API ví dụ 2500 ml).
3. Sleep qua đêm tính từ hai thời điểm đầy đủ, không trừ giờ đồng hồ bỏ mất ngày; quality thuộc `Poor`, `Fair`, `Good`, `Excellent`.
4. Input lượng nước không dương, wake trước sleep hoặc quality sai bị từ chối theo policy đã duyệt; không tạo dữ liệu của người khác.
5. Hai log ở hai phía ranh giới ngày thuộc hai tổng khác nhau theo Q01; nếu chọn timezone có DST thì tính duration từ instant, test cả chuyển offset.

**Validation và edge cases**: số nước tối đa/kiểu số, target có sửa không,
phần trăm vượt 100, nhiều giấc/chồng lấn/trùng, duration quá dài, gán ngày theo
sleep hay wake, timestamp tương lai: Q01/Q05. Không suy đoán từ ví dụ.
**Phụ thuộc**: foundation, Q01/Q05/Q08; không cần Diet/Identity.
**API/DTO**: water/log, water/today, sleep/log; hai route sau chưa có schema chi tiết ở API spec.
**Test**: tổng 1000 ml, cạnh tranh ghi, ngày rỗng, hai user, 8 giờ qua đêm, quality sai, overlap theo policy duyệt.

### US4 — US-PROG-04: Dashboard và streak (P2)

Người dùng xem tiến độ cân nặng/BMI, streak và tổng fasting/sleep.
Kiểm thử độc lập phần Progress bằng dữ liệu định trước; streak hoàn chỉnh cần Diet
contract và tích hợp thật, fixture không chứng minh tích hợp đã hoàn tất.

**Nghiệm thu**:

1. Active day = có ít nhất một meal log HOẶC body metric log; có cả hai chỉ tính một ngày. Nước/ngủ/fasting không tạo active day.
2. Active business date 1,2,3,5 cho best = 3; current neo hôm nay/hôm qua theo Q06. Diet hiện tạo meal với DateOnly client cấp; không suy ngày từ CreatedAt UTC hoặc timestamp publish/receive/processed. Future delete/change-date cần contract mở rộng: chỉ bỏ ngày nếu không còn metric/meal và cập nhật cả ngày cũ/mới; không giả Diet đã có endpoints này.
3. Với initial 72 kg, tuần trước 70 kg và hiện tại 68.5 kg, delta so tuần trước là -1.5 kg, không lấy -3.5 kg từ initial. PRD cao hơn ví dụ API; cách chọn mốc tuần trước chờ Q06.
4. Current BMI lấy snapshot đã lưu; profile thay đổi không đổi BMI cũ. Không có số đo không được dựng số đo 0; null/shape rỗng chờ Q06.
5. Fasting hoàn tất tuần và average sleep có cửa sổ, cách đếm và làm tròn được duyệt Q06; test ranh giới tuần, tuần rỗng, hai user.
6. Meal event trùng, đảo thứ tự, đến muộn và backfill không nhân ngày; không công bố streak đầy đủ khi chưa có Diet data. Response thiếu dữ liệu chờ Q09.

**Validation và edge cases**: hôm nay chưa log, gap, cuối tháng/năm/tuần, đổi timezone,
log lùi ngày, delete cuối cùng, cùng timestamp, projection rebuild và dependency lỗi.
**Phụ thuộc**: US1/US2/US3; Huy + Q01/Q06/Q09; BMI cần Nguyên + Q02.
**API/DTO**: 8 field summary tại API §4.6, delta theo PRD; xem đề xuất hợp đồng.
**Test**: chuỗi 1,2,3,5; union meal/metric; add/edit/delete; duplicate/late/revision;
backfill/rebuild; dashboard có số liệu cụ thể và ownership.

## Yêu cầu

### Yêu cầu chức năng

| ID | Yêu cầu kiểm chứng được | Căn cứ / điều kiện |
| --- | --- | --- |
| FR-001 | Chỉ đọc/ghi dữ liệu chủ thể xác thực, không tin userId client. | BRD; Q08 chốt status. |
| FR-002 | Ghi cân nặng 25–300 kg, thời điểm, 5 chỉ số tùy chọn; invalid không lưu. | API §4.1; Q04. |
| FR-003 | Tính BMI từ chiều cao hợp lệ, lưu snapshot, không tính lại lịch sử. | PRD, BR-05; Q02. |
| FR-004 | Trả BMI category theo ngưỡng và precision được duyệt. | API có field; Q04. |
| FR-005 | Lịch sử mới nhất trước, filter ngày, phân trang ổn định. | PRD/API; Q01/Q07. |
| FR-006 | Start 4 protocol, lưu target, kiểm tra giới hạn Custom đã duyệt. | PRD; Q03. |
| FR-007 | Tối đa một active/user khi cạnh tranh; retry không nhân side effect. | Yêu cầu; Q03. |
| FR-008 | End ghi thời lượng thực và đạt/chưa đạt, kể cả qua ngày. | PRD/API. |
| FR-009 | Current trả trạng thái, elapsed, remaining từ cùng mốc hiện tại. | PRD; Q03. |
| FR-010 | Ghi nước và tổng theo ngày, không mất cập nhật cạnh tranh. | PRD; Q01/Q05. |
| FR-011 | Today trả target và phần trăm, chưa log consumed = 0. | PRD/API; Q05. |
| FR-012 | Ghi sleep/wake, duration, 4 quality; xử lý qua nửa đêm. | PRD; Q05. |
| FR-013 | Dashboard gồm current/initial weight, delta tuần trước và BMI snapshot. | PRD ưu tiên API; Q06. |
| FR-014 | Streak là hợp ngày meal/metric, current/best không đếm trùng. | PRD; Q01/Q06/Q09. |
| FR-015 | Tổng fasting hoàn tất tuần và average sleep theo cửa sổ được duyệt. | API; Q06. |
| FR-016 | Meal projection xử lý create, duplicate/late và bootstrap đúng; future edit/delete/change-date có revision/tombstone/reconcile trước mở lifecycle. | Q09, chặn tích hợp; Diet MVP hiện create-only. |
| FR-017 | Phân biệt UTC instant và ngày lịch; cùng quy tắc ngày cho tổng hợp. | Coding conventions; Q01. |
| FR-018 | Thiếu chiều cao/meal data báo đúng trạng thái, không giả dữ liệu. | Q02/Q09. |
| FR-019 | Phase 1 không đổi source, migration hoặc canonical contract. | Phạm vi yêu cầu. |

### Thực thể chính

- Body metric: user, thời điểm, cân nặng, chỉ số tùy chọn, snapshot chiều cao/BMI/category.
- Fasting session: user, protocol, target, start/end, trạng thái, duration thực.
- Water log: user, lượng, thời điểm và ngày được xác định theo policy.
- Sleep log: user, sleep/wake instant, quality, duration, ngày quy thuộc chờ duyệt.
- Activity day: hợp meal/metric; giữ định danh nguồn để delete không xóa nhầm hoạt động khác.
- Water target và dedup/projection chỉ tạo nếu phương án liên quan được duyệt.

## Tiêu chí thành công

- **SC-001**: 100% FR có task; cả 4 story có kịch bản kiểm chứng; không task implement nào hoàn thành ở Phase 1.
- **SC-002**: 20 start cùng user chỉ có 1 active; user khác không bị khóa nhầm.
- **SC-003**: Mọi thử nghiệm chéo 2 user đều không lộ/đổi dữ liệu người khác; invalid không được lưu.
- **SC-004**: Bộ dữ liệu 72/70/68.5 kg, 975 phút fasting, nước 1000 ml và ngủ 8 giờ cho kết quả đúng sau khi duyệt policy còn thiếu.
- **SC-005**: BMI cũ không đổi sau cập nhật chiều cao; duplicate/late meal không nhân active day.
- **SC-006**: Theo PRD, đọc p95 <150 ms, ghi p95 <250 ms và 100 yêu cầu/giây mỗi service; Phase 6 cố định môi trường/tập dữ liệu đo, Phase 1 chưa có kết quả thực đo.
- **SC-007**: Trước hoàn tất triển khai có E2E qua gateway, DB và broker thật assert giá trị cụ thể, negative control cho kiểm thử hành vi.

## Giả định

- Không dùng timezone máy dev làm mặc định sản phẩm, không mặc định profile luôn có chiều cao.
- Q01–Q10 trong research đều PENDING, chặn phần code liên quan; skeleton không cần Q01/Q08, security acceptance vẫn cần Q08 và date logic cần Q01. Q10 không chặn BMI core; review kế hoạch không đồng nghĩa approve contract.
- Phase 1 không đổi surface đang chạy, không đặt `READY_FOR_FRONTEND`/`READY_FOR_BACKEND`.
- AI, weekly digest delivery và sửa service khác được ghi trong [deferred.md](deferred.md).
