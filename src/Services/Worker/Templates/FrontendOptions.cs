using System.ComponentModel.DataAnnotations;

namespace Longevia.Worker.Templates;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    [Url]
    public string BaseUrl { get; init; } = "http://localhost:3000";
}
