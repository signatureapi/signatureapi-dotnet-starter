using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignatureApiStarter.Data;
using StandardWebhooks;
using StandardWebhooks.Exceptions;

namespace SignatureApiStarter.SignatureApi;

/// <summary>Receives SignatureAPI webhook events and updates the matching agreement.</summary>
public static class WebhookEndpoint
{
    // Event type → agreement status. Add a line here to react to another event.
    private static readonly Dictionary<string, string> StatusByEventType = new()
    {
        ["envelope.completed"] = AgreementStatus.Completed,
        ["recipient.rejected"] = AgreementStatus.Rejected,
        ["envelope.failed"] = AgreementStatus.Failed,
        ["envelope.canceled"] = AgreementStatus.Canceled,
    };

    public static void MapSignatureApiWebhook(this IEndpointRouteBuilder app) =>
        app.MapPost("/webhooks/signatureapi", HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpRequest request, AppDbContext db, IOptions<SignatureApiOptions> options, ILoggerFactory loggers)
    {
        var logger = loggers.CreateLogger(typeof(WebhookEndpoint));
        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogError("SIGNATUREAPI_WEBHOOK_SECRET is not set. Run `npx --yes signatureapi listen` to create it.");
            return Results.Unauthorized();
        }

        // The signature covers the exact body bytes, so read the raw body
        // instead of binding a model, and verify it before parsing.
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync();

        var headers = new WebHeaderCollection();
        foreach (var name in new[] { "webhook-id", "webhook-timestamp", "webhook-signature" })
        {
            headers.Add(name, request.Headers[name].ToString());
        }

        try
        {
            new Webhook(secret).Verify(payload, headers);
        }
        catch (WebhookVerificationException ex)
        {
            logger.LogWarning("Rejected SignatureAPI webhook: {Reason}", ex.Message);
            return Results.Unauthorized();
        }

        // Events carry ids only: {id, type, timestamp, data: {envelope_id, object_id, object_type, ...}}.
        var evt = JsonSerializer.Deserialize<WebhookEvent>(payload, SignatureApiClient.Json);
        if (evt?.Data?.EnvelopeId is not { } envelopeId || evt.Type is null || !StatusByEventType.TryGetValue(evt.Type, out var status))
        {
            return Results.Ok();
        }

        var agreement = await db.Agreements.SingleOrDefaultAsync(a => a.EnvelopeId == envelopeId);
        // Unknown envelopes are not ours to handle. Events can arrive late or
        // out of order, so a final status never changes.
        if (agreement is null || AgreementStatus.IsTerminal(agreement.Status))
        {
            return Results.Ok();
        }

        agreement.Status = status;
        agreement.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private record WebhookEvent(string? Id, string? Type, WebhookEventData? Data);

    private record WebhookEventData(string? EnvelopeId, string? ObjectId, string? ObjectType);
}
