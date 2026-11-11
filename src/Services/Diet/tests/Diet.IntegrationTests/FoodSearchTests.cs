using System.Net;
using System.Net.Http.Json;
using Diet.Application.Features.Foods;

namespace Diet.IntegrationTests;

public sealed class FoodSearchTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    [Theory]
    [InlineData("ca chua", "Cà chua")]
    [InlineData("cà chua", "Cà chua")]
    [InlineData("CA CHUA", "Cà chua")]
    [InlineData("dau phong", "Đậu phộng rang")]
    [InlineData("đậu phộng", "Đậu phộng rang")]
    [InlineData("com trang", "Cơm trắng")]
    public async Task Search_MatchesWithOrWithoutDiacritics(string keyword, string expectedName)
    {
        using var client = factory.UserClient(Guid.NewGuid());

        var page = await client.GetFromJsonAsync<FoodPageResponse>(
            $"/foods?search={Uri.EscapeDataString(keyword)}&pageSize=50");

        Assert.NotNull(page);
        Assert.Contains(page.Items, item =>
            item.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_DoesNotReturnUnrelatedFoods()
    {
        using var client = factory.UserClient(Guid.NewGuid());

        var page = await client.GetFromJsonAsync<FoodPageResponse>(
            "/foods?search=khong%20ton%20tai%20mon%20nay");

        Assert.NotNull(page);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Seed_CatalogHidesDemoSuffixFromUsers()
    {
        using var client = factory.UserClient(Guid.NewGuid());

        var page = await client.GetFromJsonAsync<FoodPageResponse>("/foods?pageSize=50");

        Assert.NotNull(page);
        Assert.DoesNotContain(page.Items, item => item.Name.Contains("(demo)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(page.Items, item => item.Name == "Cơm trắng");
        Assert.Contains(page.Items, item => item.Name == "Ức gà");
        Assert.Contains(page.Items, item => item.Name == "Nước lọc");
    }

    [Fact]
    public async Task Search_StillScopesToOwnAndSharedFoods()
    {
        using var owner = factory.UserClient(Guid.NewGuid());
        var created = await owner.PostAsJsonAsync("/foods",
            new CreateFoodRequest("Món riêng của tôi", "Test", 100, "g", 100, 1, 1, 1, 1));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var other = factory.UserClient(Guid.NewGuid());
        var otherPage = await other.GetFromJsonAsync<FoodPageResponse>(
            "/foods?search=mon%20rieng%20cua%20toi");
        Assert.NotNull(otherPage);
        Assert.Empty(otherPage.Items);
    }
}
