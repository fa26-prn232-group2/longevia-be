using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Options;

namespace Recommendation.Api.Extensions;

/// <summary>
/// Bind options từ section cấu hình có key kiểu <c>SCREAMING_SNAKE</c> vào property
/// kiểu <c>PascalCase</c>.
/// </summary>
/// <remarks>
/// Biến môi trường <c>GOOGLE_AI__API_KEY</c> được ASP.NET Core đổi thành key
/// <c>GOOGLE_AI:API_KEY</c>, còn <c>RAG__TOP_K</c> thành <c>RAG:TOP_K</c>. Tên key
/// đó không khớp tên property (<c>ApiKey</c>, <c>TopK</c>) nên
/// <c>OptionsBuilder.Bind</c> không ánh xạ được. Hàm này bỏ qua dấu gạch dưới
/// khi so khớp, nên <c>API_KEY</c> -> <c>ApiKey</c> và <c>TOP_K</c> -> <c>TopK</c>.
/// </remarks>
public static class OptionsBindingExtensions
{
    /// <summary>
    /// Bind section <paramref name="sectionName"/> vào <typeparamref name="TOptions"/>.
    /// Key vắng mặt thì giữ nguyên giá trị mặc định của property.
    /// </summary>
    public static OptionsBuilder<TOptions> BindEnvSection<TOptions>(
        this OptionsBuilder<TOptions> builder,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);

        return builder.Configure(options => BindSection(section, options));
    }

    /// <summary>
    /// Đọc một giá trị cấu hình theo cùng quy tắc so khớp của
    /// <see cref="BindEnvSection{TOptions}"/>: bỏ dấu gạch dưới và so khớp không
    /// phân biệt hoa thường (ví dụ key <c>GRPC_PORT</c> khớp <c>GrpcPort</c>).
    /// Trả về <c>null</c> khi section hoặc key không tồn tại.
    /// </summary>
    public static string? GetEnvValue(
        this IConfiguration configuration,
        string sectionName,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var child = configuration.GetSection(sectionName)
            .GetChildren()
            .FirstOrDefault(c => NamesMatch(c.Key, propertyName));

        return child?.Value;
    }

    /// <summary>
    /// Đọc một giá trị số nguyên từ cấu hình theo cùng quy tắc so khớp của
    /// <see cref="GetEnvValue"/>; trả về <paramref name="defaultValue"/> khi key
    /// vắng mặt hoặc giá trị không phải số nguyên hợp lệ.
    /// </summary>
    public static int GetEnvInt(
        this IConfiguration configuration,
        string sectionName,
        string propertyName,
        int defaultValue)
    {
        var raw = configuration.GetEnvValue(sectionName, propertyName);

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static void BindSection<TOptions>(IConfiguration section, TOptions options)
        where TOptions : class
    {
        var children = section.GetChildren();
        var properties = typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanWrite) continue;

            var child = children.FirstOrDefault(c => NamesMatch(c.Key, property.Name));
            if (child?.Value is not { Length: > 0 } rawValue) continue;

            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            if (!converter.CanConvertFrom(typeof(string))) continue;

            var value = converter.ConvertFromInvariantString(rawValue);
            if (value is not null)
            {
                property.SetValue(options, value);
            }
        }
    }

    private static bool NamesMatch(string configKey, string propertyName) =>
        string.Equals(Normalize(configKey), Normalize(propertyName), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string name) =>
        name.Replace("_", string.Empty, StringComparison.Ordinal);
}
