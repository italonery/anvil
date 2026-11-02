using System.Text.RegularExpressions;

namespace Anvil.Testing;

public static partial class TestDataGenerator
{
    private static readonly string[] _firstNames =
    [
        "Ana", "Joao", "Mariana", "Carlos", "Luana", "Pedro", "Isabela", "Rafael",
        "Jose", "Julia", "Joana", "Daniel", "Franciele", "Julio", "Luis", "Fernanda",
        "Roberto", "Camila", "Gabriel", "Eduardo", "Patricia", "Marcos", "Lucia", "Ricardo", "Sofia"
    ];

    private static readonly string[] _lastNames =
    [
        "Silva", "Souza", "Ferreira", "Almeida", "Costa", "Moura", "Oliveira",
        "Pereira", "Lima", "Gomes", "Martins", "Ribeiro", "Barbosa", "Cardoso",
        "Rocha", "Dias", "Teixeira", "Nogueira", "Pinto", "Santos", "Cavalcanti"
    ];

    [GeneratedRegex(@"[^a-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex SanitizeEmailRegex();

    public static string GenerateCpf()
    {
        Span<int> cpfDigits = stackalloc int[11];

        for (var i = 0; i < 9; i++)
        {
            cpfDigits[i] = Random.Shared.Next(0, 10);
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += cpfDigits[i] * (10 - i);
        }

        var remainder = sum % 11;
        cpfDigits[9] = remainder < 2 ? 0 : 11 - remainder;

        sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += cpfDigits[i] * (11 - i);
        }

        remainder = sum % 11;
        cpfDigits[10] = remainder < 2 ? 0 : 11 - remainder;

        return string.Create(11, cpfDigits, static (chars, digits) =>
        {
            for (var i = 0; i < 11; i++)
            {
                chars[i] = (char)('0' + digits[i]);
            }
        });
    }

    public static string GenerateMobilePhoneBR()
    {
        var number = Random.Shared.Next(10000000, 99999999);
        return $"719{number:00000000}";
    }

    public static string GenerateName()
    {
        var first = _firstNames[Random.Shared.Next(_firstNames.Length)];
        var last = _lastNames[Random.Shared.Next(_lastNames.Length)];
        var token = Guid.NewGuid().ToString("N")[..4];
        return $"{first} Teste {token} {last}";
    }

    public static string GenerateEmail(string name)
    {
        var sanitized = SanitizeEmailRegex().Replace(name.ToLowerInvariant(), ".");
        var token = Guid.NewGuid().ToString("N")[..8];
        return $"{sanitized}.{token}@example.com";
    }
}
