namespace Anvil.Primitives.Validations;

public static class CheckDigitCalculator
{
    public static char CalculateMod11Digit(ReadOnlySpan<char> digits, int minMultiplier, int maxMultiplier)
    {
        var sum = 0;
        var multiplier = minMultiplier;


        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            sum += digit * multiplier;

            multiplier++;
            if (multiplier > maxMultiplier)
            {
                multiplier = minMultiplier;
            }
        }

        var remainder = sum % 11;
        var result = 11 - remainder;

        if (result >= 10)
        {
            return '0';
        }

        return (char)('0' + result);
    }
}
