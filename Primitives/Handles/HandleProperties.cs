using System.Globalization;
using System.Text.Json;

namespace Anvil.Primitives.Handles;

public static class HandleProperties
{
    public static DateTime? HandleDate(object? dateTimeObj, string propertyName)
    {
        if (dateTimeObj is null)
        {
            return null;
        }

        if (dateTimeObj is DateTime dateTimeDt)
        {
            return dateTimeDt;
        }

        if (dateTimeObj is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.UtcDateTime;
        }

        if (dateTimeObj is string dateTimeStr)
        {
            if (DateTimeOffset.TryParse(dateTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed.UtcDateTime;
            }

            return null;
        }

        if (dateTimeObj is JsonElement jsonElem)
        {
            if (jsonElem.ValueKind == JsonValueKind.String)
            {
                var raw = jsonElem.GetString();
                if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedElem))
                {
                    return parsedElem.UtcDateTime;
                }
            }

            if (jsonElem.ValueKind == JsonValueKind.Object
                && jsonElem.TryGetProperty(propertyName, out var dateProperty)
                && dateProperty.ValueKind == JsonValueKind.String)
            {
                var rawProp = dateProperty.GetString();
                if (DateTimeOffset.TryParse(rawProp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedProp))
                {
                    return parsedProp.UtcDateTime;
                }
            }
        }

        return null;
    }
}
