using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Anvil.Primitives.Extensions;

public static partial class JsonExtensions
{
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions EnumAsStringOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static readonly JsonSerializerOptions IndentedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    [GeneratedRegex(@"\r\n?|\n|\u2028|\u2029", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultiSpaceRegex();

    public static string Unescape(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var singleLine = LineBreakRegex().Replace(input, " ");
        return MultiSpaceRegex().Replace(singleLine, " ").Trim();
    }

    public static string? JsonSerializeWithIndentation<T>(this T? obj)
    {
        if (obj is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(obj, IndentedOptions);
    }

    public static string? JsonSerialize<T>(this T? obj, bool enumAsString = false)
    {
        if (obj is null)
        {
            return null;
        }

        var options = enumAsString ? EnumAsStringOptions : DefaultOptions;
        return JsonSerializer.Serialize(obj, options);
    }

    public static T? JsonDeserialize<T>(this string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, DefaultOptions);
    }

    public static T? SafeDeserialize<T>(JsonElement? json)
    {
        if (json is null || json.Value.ValueKind == JsonValueKind.Null || json.Value.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        return json.Value.Deserialize<T>(DefaultOptions);
    }

    public static T? TryGetJsonField<T>(this object? data, string fieldPath)
    {
        if (data is null || string.IsNullOrWhiteSpace(fieldPath))
        {
            return default;
        }

        JsonElement rootElement;
        if (data is JsonElement element)
        {
            rootElement = element;
        }
        else if (data is JsonDocument document)
        {
            rootElement = document.RootElement;
        }
        else
        {
            rootElement = JsonSerializer.SerializeToElement(data, DefaultOptions);
        }

        if (TryGetJsonPathValue(rootElement, fieldPath, out var targetElement) && targetElement.ValueKind is not JsonValueKind.Null && targetElement.ValueKind is not JsonValueKind.Undefined)
        {
            return targetElement.Deserialize<T>(DefaultOptions);
        }

        return default;
    }

    private static bool TryGetJsonPathValue(JsonElement jsonElement, string fieldPath, out JsonElement value)
    {
        value = jsonElement;
        var segments = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out var nextValue))
            {
                value = default;
                return false;
            }

            value = nextValue;
        }

        return true;
    }
}

public sealed class StringOrNumberToStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetInt64().ToString(),
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException($"Unexpected token type '{reader.TokenType}' when reading string.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
