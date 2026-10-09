using Microsoft.Extensions.Options;

namespace SignatureApiStarter.Tests;

public class StartupTests
{
    [Fact]
    public void FailsFastWithoutApiKey()
    {
        using var app = new AppFactory(apiKey: "");

        var error = Assert.Throws<OptionsValidationException>(() => app.CreateClient());
        Assert.Contains("SIGNATUREAPI_KEY is not set", error.Message);
    }
}
