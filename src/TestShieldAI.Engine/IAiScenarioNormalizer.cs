namespace TestShieldAI.Engine;

public interface IAiScenarioNormalizer
{
    AiScenarioNormalizationResult Normalize(
        IReadOnlyList<AiTestScenario> proposals,
        IReadOnlyList<ImportedOperation> operations,
        IReadOnlyList<GeneratedApiTestCase> existingTests);
}
