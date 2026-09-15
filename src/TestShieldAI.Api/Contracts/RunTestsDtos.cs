namespace TestShieldAI.Api.Contracts;

public sealed class RunTestsResponse
{
    public required string BaseUrl { get; init; }

    public required IReadOnlyList<ApiTestExecutionResultDto> Results { get; init; }

    public required string Decision { get; init; }

    public DateTimeOffset? BaselineEstablishedAt { get; init; }

    public required IReadOnlyList<RegressionFindingDto> Findings { get; init; }

    public required AiRunSummaryDto Ai { get; init; }

    public required CoverageDto Coverage { get; init; }

    public required RiskSummaryDto Risk { get; init; }
}

public sealed class ApiTestExecutionResultDto
{
    public required string Kind { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string SourceMethod { get; init; }

    public required string SourcePath { get; init; }

    public string? SpecKey { get; init; }

    public required int ExpectedStatus { get; init; }

    public int? ActualStatus { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public string? Body { get; init; }

    public required int DurationMs { get; init; }

    public required bool StatusMatched { get; init; }

    public string? Error { get; init; }

    public required ContractValidationResultDto Validation { get; init; }
}

public sealed class ContractValidationResultDto
{
    public required string Outcome { get; init; }

    public required bool StatusValid { get; init; }

    public bool? SchemaValid { get; init; }

    public required IReadOnlyList<ContractValidationFailureDto> Failures { get; init; }
}

public sealed class ContractValidationFailureDto
{
    public required string Code { get; init; }

    public required string JsonPath { get; init; }

    public required string Message { get; init; }
}

public sealed class RegressionFindingDto
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
