using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SignatureApiStarter.SignatureApi;

/// <summary>
/// The app's only SignatureAPI calls. Plain HTTP; there is no SDK.
/// Request and response fields follow https://spec.signatureapi.com/openapi.yaml.
/// </summary>
public class SignatureApiClient(HttpClient http)
{
    public static readonly Uri BaseAddress = new("https://api.signatureapi.com/v1/");

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>Uploads a PDF and returns its temporary URL, usable as a document URL.</summary>
    public async Task<string> UploadDocumentAsync(byte[] pdf, CancellationToken ct = default)
    {
        var content = new ByteArrayContent(pdf);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var upload = await SendAsync<Upload>(new HttpRequestMessage(HttpMethod.Post, "uploads") { Content = content }, ct);
        return upload.Url;
    }

    /// <summary>Creates an envelope with one signer and returns its id.</summary>
    public async Task<string> CreateEnvelopeAsync(
        string documentUrl, string signerName, string signerEmail, string agreementId, CancellationToken ct = default)
    {
        var body = new
        {
            Title = "Sample agreement",
            Documents = new[]
            {
                new
                {
                    Format = "pdf",
                    Url = documentUrl,
                    // Fills the [[signer_signature]] placeholder in the PDF.
                    Places = new[] { new { Key = "signer_signature", Type = "signature", RecipientKey = "signer" } },
                },
            },
            // No ceremony: the API default, email_link, emails the signer.
            Recipients = new[] { new { Type = "signer", Key = "signer", Name = signerName, Email = signerEmail } },
            Metadata = new Dictionary<string, string> { ["agreement_id"] = agreementId },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "envelopes") { Content = JsonContent.Create(body, options: Json) };
        var envelope = await SendAsync<Envelope>(request, ct);
        return envelope.Id;
    }

    /// <summary>Lists an envelope's deliverables. Their URLs expire after an hour, so fetch them when needed.</summary>
    public async Task<IReadOnlyList<Deliverable>> ListDeliverablesAsync(string envelopeId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"envelopes/{Uri.EscapeDataString(envelopeId)}/deliverables");
        var list = await SendAsync<DeliverableList>(request, ct);
        return list.Data;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await SignatureApiException.FromResponseAsync(response, ct);
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new SignatureApiException("Empty response from SignatureAPI", (int)response.StatusCode);
    }

    private record Upload(string Url);

    private record Envelope(string Id);

    private record DeliverableList(List<Deliverable> Data);
}

public record Deliverable(string Id, string Status, string? Url);
