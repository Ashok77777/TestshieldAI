using TestShieldAI.Api.Persistence;
using TestShieldAI.Api.TestsRun;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Contracts;

public static class RunTestsMapper
{
    public static RunTestsResponse ToResponse(
        string baseUrl,
        IReadOnlyList<TestsRunCaseResult> results,
        RegressionDetectionResult detection,
        DateTimeOffset? baselineEstablishedAt,
        AiRunSummaryDto ai,
        CoverageCalculationResult coverage,
        RiskSummaryResult risk) =>
        new()
        {
            BaseUrl = baseUrl,
            Results = results.Select(item => ToDto(item.Execution, item.Validation, item.Test)).ToList(),
            Decision = detection.Decision.ToString(),
            BaselineEstablishedAt = baselineEstablishedAt,
            Findings = detection.Findings.Select(ToDto).ToList(),
            Ai = ai,
            Coverage = CoverageMapper.ToDto(coverage),
            Risk = RiskSummaryMapper.ToDto(risk)
        };

    public static RegressionTestSnapshot ToSnapshot(
        GeneratedApiTestCase test,
        ContractValidationResult validation) =>
        new(
            test.Kind,
            test.SourceMethod,
            test.SourcePath,
            test.Method,
            test.PathTemplate,
            test.ExpectedStatus,
            test.ExpectedResponseSchema,
            validation.Outcome,
            test.ScenarioKey,
            test.SpecKey);

    public static ProjectTestRunSave ToLastRunSave(
        IReadOnlyList<TestsRunCaseResult> results,
        RegressionDetectionResult detection)
    {
        var outcomes = results.Select(item => item.Validation.Outcome).ToList();
        return new ProjectTestRunSave
        {
            Decision = detection.Decision.ToString(),
            PassedCount = outcomes.Count(outcome => outcome is ContractValidationOutcome.Passed),
            FailedCount = outcomes.Count(outcome => outcome is ContractValidationOutcome.Failed),
            ErrorCount = outcomes.Count(outcome => outcome is ContractValidationOutcome.Error),
            FindingCount = detection.Findings.Count,
            Results = new PersistedTestRunResults
            {
                Tests = results.Select(ToPersistedCase).ToList(),
                Findings = detection.Findings.Select(ToPersistedFinding).ToList()
            }
        };
    }

    private static ApiTestExecutionResultDto ToDto(
        ApiTestExecutionResult result,
        ContractValidationResult validation,
        GeneratedApiTestCase test) =>
        new()
        {
            Kind = result.Kind.ToString(),
            Method = result.Method,
            Path = result.Path,
            SourceMethod = result.SourceMethod,
            SourcePath = result.SourcePath,
            SpecKey = test.SpecKey,
            ExpectedStatus = result.ExpectedStatus,
            ActualStatus = result.ActualStatus,
            Headers = result.Headers,
            Body = result.Body,
            DurationMs = result.DurationMs,
            StatusMatched = result.StatusMatched,
            Error = result.Error,
            Validation = ToDto(validation)
        };

    private static ContractValidationResultDto ToDto(ContractValidationResult validation) =>
        new()
        {
            Outcome = validation.Outcome.ToString(),
            StatusValid = validation.StatusValid,
            SchemaValid = validation.SchemaValid,
            Failures = validation.Failures.Select(ToDto).ToList()
        };

    private static ContractValidationFailureDto ToDto(ContractValidationFailure failure) =>
        new()
        {
            Code = failure.Code,
            JsonPath = failure.JsonPath,
            Message = failure.Message
        };

    private static RegressionFindingDto ToDto(RegressionFinding finding) =>
        new()
        {
            Category = finding.Category.ToString(),
            Code = finding.Code,
            Severity = finding.Severity.ToString(),
            Kind = finding.Kind?.ToString(),
            Method = finding.Method,
            Path = finding.Path,
            Expected = finding.Expected,
            Current = finding.Current,
            JsonPath = finding.JsonPath,
            Message = finding.Message
        };

    private static PersistedTestRunCase ToPersistedCase(TestsRunCaseResult item) =>
        new()
        {
            Kind = item.Execution.Kind.ToString(),
            SourceMethod = item.Execution.SourceMethod,
            SourcePath = item.Execution.SourcePath,
            SpecKey = item.Test.SpecKey,
            ExpectedStatus = item.Execution.ExpectedStatus,
            ActualStatus = item.Execution.ActualStatus,
            Outcome = item.Validation.Outcome.ToString(),
            ScenarioKey = item.Test.ScenarioKey,
            Failures = item.Validation.Failures.Select(failure => new PersistedValidationFailure
            {
                Code = failure.Code,
                JsonPath = failure.JsonPath,
                Message = failure.Message
            }).ToList()
        };

    private static PersistedRegressionFinding ToPersistedFinding(RegressionFinding finding) =>
        new()
        {
            Category = finding.Category.ToString(),
            Code = finding.Code,
            Severity = finding.Severity.ToString(),
            Kind = finding.Kind?.ToString(),
            Method = finding.Method,
            Path = finding.Path,
            Expected = finding.Expected,
            Current = finding.Current,
            JsonPath = finding.JsonPath,
            Message = finding.Message
        };
}
