using Microsoft.EntityFrameworkCore;

namespace SignatureApiStarter.Data;

[Index(nameof(EnvelopeId), IsUnique = true)]
public class Agreement
{
    // Generated before the envelope is created so it can go in the envelope metadata.
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SignerName { get; set; }
    public required string SignerEmail { get; set; }
    public required string EnvelopeId { get; set; }
    public string Status { get; set; } = AgreementStatus.Sent;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class AgreementStatus
{
    public const string Sent = "sent";
    public const string Completed = "completed";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
    public const string Canceled = "canceled";

    public static bool IsTerminal(string status) => status != Sent;
}
