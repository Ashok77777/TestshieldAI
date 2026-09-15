using TestShieldAI.Ci;

namespace TestShieldAI.Engine.Tests;

public class CiAzureDevOpsAdapterTests
{
    [Fact]
    public void Script_UsesParametersAndEnvironmentVariables_NotHardCodedTargets()
    {
        var script = File.ReadAllText(AdapterPath("testshield-run.ps1"));

        Assert.Contains("-BaseUrl", script, StringComparison.Ordinal);
        Assert.Contains("-ProjectId", script, StringComparison.Ordinal);
        Assert.Contains("TESTSHIELD_BASE_URL", script, StringComparison.Ordinal);
        Assert.Contains("TESTSHIELD_PROJECT_ID", script, StringComparison.Ordinal);
        Assert.Contains("TESTSHIELD_TOKEN", script, StringComparison.Ordinal);
        Assert.Contains("/api/projects/{1}/tests/run", script, StringComparison.Ordinal);
        Assert.DoesNotContain("https://dev.azure.com", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openai.azure.com", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_TokenIsOptionalAndNeverPrinted()
    {
        var script = File.ReadAllText(AdapterPath("testshield-run.ps1"));

        Assert.Contains("Authorization", script, StringComparison.Ordinal);
        Assert.Contains("Bearer", script, StringComparison.Ordinal);
        Assert.Contains("Protect-Secret", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $Token", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Output $Token", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $headers", script, StringComparison.Ordinal);
        Assert.Contains("if (-not [string]::IsNullOrWhiteSpace($Token))", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_MapsReviewToSuccessAndBlockToFailure()
    {
        var script = File.ReadAllText(AdapterPath("testshield-run.ps1"));

        Assert.Contains("'^(?i)safe$' { return 0 }", script, StringComparison.Ordinal);
        Assert.Contains("'^(?i)review$' { return 0 }", script, StringComparison.Ordinal);
        Assert.Contains("default { return 1 }", script, StringComparison.Ordinal);
        Assert.Contains("exit (Get-ExitCode $statusCode $decision)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Yaml_DoesNotCommitSecretsAndUsesVariables()
    {
        var yaml = File.ReadAllText(AdapterPath("testshield-regression.yml"));

        Assert.Contains("testShieldBaseUrl", yaml, StringComparison.Ordinal);
        Assert.Contains("testShieldProjectId", yaml, StringComparison.Ordinal);
        Assert.Contains("TESTSHIELD_TOKEN: $(TESTSHIELD_TOKEN)", yaml, StringComparison.Ordinal);
        Assert.Contains("checkout: self", yaml, StringComparison.Ordinal);
        Assert.Contains("testshield-run.ps1", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("-Token", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer ey", yaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecretRedactor_NeverLeavesTheTokenInOutput()
    {
        const string token = "super-secret-token";
        var redacted = CiSecretRedactor.Redact($"Authorization: Bearer {token} failed", token);

        Assert.DoesNotContain(token, redacted, StringComparison.Ordinal);
        Assert.Contains("***", redacted, StringComparison.Ordinal);
        Assert.Equal("plain", CiSecretRedactor.Redact("plain", null));
    }

    private static string AdapterPath(string fileName)
    {
        var fromOutput = Path.Combine(AppContext.BaseDirectory, "ci", "azure-devops", fileName);
        if (File.Exists(fromOutput))
        {
            return fromOutput;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ci", "azure-devops", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Azure DevOps adapter file '{fileName}' was not found.");
    }
}
