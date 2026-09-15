using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestShieldAI.Api.Ai;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class AiCompletionClientTests : IClassFixture<SpecsImportWebApplicationFactory>
{
    private readonly SpecsImportWebApplicationFactory _factory;

    public AiCompletionClientTests(SpecsImportWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AiOptions_Defaults_AreDisabledWithNoneProvider()
    {
        var options = new AiOptions();

        Assert.False(options.Enabled);
        Assert.Equal(AiProvider.None, options.Provider);
        Assert.Equal("", options.Endpoint);
        Assert.Equal("", options.ModelOrDeployment);
        Assert.Equal(6, options.MaxScenariosPerOperation);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void AiOptions_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Enabled"] = "true",
                ["Ai:Provider"] = "AzureOpenAI",
                ["Ai:Endpoint"] = "https://example.openai.azure.com/",
                ["Ai:ModelOrDeployment"] = "gpt-4o-mini",
                ["Ai:MaxScenariosPerOperation"] = "4",
                ["Ai:TimeoutSeconds"] = "15"
            })
            .Build();

        var options = new AiOptions();
        configuration.GetSection(AiOptions.SectionName).Bind(options);

        Assert.True(options.Enabled);
        Assert.Equal(AiProvider.AzureOpenAI, options.Provider);
        Assert.Equal("https://example.openai.azure.com/", options.Endpoint);
        Assert.Equal("gpt-4o-mini", options.ModelOrDeployment);
        Assert.Equal(4, options.MaxScenariosPerOperation);
        Assert.Equal(15, options.TimeoutSeconds);
    }

    [Fact]
    public void Factory_ProviderNone_SelectsNoOp()
    {
        var options = new AiOptions { Enabled = true, Provider = AiProvider.None };

        Assert.True(AiCompletionClientFactory.UseNoOp(options));
        Assert.IsType<NoOpAiCompletionClient>(AiCompletionClientFactory.Create(options));
    }

    [Fact]
    public void Factory_AiDisabled_SelectsNoOp()
    {
        var options = new AiOptions { Enabled = false, Provider = AiProvider.AzureOpenAI };

        Assert.True(AiCompletionClientFactory.UseNoOp(options));
        Assert.IsType<NoOpAiCompletionClient>(AiCompletionClientFactory.Create(options));
    }

    [Fact]
    public void Host_RegistersNoOpCompletionClientByDefault()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
        var client = scope.ServiceProvider.GetRequiredService<IAiCompletionClient>();

        Assert.False(options.Enabled);
        Assert.Equal(AiProvider.None, options.Provider);
        Assert.IsType<NoOpAiCompletionClient>(client);
    }

    [Fact]
    public async Task NoOpClient_DoesNotThrowAndReturnsEmptyJson()
    {
        var client = new NoOpAiCompletionClient();

        var json = await client.CompleteJsonAsync("ignored prompt");

        Assert.Equal(NoOpAiCompletionClient.EmptyJsonResponse, json);
        Assert.Equal("[]", json);
    }

    [Fact]
    public async Task NoOpClient_PerformsNoNetworkActivity()
    {
        var client = new NoOpAiCompletionClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var json = await client.CompleteJsonAsync("do not call a provider", timeout.Token);

        Assert.Equal(NoOpAiCompletionClient.EmptyJsonResponse, json);
    }
}
