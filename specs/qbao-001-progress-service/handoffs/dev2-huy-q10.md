# Handoff gửi Huy (Diet Service Owner) — Q10 Weight gRPC Provider & Q01 UTC

Chào Huy,

Bảo gửi Huy thông tin bàn giao chính thức cho hợp đồng gRPC **Diet → Progress** để lấy cân nặng gần nhất tính toán BMR/TDEE theo phê duyệt Q10 và [ADR 003](../../../docs/decisions/003-diet-to-progress-weight-grpc.md).

Phía Progress đã hoàn tất toàn bộ server implementation, cấu hình Kestrel HTTP/2 trên cổng 50051, và vượt qua 20 test cases + flow End-to-End. Server đã **READY** để Huy kết nối client.

---

## 1. Thông số Kỹ thuật & Địa chỉ Kết nối

- **Proto File**: `contracts/progress/v1/progress.proto`
- **Protobuf Package**: `longevia.progress.v1`
- **C# Namespace**: `Longevia.Progress.Contracts.V1`
- **RPC Service**: `ProgressService`
- **RPC Method**: `rpc GetLatestWeight (GetLatestWeightRequest) returns (GetLatestWeightResponse)`
- **Địa chỉ kết nối Docker Compose**: `http://progress:50051` (HTTP/2 cleartext trong mạng `data_net`).
- **Deadline khuyến nghị**: 2000ms (`deadline: DateTime.UtcNow.AddSeconds(2)`).

---

## 2. Ngữ nghĩa Wire & Xử lý Dữ liệu

1. **User ID**:
   - `request.UserId`: Bắt buộc chuỗi GUID RFC 4122 không rỗng. Nếu rỗng hoặc sai format, server trả về `INVALID_ARGUMENT`.
2. **Mốc thời gian (As-Of)**:
   - `request.AsOfUtc`: Trường tùy chọn (`optional Timestamp`).
   - Nếu không truyền: Server tự động lấy `DateTimeOffset.UtcNow` hiện tại của server.
   - Nếu truyền mốc ở tương lai: Server clamp mốc hiệu lực về `UtcNow` hiện tại của server.
   - Các bản ghi có `LoggedAt > server UtcNow` luôn bị loại bỏ.
   - Sắp xếp tất định: `ORDER BY LoggedAt DESC, Id DESC LIMIT 1`.
3. **Biểu diễn Cân nặng (Precision)**:
   - `WeightRecord.weight_kg_x100`: Kiểu `int32`, giá trị kg × 100 (ví dụ: `70.50 kg` -> `7050`, `68.25 kg` -> `6825`).
   - Phía Diet chuyển đổi: `decimal weightKg = response.Record.WeightKgX100 / 100m;`.
4. **Phân biệt Dữ liệu Vắng mặt vs Lỗi Hệ thống**:
   - `oneof result`:
     - Nếu user chưa có cân nặng: `response.ResultCase == GetLatestWeightResponse.ResultOneofCase.NoRecordFound` (`response.NoRecordFound == true`). Server trả về gRPC `OK`. Tuyệt đối không trả về 0. Phía Diet có thể giữ BMR/TDEE là `null` hoặc yêu cầu user nhập thủ công.
     - Nếu có cân nặng: `response.ResultCase == GetLatestWeightResponse.ResultOneofCase.Record` (`response.Record` chứa `weight_kg_x100`, `measured_at`, `source_metric_id`).
   - Khi Progress DB gặp sự cố hoặc timeout: Server trả về gRPC status code (`UNAVAILABLE`, `DEADLINE_EXCEEDED`, `INTERNAL`), không bao giờ biến thành `no_record_found`.
5. **Snapshot Immutability (Phía Diet)**:
   - Khi Diet tính toán BMR/TDEE và tạo/cập nhật target dinh dưỡng, Diet snapshot giá trị cân nặng đã lấy vào bảng của Diet.
   - Các bản ghi target trong quá khứ không tự động recompute khi cân nặng thay đổi sau này trong Progress.

---

## 3. Ví dụ Mã Gọi Client Phía Diet

```csharp
// Đăng ký GrpcClient trong DI của Diet (Diet.Infrastructure)
services.AddGrpcClient<ProgressService.ProgressServiceClient>(options =>
{
    options.Address = new Uri(configuration["ProgressGrpc:Address"] ?? "http://progress:50051");
});

// Gọi lấy cân nặng khi tính BMR/TDEE
try
{
    var request = new GetLatestWeightRequest
    {
        UserId = userId.ToString()
        // AsOfUtc = Timestamp.FromDateTimeOffset(...) // Tùy chọn
    };

    var response = await progressClient.GetLatestWeightAsync(
        request,
        deadline: DateTime.UtcNow.AddSeconds(2),
        cancellationToken: cancellationToken);

    if (response.ResultCase == GetLatestWeightResponse.ResultOneofCase.Record)
    {
        decimal weightKg = response.Record.WeightKgX100 / 100m;
        DateTimeOffset measuredAt = response.Record.MeasuredAt.ToDateTimeOffset();
        Guid metricId = Guid.Parse(response.Record.SourceMetricId);

        // Tiến hành tính BMR/TDEE với weightKg và snapshot vào Diet target...
    }
    else
    {
        // no_record_found: User chưa có cân nặng nào trong hệ thống
        // Xử lý fallback: giữ BMR/TDEE null hoặc yêu cầu user nhập target thủ công
    }
}
catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
{
    // Progress Service tạm thời không khả dụng hoặc quá hạn 2s
    logger.LogWarning(ex, "Progress Service Weight gRPC call failed: {Status}", ex.StatusCode);
    // Xử lý fallback phù hợp
}
```

---

## 4. Yêu cầu Triển khai (Deployment)

- Compose project: cả Diet và Progress cùng tham gia mạng `data_net`.
- Biến môi trường Diet cấu hình trỏ sang Progress: `ProgressGrpc__Address: http://progress:50051`.
- Cổng 50051 là cổng nội bộ, không expose ra host và không route qua Nginx Gateway (tuân thủ ADR 001 §2.2).

---

## 5. Nhờ Huy Xác nhận Q01 (UTC Calendar MVP)

Về quy ước ngày nghiệp vụ Q01:
- Nội bộ Progress đã chốt MVP dùng lịch **UTC** cho mọi ngày do server tự suy ra (water today, sleep wake date, metric activity, streak calculation, week boundary bắt đầu thứ Hai 00:00 UTC).
- Khi tiêu thụ sự kiện bữa ăn của Diet (`MealLoggedEvent`), Progress luôn tôn trọng nguyên văn trường `BusinessDate` do Diet gửi, không suy đoán lại từ timestamp.
- Đối với trường hợp frontend tạo bữa ăn mới cho ngày hiện tại (không chọn ngày quá khứ): Frontend nên gửi `DateOnly` theo giờ UTC hiện tại.
- **Nhờ Huy xác nhận**: Cách hiểu này có đồng bộ với logic hiện tại của Diet/Frontend không? Nếu Huy có lưu ý gì thêm về local midnight Việt Nam, báo lại Bảo để hai bên thống nhất nhé.
