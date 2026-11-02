using Anvil.Primitives;
using Anvil.Primitives.Extensions;
using Centrifugal.Centrifugo.Api;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Error = Anvil.Primitives.Error;

namespace Anvil.Messaging.Centrifugo;

public interface IRealtimePublisher
{
    Task<Result<ResultNone>> PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default);
}

public sealed class CentrifugoPublisher : IRealtimePublisher, IDisposable
{
    private readonly CentrifugoOptions _options;
    private readonly ILogger<CentrifugoPublisher> _logger;
    private readonly GrpcChannel _channel;
    private readonly CentrifugoApi.CentrifugoApiClient _client;
    private readonly Metadata _metadata;

    public CentrifugoPublisher(IOptions<CentrifugoOptions> options, ILogger<CentrifugoPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
        {
            throw new ArgumentException("Centrifugo ServerUrl is required.", nameof(options));
        }

        _metadata = new Metadata
        {
            { "Authorization", $"apikey {_options.ApiKey}" }
        };

        _channel = GrpcChannel.ForAddress(_options.ServerUrl);
        _client = new CentrifugoApi.CentrifugoApiClient(_channel);
    }

    public async Task<Result<ResultNone>> PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return Result.Failure<ResultNone>(Error.Validation(nameof(channel), "Channel name is required."));
        }

        try
        {
            var json = payload.JsonSerialize() ?? "{}";
            var request = new PublishRequest
            {
                Channel = channel,
                Data = ByteString.CopyFromUtf8(json)
            };

            var response = await _client.PublishAsync(request, _metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.Error is null)
            {
                return Result.Success(new ResultNone());
            }
            else
            {
                _logger.LogError("Centrifugo publish error on channel '{Channel}': {Code} - {Message}", channel, response.Error.Code, response.Error.Message);
                return Result.Failure<ResultNone>(Error.Failure(ErrorCode.MessagingError, response.Error.Message, response.Error.Code.ToString()));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception publishing to Centrifugo channel '{Channel}'", channel);
            return Result.Failure<ResultNone>(Error.Unavailable(ErrorCode.MessagingError, ex.Message));
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}
