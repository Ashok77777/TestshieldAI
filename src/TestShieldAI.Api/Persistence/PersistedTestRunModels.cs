namespace TestShieldAI.Api.Persistence;

public sealed class PersistedTestRunCase
{
    public required string Kind { get; init; }

    public required string SourceMethod { get; init; }

    public required string SourcePath { get; init; }

    public string? SpecKey { get; init; }

    public required int ExpectedStatus { get; init; }

    public int? ActualStatus { get; init; }

    public required string Outcome { get; init; }

    public string? ScenarioKey { get; init; }

    public required IReadOnlyList<PersistedValidationFailure> Failures { get; init; }
}

public sealed class PersistedValidationFailure
{
    public required string Code { get; init; }

    public required string JsonPath { get; init; }

    public required string Message { get; init; }
}

public sealed class PersistedRegressionFinding
{
    public required string Category { get; init; }

    public required string Code { get; init; }

    public required string Severity { get; init; }

    public string? Kind { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string Expected { get; init; }

    public required string Current { get; init; }

    public string? JsonPath { get; init; }

    public required string Message { get; init; }
}

public sealed class PersistedTestRunResults
{
    public required IReadOnlyList<PersistedTestRunCase> Tests { get; init; }

    public required IReadOnlyList<PersistedRegressionFinding> Findings { get; init; }
}

public sealed class ProjectTestRunSave
{
    public required string Decision { get; init; }

    public required int PassedCount { get; init; }

    public required int FailedCount { get; init; }

    public required int ErrorCount { get; init; }

    public required int FindingCount { get; init; }

    public required PersistedTestRunResults Results { get; init; }
}
