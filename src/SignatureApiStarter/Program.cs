using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignatureApiStarter.Data;
using SignatureApiStarter.SignatureApi;

// Load .env (written by `npx --yes signatureapi init` and `listen`) into the
// environment. Variables already set in the environment win.
DotNetEnv.Env.TraversePath().NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options => options.Conventions.AddPageRoute("/Index", "agreements"));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddOptions<SignatureApiOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        options.ApiKey = config["SIGNATUREAPI_KEY"] ?? "";
        options.WebhookSecret = config["SIGNATUREAPI_WEBHOOK_SECRET"] ?? "";
    })
    .Validate(options => options.ApiKey != "",
        "SIGNATUREAPI_KEY is not set. Run `npx --yes signatureapi init` to write it to .env.")
    .ValidateOnStart();

builder.Services.AddHttpClient<SignatureApiClient>((services, client) =>
{
    client.BaseAddress = SignatureApiClient.BaseAddress;
    client.DefaultRequestHeaders.Add("X-API-Key", services.GetRequiredService<IOptions<SignatureApiOptions>>().Value.ApiKey);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapSignatureApiWebhook();

// Deliverable URLs expire after an hour, so ask for a fresh one on every
// download instead of storing it.
app.MapGet("/agreements/{id:guid}/signed.pdf", async (Guid id, AppDbContext db, SignatureApiClient signatureApi) =>
{
    var agreement = await db.Agreements.FindAsync(id);
    if (agreement is null)
    {
        return Results.NotFound();
    }

    var deliverables = agreement.Status == AgreementStatus.Completed
        ? await signatureApi.ListDeliverablesAsync(agreement.EnvelopeId)
        : [];
    var url = deliverables.FirstOrDefault(d => !string.IsNullOrEmpty(d.Url))?.Url;
    return url is null
        ? Results.Text("The signed PDF is not ready yet. Try again in a few seconds.", statusCode: StatusCodes.Status404NotFound)
        : Results.Redirect(url);
});

app.Run();

public partial class Program;
