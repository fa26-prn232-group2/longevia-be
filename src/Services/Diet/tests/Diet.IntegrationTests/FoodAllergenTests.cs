using System.Net;
using System.Net.Http.Json;
using Diet.Application.Features.Foods;

namespace Diet.IntegrationTests;

public sealed class FoodAllergenTests(DietApiFactory factory) : IClassFixture<DietApiFactory>
{
    [Fact]
    public async Task Catalog_PreservesUnknownAndDeclaredEmpty_AndNormalizesDeclaredTags()
    {
        using var owner = factory.UserClient(Guid.NewGuid());
        var input = new CreateFoodRequest("allergen metadata", "Test", 100, "g", 100, 1, 1, 1, 1);
        var unknown = await owner.PostAsJsonAsync("/foods", input);
        Assert.Equal(HttpStatusCode.Created, unknown.StatusCode);
        Assert.Null((await unknown.Content.ReadFromJsonAsync<FoodResponse>())!.Allergens);
        var empty = await owner.PostAsJsonAsync("/foods", input with { Allergens = [] });
        Assert.Empty((await empty.Content.ReadFromJsonAsync<FoodResponse>())!.Allergens!);
        var tagged = await owner.PostAsJsonAsync("/foods", input with { Allergens = [" Peanuts ", "MILK", "peanuts"] });
        var food = (await tagged.Content.ReadFromJsonAsync<FoodResponse>())!;
        Assert.Equal(new[] { "milk", "peanuts" }, food.Allergens);
        var catalog = await owner.GetFromJsonAsync<FoodPageResponse>("/foods?search=allergen%20metadata");
        Assert.Equal(new[] { "milk", "peanuts" }, catalog!.Items.Single(x => x.Id == food.Id).Allergens);
        using var other = factory.UserClient(Guid.NewGuid());
        Assert.Empty((await other.GetFromJsonAsync<FoodPageResponse>("/foods?search=allergen%20metadata"))!.Items);
    }

    [Fact]
    public async Task Catalog_RejectsEmptyNullOverlongAndTooManyAllergenTags()
    {
        using var client = factory.UserClient(Guid.NewGuid());
        var input = new CreateFoodRequest("invalid metadata", "Test", 100, "g", 100, 1, 1, 1, 1);
        foreach (var tags in new IReadOnlyList<string>[] { [" "], [null!], [new string('x', 51)], Enumerable.Repeat("milk", 33).ToArray() })
        {
            var response = await client.PostAsJsonAsync("/foods", input with { Allergens = tags });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Empty((await client.GetFromJsonAsync<FoodPageResponse>("/foods?search=invalid%20metadata"))!.Items);
    }
}
