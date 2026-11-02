using System.ComponentModel.DataAnnotations;

namespace Anvil.Primitives.Validations;

public sealed class CepAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.ToString().IsValidCep();
    }
}

public sealed class CpfAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.ToString().IsValidCpf();
    }
}

public sealed class CnpjAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.ToString().IsValidCnpj();
    }
}

public sealed class FullNameAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.ToString().IsValidFullName();
    }
}

public sealed class MobilePhoneAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.ToString().IsValidMobilePhoneNumberBR();
    }
}

public sealed class PasswordPolicyAttribute : ValidationAttribute
{
    public int MinimumLength { get; }
    public bool RequireLetter { get; }
    public bool RequireDigit { get; }

    public PasswordPolicyAttribute(int minimumLength = 8, bool requireLetter = true, bool requireDigit = true)
    {
        MinimumLength = minimumLength;
        RequireLetter = requireLetter;
        RequireDigit = requireDigit;
        ErrorMessage = $"The password must be at least {MinimumLength} characters long and contain letters and numbers.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var password = value as string ?? string.Empty;

        if (password.Length < MinimumLength)
        {
            return new ValidationResult(ErrorMessage);
        }

        if (RequireLetter && !password.Any(char.IsLetter))
        {
            return new ValidationResult(ErrorMessage);
        }

        if (RequireDigit && !password.Any(char.IsDigit))
        {
            return new ValidationResult(ErrorMessage);
        }

        return ValidationResult.Success;
    }
}
