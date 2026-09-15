namespace TestShieldAI.Engine;

public sealed class RegressionTestSnapshot
{
    public RegressionTestSnapshot(
        GeneratedApiTestKind kind,
        string sourceMethod,
        string sourcePath,
        string method,
        string pathTemplate,
        int expectedStatus,
        ImportedSchema? expectedResponseSchema,
        ContractValidationOutcome outcome,
        string? scenarioKey = null,
        string? specKey = null)
    {
        Kind = kind;
        SourceMethod = sourceMethod;
        SourcePath = sourcePath;
        Method = method;
        PathTemplate = pathTemplate;
        ExpectedStatus = expectedStatus;
        ExpectedResponseSchema = expectedResponseSchema;
        Outcome = outcome;
        ScenarioKey = scenarioKey;
        SpecKey = specKey;
    }

    public GeneratedApiTestKind Kind { get; }

    public string SourceMethod { get; }

    public string SourcePath { get; }

    public string Method { get; }

    public string PathTemplate { get; }

    public int ExpectedStatus { get; }

    public ImportedSchema? ExpectedResponseSchema { get; }

    public ContractValidationOutcome Outcome { get; }

    public string? ScenarioKey { get; }

    public string? SpecKey { get; }
}
