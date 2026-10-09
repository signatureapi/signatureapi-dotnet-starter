using System.Net;
using System.Text;
using SignatureApiStarter.Data;
using StandardWebhooks;

namespace SignatureApiStarter.Tests;

public class WebhookTests : IDisposable
{
    private readonly AppFactory _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task ValidEventUpdatesStatus()
    {
        var agreement = await _app.AddAgreementAsync();

        var response = await PostAsync(Event("envelope.completed", agreement.EnvelopeId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AgreementStatus.Completed, (await _app.FindAgreementAsync(agreement.Id))!.Status);
    }

    [Theory]
    [InlineData("recipient.rejected", AgreementStatus.Rejected)]
    [InlineData("envelope.failed", AgreementStatus.Failed)]
    [InlineData("envelope.canceled", AgreementStatus.Canceled)]
    public async Task MapsEventTypesToStatuses(string type, string status)
    {
        var agreement = await _app.AddAgreementAsync();

        await PostAsync(Event(type, agreement.EnvelopeId));

        Assert.Equal(status, (await _app.FindAgreementAsync(agreement.Id))!.Status);
    }

    [Fact]
    public async Task BadSignatureIsRejected()
    {
        var agreement = await _app.AddAgreementAsync();
        var otherSecret = "whsec_" + Convert.ToBase64String("some-other-secret"u8.ToArray());

        var response = await PostAsync(Event("envelope.completed", agreement.EnvelopeId), otherSecret);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AgreementStatus.Sent, (await _app.FindAgreementAsync(agreement.Id))!.Status);
    }

    [Fact]
    public async Task UnsignedRequestIsRejected()
    {
        var response = await _app.CreateClient().PostAsync("/webhooks/signatureapi",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownEventTypeIsIgnored()
    {
        var agreement = await _app.AddAgreementAsync();

        var response = await PostAsync(Event("recipient.completed", agreement.EnvelopeId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AgreementStatus.Sent, (await _app.FindAgreementAsync(agreement.Id))!.Status);
    }

    [Fact]
    public async Task UnknownEnvelopeIsIgnored()
    {
        var response = await PostAsync(Event("envelope.completed", Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TerminalStatusIsNotOverwritten()
    {
        var agreement = await _app.AddAgreementAsync(AgreementStatus.Completed);

        var response = await PostAsync(Event("envelope.canceled", agreement.EnvelopeId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AgreementStatus.Completed, (await _app.FindAgreementAsync(agreement.Id))!.Status);
    }

    private static string Event(string type, string envelopeId) => $$"""
        {"id":"evt_test","type":"{{type}}","timestamp":"2026-10-09T12:00:00.000Z",
         "data":{"envelope_id":"{{envelopeId}}","object_id":"{{envelopeId}}","object_type":"envelope","envelope_metadata": {} } }
        """;

    private Task<HttpResponseMessage> PostAsync(string payload, string? secret = null)
    {
        const string id = "msg_test";
        var timestamp = DateTimeOffset.UtcNow;
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/signatureapi")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("webhook-id", id);
        request.Headers.Add("webhook-timestamp", timestamp.ToUnixTimeSeconds().ToString());
        request.Headers.Add("webhook-signature", new Webhook(secret ?? AppFactory.WebhookSecret).Sign(id, timestamp, payload));
        return _app.CreateClient().SendAsync(request);
    }
}
