namespace Anvil.Primitives.Validations;

public static class CnpjValidator
{
    private const int _cnpjLength = 14;

    public static bool IsValid(ReadOnlySpan<char> cnpj)
    {
        if (cnpj.Length != _cnpjLength)
        {
            return false;
        }

        var firstDigit = cnpj[0];
        var allRepeated = true;

        for (var i = 0; i < _cnpjLength; i++)
        {
            if (!char.IsAsciiDigit(cnpj[i]))
            {
                return false;
            }

            if (cnpj[i] != firstDigit)
            {
                allRepeated = false;
            }
        }

        if (allRepeated)
        {
            return false;
        }

        var firstCheck = CheckDigitCalculator.CalculateMod11Digit(cnpj[..12], 2, 9);
        if (cnpj[12] != firstCheck)
        {
            return false;
        }

        var secondCheck = CheckDigitCalculator.CalculateMod11Digit(cnpj[..13], 2, 9);
        return cnpj[13] == secondCheck;
    }
}
