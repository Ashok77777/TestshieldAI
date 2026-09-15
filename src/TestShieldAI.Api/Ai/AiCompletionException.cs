namespace TestShieldAI.Api.Ai;

public sealed class AiCompletionException : Exception
{
    public AiCompletionException(string message)
        : base(message)
    {
    }

    public AiCompletionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
