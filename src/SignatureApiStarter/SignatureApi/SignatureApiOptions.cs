namespace SignatureApiStarter.SignatureApi;

// Read from SIGNATUREAPI_KEY and SIGNATUREAPI_WEBHOOK_SECRET (environment or .env).
public class SignatureApiOptions
{
    public string ApiKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
}
