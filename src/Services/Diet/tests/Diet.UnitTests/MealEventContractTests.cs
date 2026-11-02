using System.Text.Json;
using Longevia.Shared.Events;

namespace Diet.UnitTests;

public sealed class MealEventContractTests
{
    [Fact]
    public void MealEvent_RoundTripsBusinessDate_AndIgnoresFutureFields()
    {
        var id = Guid.NewGuid();
        var original = new MealLoggedEvent(id, Guid.NewGuid(), id, new DateOnly(2026, 9, 1),
            DateTimeOffset.Parse("2026-10-25T01:00:00Z"), DateTimeOffset.Parse("2026-10-25T02:00:00Z"));
        var json = JsonSerializer.Serialize(original, EventJsonOptions.Create());
        using var document = JsonDocument.Parse(json);
        Assert.Equal(6, document.RootElement.EnumerateObject().Count());
        var futureJson = json[..^1] + ",\"FutureField\":true}";
        Assert.Equal(original, JsonSerializer.Deserialize<MealLoggedEvent>(futureJson, EventJsonOptions.Create()));
    }
}
