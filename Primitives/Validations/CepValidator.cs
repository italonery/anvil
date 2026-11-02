namespace Anvil.Primitives.Validations;

public static class CepValidator
{
    public static bool IsValid(ReadOnlySpan<char> cep)
    {
        if (cep.IsEmpty)
        {
            return false;
        }

        if (cep.Length != 8 && cep.Length != 9)
        {
            return false;
        }

        var firstDigit = cep[0];
        var allRepeated = true;

        for (var i = 0; i < cep.Length; i++)
        {
            if (cep.Length == 9 && i == 5)
            {
                if (cep[i] != '-')
                {
                    return false;
                }

                continue;
            }

            if (!char.IsAsciiDigit(cep[i]))
            {
                return false;
            }

            if (cep[i] != firstDigit)
            {
                allRepeated = false;
            }
        }

        return !allRepeated;
    }
}
