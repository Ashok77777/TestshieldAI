namespace TestShieldAI.Engine;

public interface IAiScenarioGenerator
{
    Task<AiScenarioGenerationResult> GenerateAsync(
        IReadOnlyList<ImportedOperation> operations,
        IReadOnlyList<GeneratedApiTestCase> existingTests,
        CancellationToken cancellationToken = default);
}
