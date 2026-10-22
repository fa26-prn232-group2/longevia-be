using Recommendation.Application.Features.Meals;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Meals;

/// <summary>
/// T047 — lọc dị ứng phải loại 100% món khớp thành phần dị ứng, không phụ
/// thuộc cách viết (hoa thường, có dấu/không dấu, khớp chứa).
/// </summary>
public sealed class AllergenGuardTests
{
    private readonly AllergenGuard _guard = new();

    [Fact]
    public void RemovesFoodWhenAllergenMatchesExactly()
    {
        CandidateFood[] foods =
        [
            Food("f-peanut", "Bánh quy bơ", "đậu phộng"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, ["đậu phộng"]);

        var remaining = Assert.Single(result);
        Assert.Equal("f-rice", remaining.FoodId);
    }

    [Fact]
    public void RemovesFoodWhenAllergenContainsTheDeclaredOne()
    {
        CandidateFood[] foods =
        [
            Food("f-sauce", "Gỏi cuốn", "bơ đậu phộng rang"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, ["đậu phộng"]);

        Assert.Single(result);
        Assert.Equal("f-rice", result[0].FoodId);
    }

    [Fact]
    public void RemovesFoodIgnoringCaseDifference()
    {
        CandidateFood[] foods =
        [
            Food("f-nut", "Thanh hạt", "Peanut"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, ["PEANUT"]);

        Assert.Single(result);
        Assert.Equal("f-rice", result[0].FoodId);
    }

    [Fact]
    public void RemovesFoodWhenAllergyIsWrittenWithoutDiacritics()
    {
        CandidateFood[] foods =
        [
            Food("f-nut", "Chè", "Đậu phộng rang"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, ["dau phong"]);

        Assert.Single(result);
        Assert.Equal("f-rice", result[0].FoodId);
    }

    [Fact]
    public void RemovesFoodWhenAllergenListIsEmptyButNameContainsAllergen()
    {
        CandidateFood[] foods =
        [
            Food("f-shrimp", "Gỏi cuốn tôm"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, ["tôm"]);

        Assert.Single(result);
        Assert.Equal("f-rice", result[0].FoodId);
    }

    [Fact]
    public void RemovesShellfishWhenUserIsAllergicToSeafoodGroup()
    {
        CandidateFood[] foods =
        [
            Food("f-shrimp", "Tôm hấp", "tôm"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, ["hải sản"]);

        Assert.Single(result);
        Assert.Equal("f-rice", result[0].FoodId);
    }

    [Fact]
    public void KeepsAllFoodsWhenProfileHasNoAllergy()
    {
        CandidateFood[] foods =
        [
            Food("f-nut", "Chè", "đậu phộng"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, []);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void RemovesFoodWhenUserAllergyIsMoreSpecificThanCatalogEntry()
    {
        CandidateFood[] foods =
        [
            Food("f-milk", "Sữa chua Hy Lạp", "sữa"),
            Food("f-rice", "Cơm gạo lứt"),
        ];

        var result = _guard.Filter(foods, ["sữa bò"]);

        Assert.Single(result);
        Assert.Equal("f-rice", result[0].FoodId);
    }

    private static CandidateFood Food(string foodId, string name, params string[] allergens) => new()
    {
        FoodId = foodId,
        Name = name,
        Calories = 150,
        ProteinGrams = 5,
        CarbsGrams = 20,
        FatGrams = 4,
        FiberGrams = 2,
        Allergens = allergens,
    };
}
