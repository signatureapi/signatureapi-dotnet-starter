using System.Net.Http.Json;
using System.Text.Json;

namespace SignatureApiStarter.SignatureApi;

/// <summary>A SignatureAPI error response (RFC 9457 problem details).</summary>
public class SignatureApiException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    internal static async Task<SignatureApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        Problem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<Problem>(SignatureApiClient.Json, ct);
        }
        catch (JsonException)
        {
        }

        // Validation errors (422) list each problem in `errors`; other errors carry `detail`.
        var message = problem?.Errors is { Count: > 0 } errors
            ? string.Join("; ", errors)
            : problem?.Detail ?? problem?.Title ?? $"HTTP {status}";
        return new SignatureApiException(message, status);
    }

    private record Problem(string? Title, string? Detail, List<string>? Errors);
}
