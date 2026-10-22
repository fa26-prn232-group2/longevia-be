# Feature Specification: Recommendation Service với RAG tri thức sách "Chế độ ăn trường thọ"

**Feature Branch**: `duck-001-recommendation-rag`
**Created**: 2026-10-22
**Status**: Draft
**Input**: User description: "Recommendation Service voi RAG tren tri thuc sach 'Che do an truong tho': 5 gRPC RPC, knowledge store pgvector recommendation_db, corpus ingest offline, citation chuong + trang, loc di ung va rule 105% bang code"

> Tài liệu trong `specs/` viết bằng tiếng Việt (`docs/development/agent-workflow.md` §IV.7). Giữ nguyên tên heading của template để các skill downstream phân tích được.

---

## Clarifications

### Session 2026-10-22

- Q: Hệ thống có được phép trả lời các câu hỏi về chế độ ăn dành cho người đang mắc bệnh (ung thư, tiểu đường, tim mạch, Alzheimer, bệnh viêm và tự miễn) không? → A: Cho phép ở mức lối sống (chế độ ăn, thói quen), kèm disclaimer bắt buộc và cờ "chưa đối chiếu bản in" nếu trang chưa được kiểm chứng; tuyệt đối không đưa khuyến nghị về thuốc, liều thuốc, chẩn đoán hay thay thế điều trị; câu hỏi về điều trị thì trả lời "hãy hỏi bác sĩ".
- Q: Mức độ thiếu của một vi chất được phân loại dựa trên ngưỡng nào? → A: Theo tỷ lệ lượng thiếu so với nhu cầu: dưới 25% là **nhẹ**, từ 25% đến 50% là **trung bình**, trên 50% là **nặng**.
- Q: Nhu cầu dinh dưỡng tham chiếu dùng để tính lượng thiếu vi chất có được cá nhân hoá theo nhóm tuổi và giới của người dùng không? → A: Có, cá nhân hoá theo **nhóm tuổi và giới** (ví dụ người lớn nam / nữ); tuổi và giới trong hồ sơ dùng để chọn nhóm nhu cầu tham chiếu.
- Q: Điểm tối đa của từng nhánh thói quen được xác định theo ngưỡng nào? → A: Chốt ngưỡng chuẩn ngay trong spec — nhịn ăn **16 giờ**, giấc ngủ **7 giờ**, uống nước đạt mục tiêu **7/7 ngày**, đạt mục tiêu dinh dưỡng **90%** là điểm tối đa của nhánh tương ứng.
- Q: Mỗi bữa ăn nên có tối đa bao nhiêu món được gợi ý? → A: **Tối đa 5 món mỗi bữa** — đủ lựa chọn, danh sách không bị ngợp, số trích dẫn kèm theo vẫn đọc được.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gợi ý bữa ăn chắc chắn an toàn (Priority: P1) 🎯 MVP

Là người dùng đang theo dõi chế độ ăn, tôi muốn nhận gợi ý món ăn cho bữa tiếp theo dựa trên lượng calo và vi chất còn thiếu hôm nay, với điều kiện tuyệt đối không món nào chứa thành phần tôi dị ứng.

**Why this priority**: Đây là giá trị sản phẩm cao nhất và là nơi rủi ro an toàn lớn nhất (BR-03: phải loại trừ 100% thành phần gây dị ứng). Nếu chỉ làm một story, đây là story đem lại giá trị rõ ràng nhất.

**Independent Test**: Gọi dịch vụ với một danh mục món ứng viên và hồ sơ dị ứng bất kỳ → nhận về danh sách món đề xuất, kiểm tra được không món nào chứa thành phần dị ứng, và mỗi món đều truy ngược được về món trong danh mục ban đầu.

**Acceptance Scenarios**:

1. **Given** tôi dị ứng với đậu phộng và có sữa trong danh mục món ứng viên, **When** tôi xin gợi ý bữa ăn, **Then** không món nào trả về chứa đậu phộng hoặc sữa.
2. **Given** tôi đã ăn đủ hoặc vượt 105% lượng calo mục tiêu trong ngày, **When** tôi xin gợi ý bữa ăn chính, **Then** dịch vụ không gợi ý món ăn chính mà chỉ gợi ý đồ uống không calo hoặc thói quen thư giãn, kèm cờ cho biết đã chạm trần.
3. **Given** hôm nay tôi còn thiếu 30g protein, **When** tôi xin gợi ý, **Then** mỗi món được gợi ý kèm lý do chọn và lượng protein của món đó lấy đúng từ món trong danh mục tôi cung cấp, không phải do AI tự ước lượng.
4. **Given** mọi món trong danh mục đều chứa thành phần tôi dị ứng, **When** tôi xin gợi ý, **Then** dịch vụ trả về danh sách rỗng kèm lý do rõ ràng thay vì gợi ý món khác.
5. **Given** một món trong danh mục có thể giải thích theo sách, **When** dịch vụ trả về món đó, **Then** món đó kèm trích dẫn chương và trang sách.
6. **Given** tôi không dị ứng gì và còn thiếu nhiều vi chất, **When** tôi xin gợi ý một bữa, **Then** tôi nhận không quá 5 món để còn lựa chọn nhưng không bị ngợp.

---

### User Story 2 - Tri thức sách luôn được cập nhật (Priority: P2)

Là người bảo trì hệ thống, tôi muốn nạp lại toàn bộ tri thức sách vào kho tri thức khi tài liệu tham chiếu thay đổi, mà không phải sửa mã nguồn hay build lại ứng dụng.

**Why this priority**: Là điều kiện tiên quyết cho mọi câu trả lời có căn cứ (User Story 3, 4). Nếu kho tri thức rỗng thì toàn bộ RAG không có gì để truy xuất.

**Independent Test**: Chạy quy trình nạp lại hai lần trên cùng một bộ tài liệu → số đoạn trong kho tri thức không nhân đôi, và mỗi đoạn đều mang đúng chương và đúng trang sách.

**Acceptance Scenarios**:

1. **Given** bộ tài liệu tham chiếu gồm 13 chương (273 trang sách bản in), **When** quy trình nạp chạy xong, **Then** mọi chương đều có mặt trong kho tri thức kèm khoảng trang sách và trang tương ứng.
2. **Given** kho tri thức đã có dữ liệu của bộ tài liệu hiện tại, **When** quy trình nạp chạy lại với cùng nội dung, **Then** không có đoạn nào bị nhân bản và số đoạn giữ nguyên.
3. **Given** một trang trong sách chưa được đối chiếu với bản in, **When** đoạn chứa trang đó được nạp, **Then** đoạn đó được đánh dấu là chưa đối chiếu để hệ thống không khẳng định số liệu từ đoạn đó.
4. **Given** một tài liệu thiếu thông tin trang, **When** quy trình nạp chạy, **Then** quy trình dừng lại và báo lỗi thay vì nạp sai số trang.

---

### User Story 3 - Hỏi tri thức trong sách (Priority: P3)

Là người dùng, tôi muốn hỏi trực tiếp về chế độ ăn trường thọ và nhận câu trả lời bám sát nội dung sách, kèm chỗ để tôi tự tra lại trong sách.

**Why this priority**: Đây là bản chất của yêu cầu RAG và là tính năng người dùng nhìn thấy rõ nhất: mọi câu trả lời đều dẫn đường về chương và trang sách.

**Independent Test**: Đặt một câu hỏi có lời giải đích trong sách → nhận câu trả lời kèm chương, trang sách và trích đoạn; đối chiếu trích đoạn với bản in sẽ thấy đúng nội dung.

**Acceptance Scenarios**:

1. **Given** câu hỏi về cơ chế tự thực bào, **When** tôi hỏi, **Then** câu trả lời dựa trên nội dung sách và kèm chương, trang sách bản in, trang tương ứng trong bản scan và trích đoạn nguyên văn.
2. **Given** câu hỏi không có nội dung trong sách, **When** tôi hỏi, **Then** dịch vụ nói rõ là không tìm thấy dữ liệu trong sách thay vì suy diễn, và không kèm trích dẫn.
3. **Given** câu hỏi có nội dung trong sách nhưng ở trang chưa đối chiếu bản in, **When** tôi hỏi, **Then** câu trả lời ghi rõ điều đó và trích dẫn kèm cờ chưa đối chiếu.
4. **Given** câu hỏi về chế độ ăn dành cho người đang mắc bệnh (ví dụ chế độ ăn hỗ trợ phòng ngừa ung thư), **When** tôi hỏi, **Then** hệ thống trả lời ở mức lối sống dựa trên nội dung sách, kèm trích dẫn và khuyến cáo tham vấn chuyên gia.
5. **Given** câu hỏi về thuốc, liều thuốc, chẩn đoán bệnh hoặc thay thế điều trị, **When** tôi hỏi, **Then** hệ thống từ chối, không đưa khuyến nghị và hướng tôi đến bác sĩ chuyên khoa.

---

### User Story 4 - Tra cứu các đoạn trong sách (Priority: P4)

Là người dùng, tôi muốn tìm các đoạn trong sách liên quan đến một chủ đề để tự đọc, kể cả khi tôi không biết mình đang hỏi đúng từ gì.

**Why this priority**: Bổ trợ cho User Story 3 và cho người dùng kiểm chứng nguồn; giá trị độc lập thấp hơn nhưng triển khai rẻ vì dùng chung hạ tầng truy xuất.

**Independent Test**: Tìm một chủ đề (ví dụ "nhịn ăn gián đoạn") → nhận về danh sách đoạn có nội dung, chương, trang sách và mức độ liên quan; có thể lọc theo chương.

**Acceptance Scenarios**:

1. **Given** một chủ đề, **When** tôi tra cứu, **Then** nhận về danh sách đoạn có nội dung, chương, trang sách và điểm liên quan, sắp xếp theo độ liên quan giảm dần.
2. **Given** tôi chỉ quan tâm một chương cụ thể, **When** tôi tra cứu kèm lọc theo chương, **Then** chỉ những đoạn thuộc chương đó được trả về.

---

### User Story 5 - Biết mình đang thiếu vi chất gì trong tuần (Priority: P5)

Là người dùng, tôi muốn biết trong 7 ngày qua tôi đã thiếu vi chất nào và nên ăn gì để bù, dựa trên hồ sơ thể trạng của tôi.

**Why this priority**: Bổ trợ cho thói quen theo dõi; giá trị rõ nhưng phụ thuộc dữ liệu nhật ký ăn uống của service khác nên chưa phải lái xe chính.

**Independent Test**: Gửi 7 ngày dinh dưỡng và hồ sơ thể trạng → nhận về danh sách vi chất thiếu kèm lượng thiếu và mức độ, kèm gợi ý thực phẩm có căn cứ trong sách.

**Acceptance Scenarios**:

1. **Given** 7 ngày ăn uống thiếu chất xơ so với mục tiêu, **When** tôi xin phân tích, **Then** tôi thấy chất xơ nằm trong danh sách thiếu kèm lượng thiếu và mức độ thiếu tương ứng tỷ lệ lượng thiếu so với nhu cầu.
2. **Given** hồ sơ của tôi có thành phần dị ứng, **When** tôi xin phân tích, **Then** mọi thực phẩm được gợi ý đều không chứa thành phần dị ứng.
3. **Given** phần giải thích dựa trên nội dung sách, **When** tôi xin phân tích, **Then** phần đó kèm chương và trang sách.
4. **Given** cùng một dữ liệu đầu vào, **When** tôi xin phân tích hai lần, **Then** kết quả tính toán giống hệt nhau.

---

### User Story 6 - Biết điểm thói quen trường thọ và phần nào đang kéo điểm (Priority: P6)

Là người dùng, tôi muốn biết điểm số sức khỏe trường thọ của mình và nhánh thói quen nào đang làm tôi mất điểm, để biết cần cải thiện gì.

**Why this priority**: Hoàn thiện bộ giá trị của service nhưng ít người dùng chủ động xem hơn gợi ý món ăn.

**Independent Test**: Gửi dữ liệu thói quen (nhịn ăn, giấc ngủ, uống nước, mức độ đạt dinh dưỡng) → nhận điểm 0–100 kèm điểm và trọng số của từng nhánh.

**Acceptance Scenarios**:

1. **Given** tôi nhịn ăn 16 giờ/ngày, ngủ 7 giờ, uống đủ nước 5/7 ngày và đạt mục tiêu dinh dưỡng, **When** tôi xin đánh giá, **Then** tôi nhận điểm 0–100 kèm điểm, trọng số và lý do của từng nhánh.
2. **Given** cùng dữ liệu đầu vào, **When** tôi xin đánh giá hai lần, **Then** điểm số và phần giải thích giống hệt nhau.
3. **Given** nhánh thói quen điểm thấp, **When** tôi xin đánh giá, **Then** tôi nhận gợi ý cụ thể về nhánh đó kèm căn cứ trong sách nếu có.

---

### Edge Cases

- **Dị ứng lấn át**: mọi món trong danh mục đều chứa thành phần dị ứng → trả danh sách rỗng kèm lý do, không gợi ý thay thế ngoài danh mục.
- **Đã chạm trần calo**: calo đã ăn ≥ 105% mục tiêu → không gợi ý bữa ăn chính.
- **Danh mục rỗng**: caller không gửi món ứng viên nào → trả kết quả rỗng kèm thông báo, không gọi AI.
- **Kho tri thức rỗng hoặc chưa nạp**: mọi câu hỏi phải trả "không tìm thấy dữ liệu", tuyệt đối không suy diễn.
- **Câu hỏi về thuốc, liều thuốc, chẩn đoán bệnh hoặc thay thế điều trị** → từ chối, không đưa khuyến nghị và hướng người dùng đến bác sĩ chuyên khoa.
- **Câu hỏi về chế độ ăn cho người đang mắc bệnh** → trả lời ở mức lối sống dựa trên nội dung sách, kèm trích dẫn và khuyến cáo tham vấn chuyên gia; nếu trang nguồn chưa đối chiếu bản in thì phải gắn cờ chưa đối chiếu.
- **Câu hỏi ngoài lĩnh vực sách** (tin tức, thời sự, thông tin không liên quan dinh dưỡng) → từ chối và hướng về lời khuyên lối sống kèm khuyến cáo tham vấn chuyên gia.
- **Câu hỏi viết sai dấu hoặc rất dài** → vẫn tìm được đoạn liên quan nhờ tra cứu kết hợp ngữ nghĩa và từ khoá.
- **Trích dẫn không khớp nội dung nguồn** → loại trích dẫn đó; nếu mất hết trích dẫn thì hạ câu trả lời về trạng thái "không đủ căn cứ".
- **Dịch vụ AI lỗi, hết thời gian chờ hoặc hết hạn mức** → trả kết quả rỗng kèm cảnh báo; tuyệt đối không trả nội dung bịa và không làm hỏng request của phía gọi.
- **Trang sách chưa đối chiếu bản in** → vẫn dùng được nhưng phải gắn cờ chưa đối chiếu và không được khẳng định số liệu.
- **Nhiều người dùng hỏi cùng lúc** → kết quả phải giống nhau bất kể thứ tự xử lý.

---

## Requirements *(mandatory)*

### Functional Requirements

**Gợi ý bữa ăn**

- **FR-001**: Hệ thống phải chỉ gợi ý món ăn có trong danh mục món an toàn do phía gọi cung cấp và phải trả kèm định danh của món đó để ánh xạ ngược về danh mục.
- **FR-002**: Hệ thống phải loại bỏ 100% món có thành phần trùng với danh sách dị ứng của người dùng, bất kể hệ thống có diễn giải món đó hay không.
- **FR-003**: Hệ thống phải loại bỏ món không phù hợp với sở thích ăn uống đã khai báo của người dùng.
- **FR-004**: Khi lượng calo đã ăn trong ngày đạt từ 105% mục tiêu trở lên, hệ thống phải không gợi ý bữa ăn chính, chỉ gợi ý đồ uống không calo hoặc thói quen thư giãn/giấc ngủ, và phải kèm cờ báo hiệu trạng thái này.
- **FR-005**: Lượng calo và vi chất của món gợi ý phải lấy từ dữ liệu món trong danh mục, không được do hệ thống tự ước lượng.
- **FR-006**: Mỗi món gợi ý phải kèm lý do chọn món đó.
- **FR-007**: Hệ thống phải hỗ trợ các bữa sáng, trưa, tối và phụ, và phải trả về **tối đa 5 món** cho mỗi bữa.

**Phân tích vi chất**

- **FR-008**: Hệ thống phải nhận hồ sơ thể trạng của người dùng (tuổi, giới, chiều cao, cân nặng, mức hoạt động, dị ứng, sở thích ăn uống) cùng dữ liệu 7 ngày dinh dưỡng và mục tiêu dinh dưỡng; trong đó **tuổi và giới dùng để chọn nhóm nhu cầu dinh dưỡng tham chiếu**.
- **FR-009**: Hệ thống phải tính lượng thiếu của từng vi chất theo nhu cầu dinh dưỡng tham chiếu **ứng với nhóm tuổi và giới của người dùng**, và phải cho cùng một đầu vào thì cho cùng một kết quả.
- **FR-010**: Hệ thống phải phân loại mức độ thiếu của từng vi chất theo tỷ lệ lượng thiếu so với nhu cầu: dưới 25% là **nhẹ**, từ 25% đến 50% là **trung bình**, trên 50% là **nặng**.
- **FR-011**: Phần giải thích và thực phẩm gợi ý trong phân tích vi chất phải có căn cứ trong sách và kèm trích dẫn.

**Chấm điểm thói quen trường thọ**

- **FR-012**: Hệ thống phải chấm điểm thói quen trường thọ trên thang 0–100 theo một bộ tiêu chí cố định với trọng số công khai: nhịn ăn 35%, giấc ngủ 30%, uống nước 20%, mức độ đạt dinh dưỡng 15%; điểm tối đa của từng nhánh đạt khi nhịn ăn đủ 16 giờ, ngủ đủ 7 giờ, uống nước đạt mục tiêu 7/7 ngày, đạt mục tiêu dinh dưỡng 90%.
- **FR-013**: Hệ thống phải trả kèm điểm, trọng số và lý do của từng nhánh thành phần, và phải cho cùng một đầu vào thì cho cùng một điểm số.
- **FR-014**: Mỗi gợi ý hành động phải kèm căn cứ trong sách nếu nội dung đó có trong sách.

**Hỏi và tra cứu tri thức**

- **FR-015**: Hệ thống phải trả lời câu hỏi của người dùng dựa duy nhất trên nội dung sách đã nạp, và phải gắn trích dẫn cho từng khẳng định.
- **FR-016**: Mỗi trích dẫn phải chứa chương, tên chương, trang sách bản in, trang tương ứng trong bản scan, trích đoạn nguyên văn và trạng thái đã/chưa đối chiếu bản in.
- **FR-017**: Hệ thống phải kiểm tra rằng trích đoạn nằm trong đoạn nguồn đã truy xuất; trích dẫn không khớp phải bị loại và nếu mất hết trích dẫn thì câu trả lời phải chuyển sang trạng thái không đủ căn cứ.
- **FR-018**: Khi sách không có đủ dữ liệu để trả lời, hệ thống phải nói rõ không tìm thấy dữ liệu và tuyệt đối không suy diễn nội dung ngoài sách.
- **FR-019**: Nội dung lấy từ trang chưa đối chiếu bản in phải được đánh dấu là chưa kiểm chứng và không được khẳng định như sự thật y tế.
- **FR-020**: Hệ thống phải hỗ trợ tra cứu các đoạn sách theo truy vấn tự do, có lọc theo chương, và trả về kèm chương, trang sách và mức độ liên quan.
- **FR-021**: Hệ thống phải đáp ứng người dùng trong một khoảng thời gian chấp nhận được, tính cả thời gian tìm kiếm tri thức và thời gian sinh câu trả lời.

**Kho tri thức và vòng đời dữ liệu**

- **FR-022**: Hệ thống phải lưu trữ toàn bộ tri thức sách đã chia nhỏ trong một kho tri thức riêng của service, kèm chương, trang sách bản in, trang scan, trạng thái đối chiếu và dạng nhúng của đoạn.
- **FR-023**: Kho tri thức phải được nạp bằng quy trình ngoài thời gian chạy của service, chạy lại được nhiều lần mà không tạo bản sao.
- **FR-024**: Mỗi lần nạp phải ghi nhận một phiên bản tri thức để có thể truy vết và quay lại phiên bản trước.
- **FR-025**: Kho tri thức phải cho phép tìm kiếm theo cả nghĩa của câu hỏi lẫn từ khoá, và phải kết hợp hai kết quả thay vì chỉ dùng một.
- **FR-026**: Kho tri thức phải cho phép lọc kết quả theo chương.

**An toàn, riêng tư và tuân thủ**

- **FR-027**: Mọi câu trả lời phải kèm khuyến cáo rằng nội dung chỉ mang tính chất tham khảo khoa học và không thay thế tư vấn của bác sĩ chuyên khoa.
- **FR-028**: Kho tri thức phải không chứa bất kỳ dữ liệu người dùng nào; mọi dữ liệu người dùng phải chỉ đi qua vào và ra theo từng lời gọi.
- **FR-029**: Hệ thống phải không ghi vào nhật ký các thông tin nhạy cảm của người dùng, khóa bí mật hay nội dung sức khỏe.
- **FR-030**: Nội dung trong sách phải được coi là dữ liệu tham chiếu, không phải chỉ dẫn, để câu hỏi của người dùng không thể thay đổi hành vi của hệ thống.
- **FR-031**: Khi dịch vụ AI lỗi hoặc hết thời gian chờ, hệ thống phải trả kết quả rỗng kèm cảnh báo thay vì trả nội dung suy diễn.
- **FR-032**: Khi người dùng hỏi về chế độ ăn dành cho người đang mắc bệnh, hệ thống được phép trả lời ở mức lối sống dựa trên nội dung sách, kèm trích dẫn và khuyến cáo tham vấn chuyên gia; nếu trang nguồn chưa đối chiếu bản in thì phải gắn cờ chưa đối chiếu.
- **FR-033**: Khi người dùng hỏi về thuốc, liều thuốc, chẩn đoán bệnh hoặc thay thế điều trị, hệ thống phải từ chối, không được đưa khuyến nghị, và phải hướng người dùng đến bác sĩ chuyên khoa.

### Key Entities

- **Phiên bản tri thức (Corpus)**: một lần cập nhật của bộ tài liệu tham chiếu; ghi nhận mã định danh, phiên bản, nguồn, số chương, số đoạn và thông số dùng để chia nhỏ và nhúng.
- **Chương (Chapter)**: một chương của sách; giữ số thứ tự, tên chương, tệp nguồn và khoảng trang sách bản in cùng trang scan.
- **Đoạn tri thức (Chunk)**: đơn vị nhỏ nhất được truy xuất; giữ nội dung, chương, khoảng trang sách, khoảng trang scan, số từ, trạng thái đối chiếu và dạng nhúng.
- **Cờ chất lượng trang (Page Flag)**: ghi nhận trang nào đã đối chiếu bản in, trang nào còn nghi vấn về số liệu hoặc chữ; là nguồn sự thật để tính trạng thái của đoạn.
- **Trích dẫn (Citation)**: chứng cứ gắn với một khẳng định; gồm chương, tên chương, trang sách bản in, trang scan, trích đoạn và trạng thái đối chiếu.
- **Hồ sơ người dùng (User Profile)**: dữ liệu đầu vào của mỗi lời gọi gồm tuổi, giới, chiều cao, cân nặng, mức hoạt động, dị ứng và sở thích ăn uống.
- **Món ăn ứng viên (Candidate Food)**: món trong danh mục an toàn kèm dinh dưỡng và thành phần gây dị ứng, do phía gọi cung cấp.
- **Khoảng thiếu vi chất (Nutrient Gap)**: một vi chất thiếu, gồm nhu cầu tham chiếu **theo nhóm tuổi và giới**, lượng trung bình đã nạp, lượng thiếu và mức độ.
- **Thành phần điểm (Score Component)**: một nhánh trong thang chấm điểm thói quen trường thọ, gồm điểm, trọng số, ngưỡng đạt điểm tối đa của nhánh và lý do.
- **Khuyến cáo (Disclaimer)**: lời nhắc tham khảo khoa học đi kèm mọi câu trả lời.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% món được gợi ý trong bộ kiểm thử tự động đều không chứa thành phần dị ứng của người dùng.
- **SC-002**: 100% trích dẫn trả về có trích đoạn khớp với đoạn nguồn đã truy xuất, kiểm chứng tự động.
- **SC-003**: Trong bộ câu hỏi đánh giá, ít nhất 90% câu hỏi có lời giải đích trong sách được trả lời kèm trích dẫn chương và trang đúng trong sai số ±3 trang.
- **SC-004**: 0 câu hỏi nằm ngoài sách nào được trả lời bằng nội dung không có trích dẫn; tất cả đều báo rõ không tìm thấy dữ liệu.
- **SC-005**: Người dùng nhận được gợi ý và câu trả lời trong vòng 3 giây ở 95% các lượt gọi.
- **SC-006**: Cùng một dữ liệu đầu vào luôn cho cùng một điểm thói quen trường thọ và cùng một danh sách khoảng thiếu vi chất.
- **SC-007**: Toàn bộ 273 trang sách (13 chương) nằm trong kho tri thức và mỗi đoạn đều truy được về chương và trang sách bản in.
- **SC-008**: 100% trích dẫn trả về cho người dùng kèm trạng thái đã/chưa đối chiếu bản in.
- **SC-009**: 0 bản ghi dữ liệu người dùng tồn tại trong kho tri thức của hệ thống.
- **SC-010**: Việc nạp lại kho tri thức hai lần trên cùng bộ tài liệu không làm thay đổi số đoạn lưu trữ.

---

## Assumptions

- Cuốn sách *Chế độ ăn trường thọ* đã được số hoá thành văn bản theo chương với đánh số trang bản in và trang bản scan; đó là nguồn tri thức duy nhất mà hệ thống được phép dùng.
- Văn bản số hoá là bản OCR chưa được đối chiếu toàn bộ với bản in; vì vậy hệ thống phải đánh dấu trạng thái đối chiếu cho từng trích dẫn thay vì coi mọi nội dung là chính xác.
- Người dùng cuối không gọi trực tiếp service nội bộ này; các yêu cầu đến từ service khác qua giao thức nội bộ.
- Dữ liệu hồ sơ thể trạng và dị ứng do service hồ sơ cung cấp và được truyền vào theo từng lời gọi; service này không tự truy vấn nguồn dữ liệu đó.
- Danh mục món ăn an toàn do service dinh dưỡng cung cấp kèm theo mỗi lời gọi; service này không tự quản lý danh mục món.
- Việc tích hợp phía service dinh dưỡng (gửi danh mục món và hiển thị trích dẫn ra phía người dùng) là một hạng mục riêng, không nằm trong phạm vi feature này.
- Khi có nhiều người dùng hỏi cùng lúc, kết quả phải giống nhau bất kể thứ tự xử lý.
- Các quy ước về hiệu năng, độ trễ và giới hạn chi phí được lấy từ yêu cầu phi chức năng trong tài liệu yêu cầu sản phẩm.
