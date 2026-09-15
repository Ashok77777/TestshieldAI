namespace TestShieldAI.Engine;

public sealed class AiScenarioNormalizationResult
{
    public AiScenarioNormalizationResult(
        IReadOnlyList<GeneratedApiTestCase> tests,
        IReadOnlyList<string> warnings)
    {
        Tests = tests;
        Warnings = warnings;
    }

    public IReadOnlyList<GeneratedApiTestCase> Tests { get; }

    public IReadOnlyList<string> Warnings { get; }
}
