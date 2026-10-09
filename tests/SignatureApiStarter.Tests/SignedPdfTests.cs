using System.Net;
using SignatureApiStarter.Data;

namespace SignatureApiStarter.Tests;

public class SignedPdfTests : IDisposable
{
    private readonly AppFactory _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task RedirectsToAFreshDeliverableUrl()
    {
        var agreement = await _app.AddAgreementAsync(AgreementStatus.Completed);
        _app.SignatureApi.Respond = (method, path) => path == $"/v1/envelopes/{agreement.EnvelopeId}/deliverables"
            ? FakeSignatureApi.Json(HttpStatusCode.OK,
                """{"links":{},"data":[{"id":"del_1","type":"standard","status":"generated","url":"https://files.example.com/signed.pdf?sig=abc"}]}""")
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        var response = await _app.CreateNoRedirectClient().GetAsync($"/agreements/{agreement.Id}/signed.pdf");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://files.example.com/signed.pdf?sig=abc", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task NotFoundWhileNoDeliverableIsReady()
    {
        var agreement = await _app.AddAgreementAsync(AgreementStatus.Completed);
        _app.SignatureApi.Respond = (_, _) => FakeSignatureApi.Json(HttpStatusCode.OK,
            """{"links":{},"data":[{"id":"del_1","type":"standard","status":"processing","url":null}]}""");

        var response = await _app.CreateNoRedirectClient().GetAsync($"/agreements/{agreement.Id}/signed.pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
