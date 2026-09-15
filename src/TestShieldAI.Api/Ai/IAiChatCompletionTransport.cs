namespace TestShieldAI.Api.Ai;

public interface IAiChatCompletionTransport
{
    Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken);
}
