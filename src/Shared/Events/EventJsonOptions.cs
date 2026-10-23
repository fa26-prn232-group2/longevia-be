using System.Text.Json;
using System.Text.Json.Serialization;

namespace Longevia.Shared.Events;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for Longevia async event contracts.
/// Consumers MUST ignore unknown fields so additive optional fields stay
/// backward compatible (contract policy in <c>contracts/events/v1/README.md</c>).
/// System.Text.Json already skips unmapped members by default; this helper sets
/// it explicitly so the intent survives option-copying (e.g. MassTransit wiring
/// in the Worker, which must apply these settings to its serializer options).
/// Naming policy is deliberately left untouched: producer and consumer share the
/// same record types, so default (as-written) naming stays consistent on both ends.
/// </summary>
public static class EventJsonOptions
{
    /// <summary>Creates event serializer options with forward-compatible defaults.</summary>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions();
        Configure(options);
        return options;
    }

    /// <summary>
    /// Applies the event-contract settings to an existing options instance
    /// (e.g. the serializer options object owned by the MassTransit configuration).
    /// </summary>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        options.PropertyNameCaseInsensitive = true;
    }
}
