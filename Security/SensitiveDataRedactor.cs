using System.Text.RegularExpressions;

namespace Anvil.Security;

public static partial class SensitiveDataRedactor
{
    [GeneratedRegex(@"^/service/.*/settings$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProtectedRouteRegex();

    [GeneratedRegex(@"""password""\s*:\s*""[^""]*""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PasswordJsonFieldRegex();

    public static string RemoveSensitiveData(this string? input, string? route = null)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(route) && ProtectedRouteRegex().IsMatch(route))
        {
            return "[REDACTED_ROUTE_PAYLOAD]";
        }

        if (!input.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        return PasswordJsonFieldRegex().Replace(input, @"""password"":""***""");
    }
}
