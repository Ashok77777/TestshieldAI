using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Ai;

public static class AiCompletionClientFactory
{
    public static IAiCompletionClient Create(AiOptions? options) =>
        Create(options, apiKeys: null, loggerFactory: null);

    public static IAiCompletionClient Create(
        AiOptions? options,
        IAiApiKeyAccessor? apiKeys,
        ILoggerFactory? loggerFactory)
    {
        if (UseNoOp(options))
        {
            return new NoOpAiCompletionClient();
        }

        var logger = loggerFactory?.CreateLogger(typeof(AiCompletionClientFactory).FullName!)
            ?? NullLogger.Instance;

        if (!Enum.IsDefined(options!.Provider))
        {
            logger.LogWarning(
                "Unknown AI provider {Provider}; using NoOp completion client.",
                (int)options.Provider);
            return new NoOpAiCompletionClient();
        }

        var keys = apiKeys ?? new MissingAiApiKeyAccessor();
        return options.Provider switch
        {
            AiProvider.AzureOpenAI => new AzureOpenAiCompletionClient(
                options,
                keys,
                loggerFactory?.CreateLogger<AzureOpenAiCompletionClient>()),
            AiProvider.OpenAI => new OpenAiCompletionClient(
                options,
                keys,
                loggerFactory?.CreateLogger<OpenAiCompletionClient>()),
            _ => UnknownProvider(logger, options.Provider)
        };
    }

    public static bool UseNoOp(AiOptions? options) =>
        options is null ||
        !options.Enabled ||
        options.Provider is AiProvider.None;

    private static IAiCompletionClient UnknownProvider(ILogger logger, AiProvider provider)
    {
        logger.LogWarning(
            "Unknown AI provider {Provider}; using NoOp completion client.",
            provider);
        return new NoOpAiCompletionClient();
    }

    private sealed class MissingAiApiKeyAccessor : IAiApiKeyAccessor
    {
        public string? GetApiKey(AiProvider provider) => null;
    }
}
