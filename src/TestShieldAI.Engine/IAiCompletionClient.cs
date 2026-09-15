namespace TestShieldAI.Engine;

public interface IAiCompletionClient
{
    Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken = default);
}
