namespace Anvil.Messaging.Centrifugo;

public sealed class CentrifugoOptions
{
    public const string SectionName = "Centrifugo";
    public string ServerUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ClientsJwtKey { get; init; } = string.Empty;
}
