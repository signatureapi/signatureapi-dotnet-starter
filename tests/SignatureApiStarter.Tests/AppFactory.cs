using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignatureApiStarter.Data;
using SignatureApiStarter.SignatureApi;

namespace SignatureApiStarter.Tests;

/// <summary>Runs the app against a temporary SQLite file and a fake SignatureAPI.</summary>
public class AppFactory(string apiKey = "key_test_unit") : WebApplicationFactory<Program>
{
    public static readonly string WebhookSecret =
        "whsec_" + Convert.ToBase64String("test-secret-for-unit-tests-only"u8.ToArray());

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"signatureapi-starter-{Guid.NewGuid()}.db");

    public FakeSignatureApi SignatureApi { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Added last, so these win over a real .env or environment.
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SIGNATUREAPI_KEY"] = apiKey,
            ["SIGNATUREAPI_WEBHOOK_SECRET"] = WebhookSecret,
            ["ConnectionStrings:Default"] = $"Data Source={_dbPath};Pooling=False",
        }));
        builder.ConfigureTestServices(services =>
            services.AddHttpClient<SignatureApiClient>().ConfigurePrimaryHttpMessageHandler(() => SignatureApi));
    }

    public HttpClient CreateNoRedirectClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task<Agreement> AddAgreementAsync(string status = AgreementStatus.Sent)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agreement = new Agreement
        {
            SignerName = "Jane Doe",
            SignerEmail = "jane@example.com",
            EnvelopeId = Guid.NewGuid().ToString(),
            Status = status,
        };
        db.Agreements.Add(agreement);
        await db.SaveChangesAsync();
        return agreement;
    }

    public async Task<Agreement?> FindAgreementAsync(Guid id)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Agreements.FindAsync(id);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_dbPath);
    }
}

/// <summary>Stands in for api.signatureapi.com. Records requests and answers from <see cref="Respond"/>.</summary>
public class FakeSignatureApi : HttpMessageHandler
{
    public List<(HttpMethod Method, string Path, string? ContentType, byte[] Body)> Requests { get; } = [];

    public Func<HttpMethod, string, HttpResponseMessage> Respond { get; set; } =
        (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.Equal("key_test_unit", request.Headers.GetValues("X-API-Key").Single());
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(ct);
        var path = request.RequestUri!.AbsolutePath;
        Requests.Add((request.Method, path, request.Content?.Headers.ContentType?.MediaType, body));
        return Respond(request.Method, path);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, mediaType) };
}
