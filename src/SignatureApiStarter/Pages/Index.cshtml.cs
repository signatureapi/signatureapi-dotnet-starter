using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SignatureApiStarter.Data;
using SignatureApiStarter.SignatureApi;

namespace SignatureApiStarter.Pages;

// Served at "/" and, for the form post, at "/agreements" (see Program.cs).
public class IndexModel(AppDbContext db, SignatureApiClient signatureApi, IWebHostEnvironment env) : PageModel
{
    [BindProperty(Name = "signer_name"), Required]
    public string SignerName { get; set; } = "";

    [BindProperty(Name = "signer_email"), Required, EmailAddress]
    public string SignerEmail { get; set; } = "";

    public List<Agreement> Agreements { get; private set; } = [];

    public string? Error { get; private set; }

    public async Task OnGetAsync() => await LoadAgreementsAsync();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await ShowErrorAsync("enter the signer's name and a valid email.");
        }

        var agreement = new Agreement { SignerName = SignerName, SignerEmail = SignerEmail, EnvelopeId = "" };
        try
        {
            var pdf = await System.IO.File.ReadAllBytesAsync(Path.Combine(env.ContentRootPath, "sample-agreement.pdf"), ct);
            var documentUrl = await signatureApi.UploadDocumentAsync(pdf, ct);
            agreement.EnvelopeId = await signatureApi.CreateEnvelopeAsync(
                documentUrl, SignerName, SignerEmail, agreement.Id.ToString(), ct);
        }
        catch (Exception ex) when (ex is SignatureApiException or HttpRequestException)
        {
            return await ShowErrorAsync(ex.Message);
        }

        db.Agreements.Add(agreement);
        await db.SaveChangesAsync(ct);
        return Redirect("/");
    }

    private async Task<IActionResult> ShowErrorAsync(string message)
    {
        Error = message;
        await LoadAgreementsAsync();
        return Page();
    }

    private async Task LoadAgreementsAsync() =>
        Agreements = await db.Agreements.OrderByDescending(a => a.CreatedAt).ToListAsync();
}
