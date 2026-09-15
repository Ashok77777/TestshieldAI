namespace TestShieldAI.Api.Contracts;

public sealed class GenerateTestsResponse
{
    public required IReadOnlyList<GeneratedApiTestCaseDto> Tests { get; init; }

    public required CoverageDto Coverage { get; init; }

    public required RiskSummaryDto Risk { get; init; }
}

public sealed class GeneratedApiTestCaseDto
{
    public required string Kind { get; init; }

    public required string Method { get; init; }

    public required string PathTemplate { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<GeneratedApiParameterDto> Parameters { get; init; }

    public string? RequestBody { get; init; }

    public required int ExpectedStatus { get; init; }

    public ImportedSchemaDto? ExpectedResponseSchema { get; init; }

    public required string SourceMethod { get; init; }

    public required string SourcePath { get; init; }

    public string? SpecKey { get; init; }
}

public sealed class GeneratedApiParameterDto
{
    public required string Name { get; init; }

    public required string Location { get; init; }

    public required bool Required { get; init; }

    public required string Placeholder { get; init; }
}
