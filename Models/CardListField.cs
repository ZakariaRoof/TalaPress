using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TalaPress.Models;

public class CardListFieldValue
{
    [JsonPropertyName("sectionTitle")]
    public string SectionTitle { get; set; } = string.Empty;

    [JsonPropertyName("sectionTitleEn")]
    public string SectionTitleEn { get; set; } = string.Empty;

    [JsonPropertyName("sectionSubtitle")]
    public string SectionSubtitle { get; set; } = string.Empty;

    [JsonPropertyName("sectionSubtitleEn")]
    public string SectionSubtitleEn { get; set; } = string.Empty;

    [JsonPropertyName("sectionSummary")]
    public string SectionSummary { get; set; } = string.Empty;

    [JsonPropertyName("sectionSummaryEn")]
    public string SectionSummaryEn { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<CardListItemValue> Items { get; set; } = new();
}

public class CardListItemValue
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("titleEn")]
    public string TitleEn { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("subtitleEn")]
    public string SubtitleEn { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("descriptionEn")]
    public string DescriptionEn { get; set; } = string.Empty;

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "icon";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("featured")]
    public bool Featured { get; set; }
}

public static class CardListFieldParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CardListFieldValue Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new CardListFieldValue();
        }

        try
        {
            return JsonSerializer.Deserialize<CardListFieldValue>(json, JsonOptions) ?? new CardListFieldValue();
        }
        catch
        {
            return new CardListFieldValue();
        }
    }

    public static CardListFieldValue Parse(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new CardListFieldValue();
        }

        try
        {
            return JsonSerializer.Deserialize<CardListFieldValue>(element.GetRawText(), JsonOptions) ?? new CardListFieldValue();
        }
        catch
        {
            return new CardListFieldValue();
        }
    }

    public static int CountMeaningfulItems(CardListFieldValue value)
    {
        value.Items ??= new List<CardListItemValue>();
        return value.Items.Count(IsMeaningfulItem);
    }

    private static bool IsMeaningfulItem(CardListItemValue item)
    {
        return !string.IsNullOrWhiteSpace(item.Title) ||
               !string.IsNullOrWhiteSpace(item.TitleEn) ||
               !string.IsNullOrWhiteSpace(item.Subtitle) ||
               !string.IsNullOrWhiteSpace(item.SubtitleEn) ||
               !string.IsNullOrWhiteSpace(item.Description) ||
               !string.IsNullOrWhiteSpace(item.DescriptionEn) ||
               !string.IsNullOrWhiteSpace(item.Icon) ||
               !string.IsNullOrWhiteSpace(item.Image) ||
               !string.IsNullOrWhiteSpace(item.Link);
    }

    public static bool TryValidate(CardListFieldValue value, bool isRequired, out string? errorMessage)
    {
        errorMessage = null;
        value.Items ??= new List<CardListItemValue>();

        if (!isRequired)
        {
            return true;
        }

        bool hasItem = value.Items.Any(IsMeaningfulItem);

        if (!hasItem)
        {
            errorMessage = "يجب إضافة عنصر واحد على الأقل يحتوي على عنوان.";
            return false;
        }

        return true;
    }

    public static object ToStorageObject(CardListFieldValue value)
    {
        value.Items ??= new List<CardListItemValue>();
        var normalizedItems = value.Items
            .Select(NormalizeItem)
            .Where(IsMeaningfulItem)
            .ToList();

        bool featuredAssigned = false;
        foreach (var item in normalizedItems)
        {
            if (item.Featured && !featuredAssigned)
            {
                featuredAssigned = true;
                continue;
            }

            item.Featured = false;
        }

        var normalized = new CardListFieldValue
        {
            SectionTitle = value.SectionTitle?.Trim() ?? string.Empty,
            SectionTitleEn = value.SectionTitleEn?.Trim() ?? string.Empty,
            SectionSubtitle = value.SectionSubtitle?.Trim() ?? string.Empty,
            SectionSubtitleEn = value.SectionSubtitleEn?.Trim() ?? string.Empty,
            SectionSummary = value.SectionSummary?.Trim() ?? string.Empty,
            SectionSummaryEn = value.SectionSummaryEn?.Trim() ?? string.Empty,
            Items = normalizedItems
        };

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        return JsonSerializer.Deserialize<object>(json) ?? new CardListFieldValue();
    }

    private static CardListItemValue NormalizeItem(CardListItemValue item)
    {
        var mediaType = ResolveMediaType(item);
        var normalized = new CardListItemValue
        {
            Title = item.Title?.Trim() ?? string.Empty,
            TitleEn = item.TitleEn?.Trim() ?? string.Empty,
            Subtitle = item.Subtitle?.Trim() ?? string.Empty,
            SubtitleEn = item.SubtitleEn?.Trim() ?? string.Empty,
            Description = item.Description?.Trim() ?? string.Empty,
            DescriptionEn = item.DescriptionEn?.Trim() ?? string.Empty,
            MediaType = mediaType,
            Link = item.Link?.Trim() ?? string.Empty,
            Featured = item.Featured
        };

        if (mediaType == "image")
        {
            normalized.Image = item.Image?.Trim() ?? string.Empty;
            normalized.Icon = string.Empty;
        }
        else
        {
            normalized.Icon = NormalizeIcon(item.Icon);
            normalized.Image = string.Empty;
        }

        return normalized;
    }

    private static string ResolveMediaType(CardListItemValue item)
    {
        if (string.Equals(item.MediaType, "image", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (string.Equals(item.MediaType, "icon", StringComparison.OrdinalIgnoreCase))
        {
            return "icon";
        }

        return !string.IsNullOrWhiteSpace(item.Image) ? "image" : "icon";
    }

    private static string NormalizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return string.Empty;
        }

        var clean = icon.Trim().Replace("  ", " ", StringComparison.Ordinal);

        if (Regex.IsMatch(clean, @"^bi[\s-]", RegexOptions.IgnoreCase))
        {
            if (clean.StartsWith("bi ", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[3..].Trim();
            }

            if (!clean.StartsWith("bi-", StringComparison.OrdinalIgnoreCase))
            {
                clean = "bi-" + clean.TrimStart('-');
            }

            return clean;
        }

        var longPrefix = Regex.Match(clean, @"^(fa-solid|fa-regular|fa-brands|fa-light|fa-thin|fa-duotone)\s+(fa-[\w-]+)$", RegexOptions.IgnoreCase);
        if (longPrefix.Success)
        {
            var style = longPrefix.Groups[1].Value.ToLowerInvariant() switch
            {
                "fa-solid" => "fas",
                "fa-regular" => "far",
                "fa-brands" => "fab",
                "fa-light" => "fal",
                "fa-thin" => "fat",
                "fa-duotone" => "fad",
                _ => "fas"
            };
            return $"{style} {longPrefix.Groups[2].Value.ToLowerInvariant()}";
        }

        var shortPrefix = Regex.Match(clean, @"^(fas|far|fab|fal|fat|fad)\s+(fa-[\w-]+)$", RegexOptions.IgnoreCase);
        if (shortPrefix.Success)
        {
            return $"{shortPrefix.Groups[1].Value.ToLowerInvariant()} {shortPrefix.Groups[2].Value.ToLowerInvariant()}";
        }

        if (Regex.IsMatch(clean, @"^fa-[\w-]+$", RegexOptions.IgnoreCase))
        {
            return $"fas {clean.ToLowerInvariant()}";
        }

        if (!clean.Contains(' ', StringComparison.Ordinal))
        {
            return $"fas fa-{clean.TrimStart('-').ToLowerInvariant()}";
        }

        return clean.ToLowerInvariant();
    }
}
