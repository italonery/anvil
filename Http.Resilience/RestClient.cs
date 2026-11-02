using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Anvil.Primitives;
using Microsoft.Extensions.Logging;

namespace Anvil.Http.Resilience;

public sealed class RestClient
{
    private static readonly JsonSerializerOptions _webJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientPoolFactory _clientFactory;
    private readonly ILogger<RestClient> _logger;

    public RestClient(IHttpClientPoolFactory clientFactory, ILogger<RestClient> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        TRequest requestData,
        string? bearerToken,
        string endpoint,
        string poolName = "default",
        int maxTimeoutSeconds = 120,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _clientFactory.GetClient(poolName, maxTimeoutSeconds);
            var json = JsonSerializer.Serialize(requestData, _webJsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            AttachHeaders(request, bearerToken, additionalHeaders);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ParseResponseAsync<TResponse>(response, endpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST request failed for endpoint {Endpoint}", endpoint);
            return Result.Failure<TResponse>(Error.Unavailable(ErrorCode.NetworkError, ex.Message, "503"));
        }
    }

    public async Task<Result<TResponse>> GetAsync<TResponse>(
        string? bearerToken,
        string endpoint,
        string poolName = "default",
        int maxTimeoutSeconds = 120,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _clientFactory.GetClient(poolName, maxTimeoutSeconds);
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            AttachHeaders(request, bearerToken, additionalHeaders);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ParseResponseAsync<TResponse>(response, endpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET request failed for endpoint {Endpoint}", endpoint);
            return Result.Failure<TResponse>(Error.Unavailable(ErrorCode.NetworkError, ex.Message, "503"));
        }
    }

    private static void AttachHeaders(HttpRequestMessage request, string? bearerToken, IReadOnlyDictionary<string, string>? additionalHeaders)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (additionalHeaders is not null)
        {
            foreach (var header in additionalHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    private static async Task<Result<TResponse>> ParseResponseAsync<TResponse>(HttpResponseMessage response, string endpoint, CancellationToken cancellationToken)
    {
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var data = JsonSerializer.Deserialize<TResponse>(responseContent, _webJsonOptions);
            return Result.Success(data!);
        }
        else
        {
            return Result.Failure<TResponse>(
                Error.Failure(
                    ErrorCode.ExternalServiceError,
                    $"Request to '{endpoint}' failed with status {(int)response.StatusCode}.",
                    ((int)response.StatusCode).ToString(),
                    responseContent));
        }
    }
}
