using Microsoft.Extensions.Configuration;

namespace TestShieldAI.Api.Ai;

public sealed class ConfigurationAiApiKeyAccessor : IAiApiKeyAccessor
{
    public const string SharedEnvironmentVariable = "AI_PROVIDER_API_KEY";
    public const string AzureEnvironmentVariable = "AZURE_OPENAI_API_KEY";
    public const string OpenAiEnvironmentVariable = "OPENAI_API_KEY";
    public const string UserSecretsConfigurationKey = "Ai:ApiKey";

    private readonly IConfiguration _configuration;
    private readonly Func<string, string?> _environment;

    public ConfigurationAiApiKeyAccessor(IConfiguration configuration)
        : this(configuration, Environment.GetEnvironmentVariable)
    {
    }

    public ConfigurationAiApiKeyAccessor(
        IConfiguration configuration,
        Func<string, string?> environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public string? GetApiKey(AiProvider provider)
    {
        return FirstNonEmpty(
            _environment(SharedEnvironmentVariable),
            _environment(ProviderEnvironmentVariable(provider)),
            _configuration[SharedEnvironmentVariable],
            _configuration[ProviderEnvironmentVariable(provider)],
            _configuration[UserSecretsConfigurationKey]);
    }

    private static string ProviderEnvironmentVariable(AiProvider provider) =>
        provider == AiProvider.AzureOpenAI ? AzureEnvironmentVariable : OpenAiEnvironmentVariable;

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
