namespace Recommendation.Domain.Constants;

/// <summary>
/// Sở thích ăn uống đã khai báo trong hồ sơ người dùng (FR-003). Giá trị khớp
/// với <c>dietary_preference</c> trong contract gRPC.
/// </summary>
public enum DietaryPreference
{
    /// <summary>Không khai báo / ăn uống cân bằng — không lọc.</summary>
    None = 0,

    /// <summary>Keto — hạn chế mạnh carbohydrate.</summary>
    Keto = 1,

    /// <summary>Thuần chay — loại toàn bộ thực phẩm có nguồn gốc động vật.</summary>
    Vegan = 2,

    /// <summary>Địa Trung Hải — không loại trừ, chỉ là ưu tiên khi mô hình chọn.</summary>
    Mediterranean = 3,

    /// <summary>Ít tinh bột — hạn chế carbohydrate ở mức vừa.</summary>
    LowCarb = 4,
}
