using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SignatureApiStarter.Data;

namespace SignatureApiStarter.Tests;

public partial class AgreementsTests : IDisposable
{
    private readonly AppFactory _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task HomePageListsAgreements()
    {
        var agreement = await _app.AddAgreementAsync(AgreementStatus.Completed);

        var html = await _app.CreateClient().GetStringAsync("/");

        Assert.Contains($"data-envelope-id=\"{agreement.EnvelopeId}\"", html);
        Assert.Contains("data-status=\"completed\">Completed</span>", html);
        Assert.Contains($"href=\"/agreements/{agreement.Id}/signed.pdf\"", html);
    }

    [Fact]
    public async Task SendUploadsCreatesEnvelopeAndSavesAgreement()
    {
        _app.SignatureApi.Respond = (method, path) => (method.Method, path) switch
        {
            ("POST", "/v1/uploads") => FakeSignatureApi.Json(HttpStatusCode.Created,
                """{"id":"upl_1","url":"https://api.signatureapi.com/v1/uploads/upl_1"}"""),
            ("POST", "/v1/envelopes") => FakeSignatureApi.Json(HttpStatusCode.Created,
                """{"id":"8f3e7c1a-0000-4000-8000-000000000001","status":"processing"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
        var client = _app.CreateNoRedirectClient();

        var response = await SubmitFormAsync(client, "Jane Doe", "jane@example.com");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);

        var upload = _app.SignatureApi.Requests[0];
        Assert.Equal("application/pdf", upload.ContentType);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(upload.Body, 0, 5));

        var html = await client.GetStringAsync("/");
        var agreementId = AgreementIdPattern().Match(html).Groups[1].Value;
        Assert.Contains("data-envelope-id=\"8f3e7c1a-0000-4000-8000-000000000001\"", html);
        Assert.Contains("data-status=\"sent\"", html);

        var envelope = _app.SignatureApi.Requests[1];
        Assert.Equal("application/json", envelope.ContentType);
        var expected = $$"""
            {
              "title": "Sample agreement",
              "documents": [{
                "format": "pdf",
                "url": "https://api.signatureapi.com/v1/uploads/upl_1",
                "places": [{ "key": "signer_signature", "type": "signature", "recipient_key": "signer" }]
              }],
              "recipients": [{ "type": "signer", "key": "signer", "name": "Jane Doe", "email": "jane@example.com" }],
              "metadata": { "agreement_id": "{{agreementId}}" }
            }
            """;
        Assert.Equal(Normalize(expected), Normalize(System.Text.Encoding.UTF8.GetString(envelope.Body)));
    }

    [Fact]
    public async Task SendShowsApiErrors()
    {
        _app.SignatureApi.Respond = (_, _) => FakeSignatureApi.Json(HttpStatusCode.Unauthorized,
            """{"type":"https://signatureapi.com/docs/v1/errors/invalid-api-key","title":"Invalid API Key","status":401,"detail":"Please provide a valid API key in the X-API-Key header."}""",
            "application/problem+json");
        var client = _app.CreateNoRedirectClient();

        var response = await SubmitFormAsync(client, "Jane Doe", "jane@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("role=\"alert\">Could not create the envelope: Please provide a valid API key in the X-API-Key header.</p>", html);
        Assert.Contains("No agreements yet.", html);
    }

    private static async Task<HttpResponseMessage> SubmitFormAsync(HttpClient client, string name, string email)
    {
        // The form carries an antiforgery token (and a cookie) like any Razor Pages form.
        var page = await client.GetStringAsync("/");
        var token = AntiforgeryPattern().Match(page).Groups[1].Value;
        return await client.PostAsync("/agreements", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["signer_name"] = name,
            ["signer_email"] = email,
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static string Normalize(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();

    [GeneratedRegex("data-agreement-id=\"([^\"]+)\"")]
    private static partial Regex AgreementIdPattern();
}
