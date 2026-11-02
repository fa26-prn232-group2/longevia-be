# Gửi Nguyên — Identity Service (dev 1)

Huy (dev 2) cần profile cho BMR/TDEE và meal AI; Bảo cần height cho BMI.

## Đã có và phần còn thiếu

Main 2e249a2 đã có HeightCm decimal numeric(5,2), profile validation và
UserProfileUpdatedEvent v1 qua outbox. Event chỉ UserId/OccurredOn; identity.proto
vẫn không có RPC. Vì vậy Diet/Progress chưa lấy được dữ liệu sức khỏe qua contract.
Không dùng REST liên service, DB chéo hoặc thêm health claims vào JWT để đi tắt.

## Nhờ Nguyên cung cấp

- Chốt cùng lead ADR cho Diet → Identity và Progress → Identity, hoặc event
  projection có payload/profile version được duyệt; hiện các chiều gRPC này chưa
  có trong whitelist kiến trúc.
- Nếu chọn gRPC: profile read theo UserId, presence rõ (user/profile không tồn tại
  khác transport unavailable), BirthDate, BiologicalSex, HeightCm, ActivityLevel,
  DietaryPreference, Allergies, UpdatedAt/profile version. Không đoán giá trị thiếu.
- Huy sẽ viết adapter Diet sau khi canonical proto và ADR có; Nguyên sở hữu
  provider, identity_db và migrations Identity. Thống nhất deadline/error mapping.
- Chốt với Bảo/Duy chính sách JWT: ES256/P-256, kid bắt buộc hay không, rotation,
  issuer/audience/lifetime, một sub Guid hợp lệ và public-key-only cho consumer.
  Shared hiện fallback current key khi thiếu kid; Progress PR #11 từ chối thiếu kid.
  Không báo auth policy đồng nhất trước khi có quyết định/test chéo.

## Kiểm chứng chung cần có

Identity phát token thật → Diet/Progress xác thực; sai issuer/audience/expired/kid/
sub phải theo policy đã duyệt. Profile read có đủ trường, thiếu profile và lỗi
transport phân biệt, không log token hoặc dữ liệu sức khỏe. Profile update/invalidate
không đồng nghĩa consumer đã có snapshot.

BMR/TDEE vẫn thuộc Diet; Identity không cần tính thay. Đây là yêu cầu phối hợp,
không phải chỉ dẫn sửa API public hay payload profile event hiện hữu ngay lập tức.

## Phần Huy đã kiểm chứng thêm

Compose map đúng Jwt__SigningPrivateKey/SigningPublicKey/KeyId, issuer/audience và
RabbitMQ credentials (không đổi tên .env bên ngoài, không sửa source Identity).
Script scripts/smoke/identity-diet-smoke.ps1 đã qua register → broker → Worker →
Mailpit → verify → login → đọc/ghi Diet bằng token thật. Đường hợp lệ đã chạy;
policy missing kid/rotation và profile provider vẫn cần Nguyên/Bảo/lead chốt.
