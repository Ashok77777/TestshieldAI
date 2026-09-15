namespace TestShieldAI.Engine;

public interface ICoverageCalculator
{
    CoverageCalculationResult Calculate(
        IReadOnlyList<ImportedOperation> operations,
        IReadOnlyList<GeneratedApiTestCase> generatedTests);
}
