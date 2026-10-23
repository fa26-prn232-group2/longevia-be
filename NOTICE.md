# NOTICE — Mục đích sử dụng, nội dung bên thứ ba và giấy phép

## 1. Mục đích sử dụng

Đây là **dự án môn học**, được xây dựng **chỉ nhằm mục đích học tập và nghiên cứu**:
thực hành kiến trúc microservices, gRPC, EF Core, RabbitMQ và kỹ thuật RAG
(Retrieval-Augmented Generation).

- **Không** dùng cho mục đích thương mại.
- **Không** phải là sản phẩm đang vận hành thật, không cung cấp dịch vụ cho người dùng thật.
- **Không** phải là thiết bị y tế hay cơ sở khám chữa bệnh.

## 2. Nội dung bên thứ ba có bản quyền

Thư mục [`docs/reference/che-do-an-truong-tho/`](docs/reference/che-do-an-truong-tho/README.md)
chứa bản số hoá (OCR) từ bản in cuốn sách **“Chế độ ăn trường thọ”** của
**Tiến sĩ Valter Longo** (bản dịch tiếng Việt; lời giới thiệu của PGS. TS. Nguyễn Đỗ Huy,
Hà Nội, 2020).

- Bản quyền nội dung thuộc **tác giả, dịch giả và nhà xuất bản**. Dự án **không**
  tuyên bố bất kỳ quyền nào đối với nội dung này.
- Nội dung chỉ được dùng trong **phạm vi học tập/nghiên cứu** của đồ án, **không**
  nhằm mục đích phân phối lại, không bán, không xuất bản lại.
- Văn bản được tạo bằng OCR nên **có thể sai sót**; số liệu, liều lượng và khuyến nghị
  sức khỏe phải đối chiếu bản in trước khi sử dụng.
- **Gỡ bỏ theo yêu cầu**: nếu chủ sở hữu quyền hoặc đại diện hợp pháp yêu cầu, nội dung
  sẽ được gỡ khỏi repository. Cách liên hệ: **mở issue tại repository này**.

> Nếu repository được chuyển sang chế độ công khai, nên thay toàn bộ bản số hoá bằng
> một trích đoạn ngắn (ví dụ 2–3 chương) để giảm rủi ro bản quyền.

## 3. Dữ liệu phái sinh

Seed và vector nhúng (embeddings) trong `recommendation_db` được sinh ra từ nội dung
nêu tại mục 2. Dữ liệu phái sinh này:

- chỉ dùng cho mục đích học tập của đồ án;
- không phải tài sản độc lập để phân phối, không cấp quyền gì thêm đối với nội dung gốc;
- phải được gỡ bỏ cùng lúc với nội dung gốc nếu có yêu cầu tại mục 2.

## 4. Giấy phép mã nguồn

Mã nguồn của dự án được phát hành theo giấy phép [MIT](LICENSE).

> Giấy phép MIT **chỉ áp dụng cho mã nguồn của nhóm**. Nó **không** áp dụng cho nội
> dung sách, bản số hoá OCR, hay dữ liệu phái sinh nêu tại mục 2 và mục 3.

## 5. Thành phần bên thứ ba

Giấy phép dưới đây lấy từ metadata (`.nuspec`) của từng gói NuGet tại thời điểm ghi.
Khi nâng cấp gói, cần đối chiếu lại với metadata chính thức của nhà phát hành.

| Thành phần | Giấy phép |
| --- | --- |
| .NET, ASP.NET Core, Entity Framework Core, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.NET.Test.Sdk, coverlet.collector | MIT |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL License |
| Pgvector, Pgvector.EntityFrameworkCore | MIT |
| Google.GenAI | Apache-2.0 |
| Grpc.AspNetCore, Grpc.AspNetCore.HealthChecks, Grpc.Net.Client, Grpc.Tools | Apache-2.0 |
| Polly | BSD-3-Clause |
| Swashbuckle.AspNetCore | MIT |
| FluentValidation, FluentValidation.DependencyInjectionExtensions | Apache-2.0 |
| xunit, xunit.runner.visualstudio | Apache-2.0 |
| Testcontainers.PostgreSql | MIT |
| MassTransit, MassTransit.RabbitMQ | Apache-2.0 *(chưa xác minh cục bộ trong môi trường hiện tại — đối chiếu lại từ metadata NuGet)* |

Hạ tầng và công cụ chạy:

| Thành phần | Giấy phép |
| --- | --- |
| PostgreSQL | PostgreSQL License |
| pgvector (extension) | PostgreSQL License |
| RabbitMQ | MPL-2.0 |
| Nginx | BSD-2-Clause |
| Docker image dùng trong `deployments/` | Theo giấy phép của từng image |
| Sách “Chế độ ăn trường thọ” (nội dung tri thức) | **Có bản quyền — không phải open source**, xem mục 2 |

## 6. Miễn trừ trách nhiệm

- Mã nguồn được cung cấp **“nguyên trạng”**, không kèm bất kỳ bảo đảm nào, dùng tự chịu rủi ro.
- Nội dung do hệ thống sinh ra (gợi ý món ăn, phân tích vi chất, câu trả lời từ sách)
  **chỉ mang tính tham khảo**, không thay thế tư vấn của bác sĩ hoặc chuyên gia dinh dưỡng.
- Các nhãn hiệu và tên sản phẩm được nhắc tới (Google AI/Gemini, Cloudinary, Brevo,
  Nginx, PostgreSQL, RabbitMQ, .NET và các nhãn khác) thuộc chủ sở hữu tương ứng.
  Dự án **không liên kết, không được bảo trợ và không được chứng thực** bởi các bên đó.

## 7. Yêu cầu gỡ bỏ hoặc đính chính

Mọi yêu cầu gỡ bỏ nội dung hoặc đính chính thông tin: **mở issue tại repository này**.
