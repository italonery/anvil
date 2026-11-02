using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Anvil.Primitives.Validations;

public static partial class StringValidationExtensions
{
    [GeneratedRegex(@"[^\d]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonDigitsRegex();

    [GeneratedRegex(@"^(?:(?:\+|00)?55\s?)?(?:\(?[1-9][0-9]\)?\s?)?9[1-9]\d{3}-?\d{4}$", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
    private static partial Regex BrazilMobilePhoneRegex();

    [GeneratedRegex(@"[^A-Za-zÀ-ÖØ-öø-ÿ\s]", RegexOptions.CultureInvariant)]
    private static partial Regex NonLettersRegex();

    public static string MaskEmail(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var parts = input.Split('@');
        if (parts.Length != 2)
        {
            return "*****@***";
        }

        var name = parts[0];
        var domain = parts[1];
        var suffix = name.Length <= 3 ? name : name[^3..];

        return $"*****{suffix}@{domain}";
    }

    public static string MaskPhone(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var digits = input.ToOnlyNumbers();
        if (digits.Length <= 3)
        {
            return new string('*', digits.Length);
        }

        var last3 = digits[^3..];
        var maskedLength = digits.Length - 3;

        return string.Concat(new string('*', maskedLength), last3);
    }

    public static string ToOnlyNumbers(this string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return NonDigitsRegex().Replace(input, string.Empty);
    }

    public static bool IsValidCep(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return CepValidator.IsValid(input.AsSpan());
    }

    public static bool IsValidCnpj(this string? input, bool normalize = true)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var candidate = normalize ? input.ToOnlyNumbers() : input;
        return CnpjValidator.IsValid(candidate.AsSpan());
    }

    public static bool IsValidCpf(this string? input, bool normalize = true)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var candidate = normalize ? input.ToOnlyNumbers() : input;
        return CpfValidator.IsValid(candidate.AsSpan());
    }

    public static bool IsValidFullName(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1;
    }

    public static bool IsValidEmail(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmedEmail = input.Trim();
        if (trimmedEmail.EndsWith('.'))
        {
            return false;
        }

        return MailAddress.TryCreate(trimmedEmail, out var addr) && addr.Address == trimmedEmail;
    }

    public static bool IsValidMobilePhoneNumberBR(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return BrazilMobilePhoneRegex().IsMatch(input);
    }

    public static string UpperFirstWordLetter(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }

    public static string StripNonLetters(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return NonLettersRegex().Replace(input, string.Empty);
    }
}
