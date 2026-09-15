using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TestShieldAI.Api.Ai;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class AiProviderCompletionClientTests
{
    private const string SecretKey = "sk-test-do-not-commit-or-log";

    [Fact]
    public void Factory_AzureOpenAI_SelectsAzureAdapter()
    {
        var options = Enabled(AiProvider.AzureOpenAI);

        var client = AiCompletionClientFactory.Create(options, new StaticApiKey(SecretKey), loggerFactory: null);

        Assert.False(AiCompletionClientFactory.UseNoOp(options));
        Assert.IsType<AzureOpenAiCompletionClient>(client);
    }

    [Fact]
    public void Factory_OpenAI_SelectsOpenAiAdapter()
    {
        var options = Enabled(AiProvider.OpenAI);

        var client = AiCompletionClientFactory.Create(options, new StaticApiKey(SecretKey), loggerFactory: null);

        Assert.IsType<OpenAiCompletionClient>(client);
    }

    [Fact]
    public void Factory_UnknownProvider_FallsBackToNoOp()
    {
        var logger = new CollectingLogger();
        var factory = new CollectingLoggerFactory(logger);
        var options = new AiOptions
        {
            Enabled = true,
            Provider = (AiProvider)42,
            Endpoint = "https://example.openai.azure.com/",
            ModelOrDeployment = "gpt-4o-mini"
        };

        var client = AiCompletionClientFactory.Create(options, new StaticApiKey(SecretKey), factory);

        Assert.IsType<NoOpAiCompletionClient>(client);
        Assert.Contains(logger.Messages, message => message.Contains("Unknown AI provider", StringComparison.Ordinal));
        Assert.DoesNotContain(SecretKey, string.Join('\n', logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_Disabled_DoesNotSelectProviderAdapter()
    {
        var options = new AiOptions
        {
            Enabled = false,
            Provider = AiProvider.AzureOpenAI,
            Endpoint = "https://example.openai.azure.com/",
            ModelOrDeployment = "gpt-4o-mini"
        };

        var client = AiCompletionClientFactory.Create(options, new StaticApiKey(SecretKey), loggerFactory: null);

        Assert.True(AiCompletionClientFactory.UseNoOp(options));
        Assert.IsType<NoOpAiCompletionClient>(client);
    }

    [Fact]
    public async Task AzureClient_MissingApiKey_ThrowsWithoutExposingSecrets()
    {
        var logger = new CollectingLogger<AzureOpenAiCompletionClient>();
        var client = new AzureOpenAiCompletionClient(
            Enabled(AiProvider.AzureOpenAI),
            new StaticApiKey(null),
            logger);

        var ex = await Assert.ThrowsAsync<AiCompletionException>(
            () => client.CompleteJsonAsync("prompt"));

        Assert.Contains("API key is not configured", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretKey, ex.ToString(), StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(SecretKey, string.Join('\n', logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiClient_MissingApiKey_ThrowsWithoutExposingSecrets()
    {
        var client = new OpenAiCompletionClient(
            Enabled(AiProvider.OpenAI),
            new StaticApiKey(""));

        var ex = await Assert.ThrowsAsync<AiCompletionException>(
            () => client.CompleteJsonAsync("prompt"));

        Assert.Contains("API key is not configured", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("OPENAI_API_KEY", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretKey, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AzureClient_InvalidEndpoint_ThrowsWithoutEchoingConfiguration()
    {
        var client = new AzureOpenAiCompletionClient(
            new AiOptions
            {
                Enabled = true,
                Provider = AiProvider.AzureOpenAI,
                Endpoint = "not-a-uri",
                ModelOrDeployment = "gpt-4o-mini"
            },
            new StaticApiKey(SecretKey));

        var ex = await Assert.ThrowsAsync<AiCompletionException>(
            () => client.CompleteJsonAsync("prompt"));

        Assert.Contains("endpoint is missing or invalid", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretKey, ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-uri", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderClient_TransportException_DoesNotExposeSecretOrCreateScenarios()
    {
        var logger = new CollectingLogger<AzureOpenAiCompletionClient>();
        var transport = new FakeTransport
        {
            Exception = new InvalidOperationException($"provider rejected {SecretKey}")
        };
        var client = new AzureOpenAiCompletionClient(
            Enabled(AiProvider.AzureOpenAI),
            new StaticApiKey(SecretKey),
            logger,
            transport);

        var ex = await Assert.ThrowsAsync<AiCompletionException>(
            () => client.CompleteJsonAsync($"prompt containing {SecretKey}"));

        Assert.Equal("AzureOpenAI completion failed.", ex.Message);
        Assert.DoesNotContain(SecretKey, ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SecretKey, string.Join('\n', logger.Messages), StringComparison.Ordinal);
        Assert.DoesNotContain("prompt containing", string.Join('\n', logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderClient_ReturnsCompletionTextWithoutParsingScenarios()
    {
        var transport = new FakeTransport { Response = """[{"type":"Positive"}]""" };
        var client = new OpenAiCompletionClient(
            Enabled(AiProvider.OpenAI),
            new StaticApiKey(SecretKey),
            transport: transport);

        var json = await client.CompleteJsonAsync("return scenarios");

        Assert.Equal("""[{"type":"Positive"}]""", json);
        Assert.Equal("return scenarios", transport.LastPrompt);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task ProviderClient_Timeout_ThrowsProviderExceptionNotCancellation()
    {
        var transport = new FakeTransport { Delay = TimeSpan.FromSeconds(30) };
        var client = new AzureOpenAiCompletionClient(
            new AiOptions
            {
                Enabled = true,
                Provider = AiProvider.AzureOpenAI,
                Endpoint = "https://example.openai.azure.com/",
                ModelOrDeployment = "gpt-4o-mini",
                TimeoutSeconds = 1
            },
            new StaticApiKey(SecretKey),
            transport: transport);

        var ex = await Assert.ThrowsAsync<AiCompletionException>(
            () => client.CompleteJsonAsync("prompt"));

        Assert.Contains("timed out", ex.Message, StringComparison.Ordinal);
        Assert.IsNotType<OperationCanceledException>(ex);
    }

    [Fact]
    public async Task ProviderClient_Cancellation_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var transport = new FakeTransport { Delay = TimeSpan.FromSeconds(30) };
        var client = new OpenAiCompletionClient(
            Enabled(AiProvider.OpenAI),
            new StaticApiKey(SecretKey),
            transport: transport);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteJsonAsync("prompt", cts.Token));
    }

    [Fact]
    public void ApiKeyAccessor_ReadsEnvironmentThenUserSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "from-user-secrets"
            })
            .Build();
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ConfigurationAiApiKeyAccessor.SharedEnvironmentVariable] = "from-shared-env"
        };
        var accessor = new ConfigurationAiApiKeyAccessor(
            configuration,
            name => env.GetValueOrDefault(name));

        Assert.Equal("from-shared-env", accessor.GetApiKey(AiProvider.AzureOpenAI));
    }

    [Fact]
    public void ApiKeyAccessor_ReadsProviderSpecificEnvironmentAndUserSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "from-user-secrets"
            })
            .Build();
        var azureEnv = new ConfigurationAiApiKeyAccessor(
            configuration,
            name => name == ConfigurationAiApiKeyAccessor.AzureEnvironmentVariable ? "from-azure-env" : null);
        var secretsOnly = new ConfigurationAiApiKeyAccessor(
            configuration,
            _ => null);

        Assert.Equal("from-azure-env", azureEnv.GetApiKey(AiProvider.AzureOpenAI));
        Assert.Equal("from-user-secrets", secretsOnly.GetApiKey(AiProvider.OpenAI));
    }

    [Fact]
    public void ApiKeyAccessor_MissingKey_ReturnsNull()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var accessor = new ConfigurationAiApiKeyAccessor(configuration, _ => null);

        Assert.Null(accessor.GetApiKey(AiProvider.OpenAI));
    }

    private static AiOptions Enabled(AiProvider provider) => new()
    {
        Enabled = true,
        Provider = provider,
        Endpoint = provider == AiProvider.AzureOpenAI ? "https://example.openai.azure.com/" : "",
        ModelOrDeployment = provider == AiProvider.AzureOpenAI ? "gpt-4o-mini" : "gpt-4o-mini",
        TimeoutSeconds = 30
    };

    private sealed class StaticApiKey : IAiApiKeyAccessor
    {
        private readonly string? _key;

        public StaticApiKey(string? key) => _key = key;

        public string? GetApiKey(AiProvider provider) => _key;
    }

    private sealed class FakeTransport : IAiChatCompletionTransport
    {
        public string Response { get; init; } = "[]";

        public Exception? Exception { get; init; }

        public TimeSpan Delay { get; init; }

        public int Calls { get; private set; }

        public string? LastPrompt { get; private set; }

        public async Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken)
        {
            Calls++;
            LastPrompt = prompt;
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (Exception is not null)
            {
                throw Exception;
            }

            return Response;
        }
    }

    private sealed class CollectingLoggerFactory : ILoggerFactory
    {
        private readonly ILogger _logger;

        public CollectingLoggerFactory(ILogger logger) => _logger = logger;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => _logger;

        public void Dispose()
        {
        }
    }

    private class CollectingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
            {
                Messages.Add(exception.ToString());
            }
        }
    }

    private sealed class CollectingLogger<T> : CollectingLogger, ILogger<T>
    {
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
