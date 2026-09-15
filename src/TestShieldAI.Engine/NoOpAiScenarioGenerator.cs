namespace TestShieldAI.Engine;

public sealed class NoOpAiScenarioGenerator : IAiScenarioGenerator
{
    public const string NotConfiguredWarning = "AI scenario generation is not configured.";

    public Task<AiScenarioGenerationResult> GenerateAsync(
        IReadOnlyList<ImportedOperation> operations,
        IReadOnlyList<GeneratedApiTestCase> existingTests,
        CancellationToken cancellationToken = default)
    {
        _ = operations;
        _ = existingTests;
        _ = cancellationToken;
        return Task.FromResult(new AiScenarioGenerationResult(
            [],
            [NotConfiguredWarning],
            succeeded: false));
    }
}
