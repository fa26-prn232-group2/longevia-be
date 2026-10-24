# Việc ngoài phạm vi

Không để checkbox implement các việc dưới đây trong tasks Progress core.

| ID | Việc / lý do ngoài phạm vi | Điều kiện mở lại / owner | Ảnh hưởng evidence |
| --- | --- | --- | --- |
| DFR-01 | AI workout/longevity và REST facade tương ứng; Phase 1 chỉ Progress core planning. | Đức + Bảo duyệt feature riêng, ADR chiều gọi, profile/nutrition contracts, deadline/error mapping, disclaimer/citation. | Chưa có task hoàn thành chứng minh AI integration; không chặn timer/water/sleep. |
| DFR-02 | Weekly digest scheduling, delivery, sửa Worker/Gateway; PRD mới có kịch bản, chưa có contract. | Duy/Bảo/Huy chốt producer, recipient/consent, tuần/timezone, schema/topology, dedup/correction; feature riêng owner thích hợp. | Template Worker tồn tại không chứng minh digest end-to-end. |
| DFR-03 | Thay đổi Identity profile kiểu chiều cao, RPC/event producer, Diet producer, Shared auth/pagination. Không sửa code/contract của owner khác. | Nguyên/Huy/lead tự thực hiện feature của họ; Progress chỉ consume contract đã duyệt. | Metrics BMI và streak đầy đủ còn BLOCKED Q02/Q09; tasks Progress vẫn giữ consumer work đúng scope. |
| DFR-04 | Target nước tùy chỉnh, endpoint update/delete metric/sleep, wearable, payment, social hoặc lịch sử fasting mở rộng. Chưa được PRD/API hiện tại cấp scope này. | Product phê duyệt và mở feature/contract riêng; nếu Q05 yêu cầu target tùy chỉnh phải đánh giá lại scope trước. | Không dùng chúng làm điều kiện hoàn thành core khi chưa approved. |
| DFR-05 | Sửa thiếu sót hạ tầng chung đang có (TLS, host exposure, triển khai Recommendation, chuẩn Shared kid/fail-start). | Lead/Duy/Nguyên xử lý thuộc feature hạ tầng, không hợp thức hóa source bằng cách sửa docs chuẩn trong Progress. | Phải ghi đúng gate đỏ/môi trường thiếu; không tuyên bố security/deployment hoàn chỉnh chỉ dựa Phase 1. |
| DFR-06 | Implementation weight provider cho Diet BMR/TDEE và Diet consumer/auto-refresh; Phase 1 chỉ Q10 + T038 tracking, không chuyển thuật toán sang Progress core. | Bảo/Huy duyệt latest/asOf/effective-time/missing/stale/provenance/deadline; Nguyên profile, lead ADR Diet → Progress/Identity, canonical contract rồi feature triển khai riêng. | TargetResponse.Bmr/Tdee hiện nullable và trả null; không claim integration ready hoặc bắt core BMI chờ Q10. |

Q01–Q09 là câu hỏi trong phạm vi core, không bị đẩy ra ngoài để né nghiệm thu;
chúng nằm ở research và chặn task tương ứng trong roadmap.
Q10 là dependency ngược được theo dõi, không phải approval tự động hoặc task code BMR/TDEE.
