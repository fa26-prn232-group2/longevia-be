# Coding Conventions (C# / .NET)

Ràng buộc kỹ thuật chung: `Directory.Build.props` bật `Nullable`, `ImplicitUsings`,
và `TreatWarningsAsErrors` — cảnh báo là lỗi build.

## Naming

- Namespace/class/method/property: `PascalCase`; biến tham số: `camelCase`;
  private field: `_camelCase`.
- Interface: `I` + `PascalCase` (`IEmailSender`).
- Async method: hậu tố `Async`.
- Hằng số: `PascalCase`; không dùng `SCREAMING_CASE`.

## Ngôn ngữ & cú pháp

- File-scoped namespace (`namespace X;`).
- Bật nullable, không dùng `!` bừa; xử lý null tường minh.
- `var` khi kiểu hiển nhiên; kiểu rõ khi gây mơ hồ.
- Ưu tiên `record` cho DTO/contract bất biến.
- `async/await` xuyên suốt; không `.Result`/`.Wait()`.
- Truyền `CancellationToken` xuống các call async.
- Dùng `DateTimeOffset` (UTC) cho thời điểm.

## Cấu trúc

- Một class công khai mỗi file; tên file = tên class.
- Giữ file/method ngắn; tách khi quá dài (>~300 dòng/file, >~40 dòng/method).
- Không comment code chết; xóa đi. Comment chỉ để giải thích "tại sao".

## API / endpoint

- Endpoint mỏng: validate → gọi use case → trả response.
- Không nghiệp vụ trong controller/middleware.
- DTO request/response tách khỏi entity domain.
- Validate input bằng validator; lỗi trả theo chuẩn ở [../api/README.md](../api/README.md).

## Dependency Injection

- Đăng ký trong composition root; constructor injection; không service locator.
- `AddScoped` cho DbContext/use case; `AddSingleton` cho stateless service/thống kê.
- Adapter ngoài đăng ký qua interface.

## Logging

- Structured logging (`ILogger<T>` với message template), không nối chuỗi.
- Không log secret, token, mật khẩu, dữ liệu cá nhân nhạy cảm.
