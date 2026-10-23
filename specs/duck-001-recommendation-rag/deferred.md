# Deferred — Recommendation Service với RAG tri thức sách "Chế độ ăn trường thọ"

Công việc phát hiện trong lúc build `duck/001/recommendation-rag` nhưng **không thuộc
phạm vi của feature này**. Không mục nào ở đây chặn việc đóng feature; không mục nào
là task của feature này.

File này tồn tại vì một ô chưa tick trong `tasks.md` mang nghĩa khác: `- [ ]`
nghĩa là *feature này chưa xong*. Việc thuộc feature khác, hoặc thuộc hạ tầng
test dùng chung, không phải là việc chưa xong của feature này — ghi nó vào
`tasks.md` sẽ khiến một feature đã đóng trông như còn mở.

## Quy tắc cho file này

- Mỗi mục nêu rõ **nó là gì**, **vì sao ngoài phạm vi**, và **gì sẽ gỡ chặn**.
- Một mục làm suy yếu bằng chứng của một task đã tick **phải nói rõ và nêu ID
  task đó**.
- Nếu một mục hóa ra thuộc feature này, nó quay lại `tasks.md` thành task thật.
  **File này không phải bãi đố.**

## Các mục

### D-01 — Tích hợp phía service dinh dưỡng (gửi `candidate_foods`, trả citation ra REST)

- **Vấn đề**: `GET /api/diet/meals/ai-recommendations` hiện trả gợi ý không có trích
  dẫn. Để người dùng thấy `citations[]` và `disclaimer`, service dinh dưỡng phải
  (1) dựng `candidate_foods` từ `FoodItems` + hồ sơ dị ứng, (2) gọi
  `GetMealRecommendations` qua gRPC, (3) map `citations[]`/`disclaimer` ra DTO
  client-facing.
- **Vì sao ngoài phạm vi**: `src/Services/Diet/**` thuộc feature của owner khác.
  `docs/development/team-workflow.md` §I.2 quy định "1 service = 1 feature đang
  mở tại một thời điểm" khi feature đó tạo migration hoặc đổi contract — feature
  này đã chiếm `contracts/recommendation/**` và migration của `recommendation_db`.
  Sửa code Diet trong feature này sẽ xung đột với feature đang mở của owner Diet.
- **Gỡ chặn bằng cách nào**: mở feature `duck/002/diet-recommendation-integration`
  sau khi feature này merge; feature đó sẽ là nơi **duy nhất** được phép đổi DTO
  client-facing của endpoint Diet, và **phải** tạo `frontend-guide.md` của riêng nó.
- **Ảnh hưởng tới task đã tick**: T059, T062 (contract và endpoint Recommendation đã
  hoàn tất) — **không** làm suy yếu bằng chứng nào; tính đầy đủ của feature này là
  phía Recommendation, đúng theo `spec.md` §Assumptions.

### D-02 — Tìm kiếm fuzzy tiếng Việt bằng `pg_trgm`

- **Vấn đề**: truy vấn viết sai dấu hoặc gõ cụt ("nhien an", "protein") có thể
  không khớp `tsvector` (config `simple` không stemming, không bỏ dấu ở vế query
  nếu không bọc `unaccent`) và không khớp vector.
- **Vì sao ngoài phạm vi**: `pg_trgm` rất nhạy với dấu (`similarity('Tếng Việt','tieng')`
  = 0,14 trước khi bỏ dấu) và index `gin_trgm_ops` trên cột nội dung dài rất nặng;
  thêm nó ở MVP là tối ưu hoá không cần thiết. Nghiên cứu đã đo và loại ở
  `research.md` D-06.
- **Gỡ chặn bằng cách nào**: mở feature riêng khi có dữ liệu thực tế cho thấy người
  dùng gõ sai dấu thường xuyên; khi đó bổ sung cột chuẩn hoá + index trigram
  **chỉ trên cột ngắn** (tiêu đề chương, từ khoá) — không đặt trên `content`.
- **Ảnh hưởng tới task đã tick**: không.

### D-03 — Rerank kết quả truy xuất bằng LLM

- **Vấn đề**: RRF merge đồng trọng số giữa nhánh vector và nhánh keyword; khi
  corpus lớn lên, thứ tự 8 kết quả đầu có thể chưa tối ưu.
- **Vì sao ngoài phạm vi**: thêm 1 lượt gọi provider cho mỗi request → tăng chi phí
  và độ trễ (SC-005 p95 < 3,5s). Ở quy mô ~1.300 chunk, rerank chưa chứng minh được
  giá trị.
- **Gỡ chặn bằng cách nào**: mở feature riêng **sau khi** có số liệu từ bộ eval
  (`make kb-eval`) cho thấy recall@8 chưa đạt kỳ vọng; khi đó rerank bằng
  `gemini-flash` chỉ cho top-20.
- **Ảnh hưởng tới task đã tick**: không.

### D-04 — Hỗ trợ nhiều corpus (nhiều cuốn sách)

- **Vấn đề**: `kb.corpus` đã có `corpus_key` và unique `(corpus_key, version)` nên
  về mặt schema là nhiều corpus được, nhưng mọi truy vấn đều lọc theo **một**
  `corpus_key` cố định lấy từ cấu hình.
- **Vì sao ngoài phạm vi**: hiện nguồn tri thức chính thức chỉ có một cuốn sách
  (`docs/reference/che-do-an-truong-tho/`); `spec.md` §Assumptions ghi rõ sách là
  nguồn duy nhất.
- **Gỡ chặn bằng cách nào**: khi có cuốn thứ hai, bổ sung bảng `kb.corpus` → bảng
  nguồn và cho phép truy vấn nhiều corpus + chia sẻ embedding; khi đó cần ADR mới.
- **Ảnh hưởng tới task đã tick**: T020, T073 (thiết kế `corpus_key` đã sẵn sàng cho
  mở rộng) — không làm suy yếu.

### D-05 — `Pgvector.EntityFrameworkCore` chưa có bản release khai báo hỗ trợ EF Core 10

- **Vấn đề**: package dùng ở đây là `Pgvector.EntityFrameworkCore` **0.3.0**, chỉ có
  asset `net8.0` (dùng được trên `net10.0`) và tự README nói "hỗ trợ EF Core 9 và
  10" nhưng chưa có bản publish nào khai báo EF Core 10; PR bump lên `net10.0` /
  `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` còn đang mở upstream.
- **Vì sao ngoài phạm vi**: đây là quyết định phụ thuộc bên thứ ba, không phải việc
  của feature. Đã xử lý bằng cách **ghim phiên bản** + cô lập toàn bộ truy cập vector
  sau interface `IKnowledgeStore` để đổi backend không phải sửa use case.
- **Gỡ chặn bằng cách nào**: theo dõi repo `pgvector/pgvector-dotnet`; khi có bản
  10.0.0 chính thức thì nâng version trong `Directory.Packages.props` và chạy lại
  integration test. Nếu gặp hồi quy, chuyển sang đường dự phòng **đã kiểm chứng**:
  cột `vector` + raw SQL `$1::vector` (ghi string) và đọc `embedding::text` /
  `embedding::real[]`.
- **Ảnh hưởng tới task đã tick**: T004, T022, T027, T029 — rủi ro được đánh dấu và
  có đường dự phòng cụ thể; **không** làm suy yếu bằng chứng nếu integration test
  `make test PROJECTS=src/Services/Recommendation` xanh.

### D-06 — `specs/duck-001-recommendation-rag/frontend-guide.md` không được tạo

- **Vấn đề**: hợp đồng gRPC `contracts/recommendation/v1/recommendation.proto` thay
  đổi (thêm `Citation`, `CandidateFood`, `Disclaimer`, 2 RPC mới) nhưng **không**
  thay đổi HTTP endpoint, event payload hay DTO client-facing nào trong feature này.
- **Vì sao ngoài phạm vi**: Recommendation là service **nội bộ, gRPC-only**, không
  route qua Nginx (`docs/system-design/microservices/01-service-boundaries.md`). DTO
  client-facing của `GET /api/diet/meals/ai-recommendations` chỉ đổi khi service
  dinh dưỡng được nối — thuộc D-01.
- **Gỡ chặn bằng cách nào**: feature `duck/002` (D-01) **phải** tạo
  `frontend-guide.md` của riêng nó và chạy đếm đồng bộ route/field/enum/status
  trước khi đặt `READY_FOR_FRONTEND`.
- **Ảnh hưởng tới task đã tick**: T119 ghi kết luận này — không có bằng chứng nào
  bị suy yếu.

### D-07 — Vị trí file `.env` không khớp với thư mục project của Compose

- **Vấn đề**: `deployments/docker-compose.yml` nằm trong `deployments/`, nên Compose
  mặc định chỉ đọc `deployments/.env`; còn repo đặt file môi trường ở
  `.env` ở **thư mục gốc**. Lệnh `docker compose -f deployments/docker-compose.yml up -d`
  (đã ghi trong `README.md` và `deployments/docker-compose.yml` trước đây) vì vậy
  **không nạp** các biến trong `.env` → `POSTGRES_PASSWORD`, `GOOGLE_AI__API_KEY`
  … rỗng. Lệnh đúng phải là
  `docker compose --env-file .env -f deployments/docker-compose.yml up -d`.
- **Vì sao ngoài phạm vi**: lỗi này có sẵn từ trước feature này và ảnh hưởng **mọi**
  service của repo, không chỉ Recommendation. Sửa ở đây sẽ xung đột với feature
  của các owner khác đang chạm `deployments/docker-compose.yml`.
- **Gỡ chặn bằng cách nào**: team chốt một trong hai hướng rồi áp dụng cho toàn repo:
  (a) chuyển `.env` sang `deployments/.env` (Compose tự đọc), hoặc (b) chuẩn hoá mọi
  lệnh compose trong tài liệu + Makefile thành `--env-file .env`. Feature này đã
  dùng hướng (b) và sửa `README.md` + `docs/development/local-setup.md` cho đúng
  với nó.
- **Ảnh hưởng tới task đã tick**: T009, T016 — không làm suy yếu bằng chứng nào;
  các biến của feature đều được đặt tường minh trong `environment:` của service.

### D-08 — API của `AppException` dùng chung với feature Identity

- **Vấn đề**: `src/Shared/Errors/AppException.cs` + `FieldError.cs` do feature này tạo
  (theo `docs/system-design/design-pattern.md` §III) nhưng feature **Identity** cũng
  cần loại lỗi dùng chung này. Hai feature có thể tạo hai bản khác nhau trong
  `src/Shared/Errors/`, gây xung đột khi merge.
- **Vì sao ngoài phạm vi**: `src/Shared/**` là vùng xung đột đã khai trong
  `plan.md`; feature Identity chưa có trong registry nên chưa thể thống nhất ngay.
  Việc thống nhất API cần owner của feature Identity.
- **Gỡ chặn bằng cách nào**: owner của feature Identity dùng lại đúng `AppException`
  này (đã có `WithCode`/`WithErrors` + factory theo §III) thay vì tạo bản mới; nếu
  feature Identity đã tạo file cùng đường dẫn, hợp nhất khi merge `main` — tuyệt đối
  không sửa tay snapshot hay API lệch nhau giữa hai nhánh.
- **Ảnh hưởng tới task đã tick**: T005 — bằng chứng của task vẫn đúng (lỗi dùng chung
  đã tồn tại và được dùng bởi `Recommendation`), nhưng **rủi ro xung đột khi merge
  phải được xử lý như một task riêng của feature Identity**.


### D-09 — Deadline server và timeout provider đang bằng nhau (30s)

- **Vấn đề**: `RECOMMENDATION__DEADLINE_SECONDS` = 30 và `GOOGLE_AI__TIMEOUT_MS` = 30000. Khi provider chậm, timeout của provider và deadline của gRPC tranh nhau, nên client nhận `DEADLINE_EXCEEDED` thay vì nhánh abstain êm (kết quả rỗng + cảnh báo).
- **Vì sao ngoài phạm vi**: không làm sai kết quả, chỉ ảnh hưởng cách suy giảm; đo được 1/50 câu bị timeout tạm thời khi eval.
- **Gỡ chặn bằng cách nào**: đặt `GOOGLE_AI__TIMEOUT_MS` **nhỏ hơn** deadline (ví dụ 20s) để adapter kịp trả kết quả rỗng có kiểm soát; hoặc thêm retry có ngân sách thời gian.
- **Ảnh hưởng tới task đã tick**: T111, T110 — không làm sai số liệu; chỉ là chất lượng suy giảm.

### D-10 — Hai tiêu chí đo lường chưa đạt trong môi trường hiện tại

- **Vấn đề**:
  - **SC-003**: cần ≥ 90% câu hỏi trong sách có trang trích dẫn trong ±3 trang. Đo được **44/50 = 88,0%** (44/49 = 89,8% trên số câu đánh giá được) → **dưới ngưỡng**.
  - **SC-005**: người dùng nhận câu trả lời trong 3 giây (p95). Đo được provider mất **4,8–16,1s** → **không đạt**; PRD §3.1 đã ghi rõ phần "Thực đo".
- **Vì sao ngoài phạm vi**: đây là ngưỡng chất lượng phụ thuộc **chất lượng truy xuất** và **độ trễ provider**, không phải lỗi chức năng. Nâng chất lượng cần tinh chỉnh chunking/rerank và/hoặc đổi model — thay đổi thiết kế, không phải sửa bug.
- **Gỡ chặn bằng cách nào**: (a) rerank top-20 (D-03) để tăng tỉ lệ trúng trang; (b) xem lại 5 câu fail (`g02`, `g14`, `g15`, `g37`, `g39`) — 3 abstain oan và 2 trả sai trang; (c) với latency: streaming, model nhanh hơn, hoặc cache ngữ nghĩa.
- **Ảnh hưởng tới task đã tick**: T108–T110 (đo đúng, không pass giả) và T111 (assert riêng cho retrieval `< 150ms` đạt; phần end-to-end chỉ **ghi nhận**, không assert).

### D-11 — Chất lượng trả lời trên 5 câu vàng cần điều tra thêm

- **Vấn đề**: `g02`, `g14`, `g37` abstain dù corpus **có** nội dung (đã đối chiếu `artifacts/book-kb/chunks.jsonl`); `g15`, `g39` trả về đúng chương nhưng sai trang (ch4/p102 thay vì p79; ch4/p102 thay vì ch12/p234).
- **Vì sao ngoài phạm vi**: nguyên nhân nằm ở xếp hạng truy xuất (top-8 chọn nhầm chunk) chứ không phải lỗi contract hay luồng nghiệp vụ; sửa cần rerank hoặc điều chỉnh chunking (D-03, D-10).
- **Gỡ chặn bằng cách nào**: bật rerank top-20, hoặc tăng `RAG__TOP_K`, hoặc kiểm tra lại ranh giới chunk ở các chương 0/4/11/12 rồi đo lại bằng `make kb-eval`.
- **Ảnh hưởng tới task đã tick**: T108 (golden set đúng, không sửa để "cho pass") — không suy yếu; T110 ghi trung thực.
