using System.Text.Json;

namespace OneCart.Api.Catalog;

public static class CatalogTranslations
{
    public static readonly string[] Languages = ["en", "bn", "es"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, Dictionary<string, string>> Parse(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json ?? "{}", JsonOptions)
                ?? new(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    public static string Serialize(Dictionary<string, Dictionary<string, string>>? translations) =>
        JsonSerializer.Serialize(translations ?? new(), JsonOptions);

    public static string Resolve(Dictionary<string, Dictionary<string, string>> translations, string language, string key, string fallback)
    {
        if (language != "en" && translations.TryGetValue(language, out var localized)
            && localized.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;
        return fallback;
    }

    public static Dictionary<string, string[]> Validate(Dictionary<string, Dictionary<string, string>>? translations, string field)
    {
        var errors = new Dictionary<string, string[]>();
        if (translations is null) return errors;
        foreach (var (language, values) in translations)
        {
            if (!Languages.Contains(language, StringComparer.Ordinal))
                errors[$"{field}.{language}"] = ["Language must be en, bn, or es."];
            if (values is null || values.Count > 250 || values.Any(item => item.Key.Length > 240 || item.Value?.Length > 8000))
                errors[$"{field}.{language}"] = ["Translations have exceeded a field or value limit."];
        }
        return errors;
    }
}
