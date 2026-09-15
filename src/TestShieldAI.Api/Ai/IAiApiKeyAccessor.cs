namespace TestShieldAI.Api.Ai;

public interface IAiApiKeyAccessor
{
    string? GetApiKey(AiProvider provider);
}
