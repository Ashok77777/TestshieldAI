using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TestShieldAI.Api.Ai;

internal static class ProviderAiCompletion
{
    public static int NormalizeTimeoutSeconds(int timeoutSeconds) =>
        timeoutSeconds > 0 ? timeoutSeconds : 30;

    public static async Task<string> CompleteJsonAsync(
        string provider,
        string model,
        int timeoutSeconds,
        string prompt,
        IAiChatCompletionTransport transport,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var timeout = TimeSpan.FromSeconds(NormalizeTimeoutSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        var started = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "AI completion started. Provider={Provider} Model={Model}",
            provider,
            model);

        try
        {
            var completion = await transport.CompleteJsonAsync(prompt, linked.Token).ConfigureAwait(false);
            logger.LogInformation(
                "AI completion finished. Provider={Provider} Model={Model} Status={Status} DurationMs={DurationMs}",
                provider,
                model,
                "Succeeded",
                ElapsedMs(started));
            return completion;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "AI completion finished. Provider={Provider} Model={Model} Status={Status} DurationMs={DurationMs}",
                provider,
                model,
                "Canceled",
                ElapsedMs(started));
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "AI completion finished. Provider={Provider} Model={Model} Status={Status} DurationMs={DurationMs}",
                provider,
                model,
                "TimedOut",
                ElapsedMs(started));
            throw new AiCompletionException($"{provider} request timed out.");
        }
        catch (ClientResultException ex)
        {
            logger.LogWarning(
                "AI completion failed. Provider={Provider} Model={Model} Status={Status} DurationMs={DurationMs} ErrorType={ErrorType}",
                provider,
                model,
                ex.Status,
                ElapsedMs(started),
                ex.GetType().Name);
            throw new AiCompletionException(HttpFailureMessage(provider, ex.Status));
        }
        catch (AiCompletionException)
        {
            logger.LogWarning(
                "AI completion failed. Provider={Provider} Model={Model} Status={Status} DurationMs={DurationMs}",
                provider,
                model,
                "Failed",
                ElapsedMs(started));
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "AI completion failed. Provider={Provider} Model={Model} Status={Status} DurationMs={DurationMs} ErrorType={ErrorType}",
                provider,
                model,
                "Failed",
                ElapsedMs(started),
                ex.GetType().Name);
            throw new AiCompletionException($"{provider} completion failed.");
        }
    }

    private static string HttpFailureMessage(string provider, int status) =>
        status switch
        {
            401 or 403 => $"{provider} authentication failed.",
            404 => $"{provider} model or deployment is unavailable.",
            _ => $"{provider} HTTP request failed."
        };

    private static long ElapsedMs(long started) =>
        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
