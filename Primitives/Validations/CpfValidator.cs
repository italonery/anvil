namespace Anvil.Primitives.Validations;

public static class CpfValidator
{
    private const int _cpfLength = 11;

    public static bool IsValid(ReadOnlySpan<char> cpf)
    {
        if (cpf.Length != _cpfLength)
        {
            return false;
        }

        var firstDigit = cpf[0];
        var allRepeated = true;

        for (var i = 0; i < _cpfLength; i++)
        {
            if (!char.IsAsciiDigit(cpf[i]))
            {
                return false;
            }

            if (cpf[i] != firstDigit)
            {
                allRepeated = false;
            }
        }

        if (allRepeated)
        {
            return false;
        }

        var firstCheck = CheckDigitCalculator.CalculateMod11Digit(cpf[..9], 2, 11);
        if (cpf[9] != firstCheck)
        {
            return false;
        }

        var secondCheck = CheckDigitCalculator.CalculateMod11Digit(cpf[..10], 2, 11);
        return cpf[10] == secondCheck;
    }
}
