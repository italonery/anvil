namespace Anvil.Primitives.Extensions;

public static class DateTimeExtensions
{
    private static readonly TimeZoneInfo BrazilTimeZone = ResolveBrazilTimeZone();

    private static TimeZoneInfo ResolveBrazilTimeZone()
    {
        var tzId = OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo";
        return TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }

    public static DateTime ToBrazilTime(this DateTime utcDate)
    {
        if (utcDate.Kind == DateTimeKind.Local)
        {
            return TimeZoneInfo.ConvertTime(utcDate, TimeZoneInfo.Local, BrazilTimeZone);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDate, BrazilTimeZone);
    }

    public static DateTime? ToBrazilTime(this DateTimeOffset? utcOffsetDate)
    {
        if (utcOffsetDate.HasValue)
        {
            return utcOffsetDate.Value.UtcDateTime.ToBrazilTime();
        }
        else
        {
            return null;
        }
    }

    public static DateTime? ToBrazilTime(this DateTime? utcDate)
    {
        if (utcDate.HasValue)
        {
            return utcDate.Value.ToBrazilTime();
        }
        else
        {
            return null;
        }
    }
}
