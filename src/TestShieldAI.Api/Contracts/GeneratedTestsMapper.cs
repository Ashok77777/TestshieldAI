using TestShieldAI.Api.Contracts;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Contracts;

public static class GeneratedTestsMapper
{
    public static GenerateTestsResponse ToResponse(
        IReadOnlyList<GeneratedApiTestCase> tests,
        CoverageCalculationResult coverage,
        RiskSummaryResult risk) =>
        new()
        {
            Tests = tests.Select(ToDto).ToList(),
            Coverage = CoverageMapper.ToDto(coverage),
            Risk = RiskSummaryMapper.ToDto(risk)
        };

    private static GeneratedApiTestCaseDto ToDto(GeneratedApiTestCase test) =>
        new()
        {
            Kind = test.Kind.ToString(),
            Method = test.Method,
            PathTemplate = test.PathTemplate,
            Path = test.Path,
            Parameters = test.Parameters.Select(ToDto).ToList(),
            RequestBody = test.RequestBody,
            ExpectedStatus = test.ExpectedStatus,
            ExpectedResponseSchema = ImportSpecMapper.ToDto(test.ExpectedResponseSchema),
            SourceMethod = test.SourceMethod,
            SourcePath = test.SourcePath,
            SpecKey = test.SpecKey
        };

    private static GeneratedApiParameterDto ToDto(GeneratedApiParameter parameter) =>
        new()
        {
            Name = parameter.Name,
            Location = parameter.Location,
            Required = parameter.Required,
            Placeholder = parameter.Placeholder
        };
}
