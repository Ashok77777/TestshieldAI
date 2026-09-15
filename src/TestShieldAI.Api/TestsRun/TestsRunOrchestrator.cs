using Microsoft.Extensions.Options;
using TestShieldAI.Api.Ai;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.OpenApi;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.TestsRun;

public sealed class TestsRunOrchestrator
{
    private readonly IApiTestGenerator _generator;
    private readonly IAiScenarioGenerator _aiGenerator;
    private readonly IAiScenarioNormalizer _aiNormalizer;
    private readonly IApiTestRunner _runner;
    private readonly IApiContractValidator _validator;
    private readonly IRegressionDetector _detector;
    private readonly ICoverageCalculator _coverage;
    private readonly IRiskSummaryBuilder _risk;
    private readonly AiOptions _aiOptions;

    public TestsRunOrchestrator(
        IApiTestGenerator generator,
        IAiScenarioGenerator aiGenerator,
        IAiScenarioNormalizer aiNormalizer,
        IApiTestRunner runner,
        IApiContractValidator validator,
        IRegressionDetector detector,
        ICoverageCalculator coverage,
        IRiskSummaryBuilder risk,
        IOptions<AiOptions> aiOptions)
    {
        _generator = generator;
        _aiGenerator = aiGenerator;
        _aiNormalizer = aiNormalizer;
        _runner = runner;
        _validator = validator;
        _detector = detector;
        _coverage = coverage;
        _risk = risk;
        _aiOptions = aiOptions.Value;
    }

    public async Task<TestsBuildResult> BuildAsync(
        IReadOnlyList<SelectedOpenApiSpecification> specifications,
        CancellationToken cancellationToken)
    {
        specifications ??= [];
        var tests = new List<GeneratedApiTestCase>();
        var generatedCount = 0;
        var acceptedCount = 0;
        var warnings = new List<string>();
        var enabled = !AiCompletionClientFactory.UseNoOp(_aiOptions);
        var aiAvailable = enabled;

        if (!enabled)
        {
            var (_, summary, _) = await GenerateAiTestsAsync([], [], cancellationToken);
            warnings.AddRange(summary.Warnings);
        }

        foreach (var specification in specifications)
        {
            var deterministic = _generator.Generate(specification.Import.Operations);
            tests.AddRange(deterministic);

            if (!enabled)
            {
                continue;
            }

            var (aiTests, summary, available) = await GenerateAiTestsAsync(
                specification.Import.Operations,
                deterministic,
                cancellationToken);
            tests.AddRange(aiTests);
            generatedCount += summary.GeneratedCount;
            acceptedCount += summary.AcceptedCount;
            warnings.AddRange(summary.Warnings);
            aiAvailable &= available;
        }

        var operations = specifications
            .SelectMany(specification => specification.Import.Operations)
            .ToList();
        var coverage = _coverage.Calculate(operations, tests);
        var risk = _risk.Build(
            new RegressionDetectionResult([], RegressionDecision.Safe),
            coverage,
            testsExecuted: false);

        return new TestsBuildResult(
            tests,
            coverage,
            aiAvailable,
            new AiRunSummaryDto
            {
                Enabled = enabled,
                GeneratedCount = generatedCount,
                AcceptedCount = acceptedCount,
                WarningCount = warnings.Count,
                Warnings = warnings
            },
            risk);
    }

    public async Task<TestsRunExecution> ExecuteAsync(
        string baseUrl,
        IReadOnlyList<SelectedOpenApiSpecification> specifications,
        IReadOnlyList<RegressionTestSnapshot> baselineSnapshots,
        CancellationToken cancellationToken)
    {
        var built = await BuildAsync(specifications, cancellationToken);
        var executions = await _runner.RunAsync(baseUrl, built.Tests, cancellationToken);
        var results = built.Tests
            .Zip(executions, (test, execution) => new TestsRunCaseResult(
                test,
                execution,
                _validator.Validate(test, execution)))
            .ToList();

        var currentSnapshots = results
            .Select(item => RunTestsMapper.ToSnapshot(item.Test, item.Validation))
            .ToList();
        var (comparableBaseline, comparableCurrent) = TestsRunRegressionSelection.SelectComparable(
            baselineSnapshots,
            currentSnapshots,
            built.AiAvailable);
        var detection = _detector.Detect(comparableBaseline, comparableCurrent);
        var risk = _risk.Build(detection, built.Coverage, testsExecuted: true);

        return new TestsRunExecution(
            results,
            detection,
            currentSnapshots,
            TestsRunRegressionSelection.ShouldPromote(currentSnapshots),
            TestsRunRegressionSelection.SnapshotsToPromote(currentSnapshots),
            built.Ai,
            built.Coverage,
            risk);
    }

    private async Task<(IReadOnlyList<GeneratedApiTestCase> Tests, AiRunSummaryDto Summary, bool Available)>
        GenerateAiTestsAsync(
            IReadOnlyList<ImportedOperation> operations,
            IReadOnlyList<GeneratedApiTestCase> deterministic,
            CancellationToken cancellationToken)
    {
        var enabled = !AiCompletionClientFactory.UseNoOp(_aiOptions);
        AiScenarioGenerationResult generated;
        try
        {
            generated = await _aiGenerator
                .GenerateAsync(operations, deterministic, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            generated = new AiScenarioGenerationResult(
                [],
                ["AI scenario generation failed."],
                succeeded: false);
        }

        var normalized = _aiNormalizer.Normalize(generated.Scenarios, operations, deterministic);
        var warnings = generated.Warnings.Concat(normalized.Warnings).ToList();
        var summary = new AiRunSummaryDto
        {
            Enabled = enabled,
            GeneratedCount = generated.Scenarios.Count,
            AcceptedCount = normalized.Tests.Count,
            WarningCount = warnings.Count,
            Warnings = warnings
        };

        return (normalized.Tests, summary, enabled && generated.Succeeded);
    }
}

public sealed class TestsBuildResult
{
    public TestsBuildResult(
        IReadOnlyList<GeneratedApiTestCase> tests,
        CoverageCalculationResult coverage,
        bool aiAvailable,
        AiRunSummaryDto ai,
        RiskSummaryResult risk)
    {
        Tests = tests;
        Coverage = coverage;
        AiAvailable = aiAvailable;
        Ai = ai;
        Risk = risk;
    }

    public IReadOnlyList<GeneratedApiTestCase> Tests { get; }

    public CoverageCalculationResult Coverage { get; }

    public bool AiAvailable { get; }

    public AiRunSummaryDto Ai { get; }

    public RiskSummaryResult Risk { get; }
}

public sealed class TestsRunExecution
{
    public TestsRunExecution(
        IReadOnlyList<TestsRunCaseResult> results,
        RegressionDetectionResult detection,
        IReadOnlyList<RegressionTestSnapshot> currentSnapshots,
        bool promoteBaseline,
        IReadOnlyList<RegressionTestSnapshot> baselineSnapshotsToSave,
        AiRunSummaryDto ai,
        CoverageCalculationResult coverage,
        RiskSummaryResult risk)
    {
        Results = results;
        Detection = detection;
        CurrentSnapshots = currentSnapshots;
        PromoteBaseline = promoteBaseline;
        BaselineSnapshotsToSave = baselineSnapshotsToSave;
        Ai = ai;
        Coverage = coverage;
        Risk = risk;
    }

    public IReadOnlyList<TestsRunCaseResult> Results { get; }

    public RegressionDetectionResult Detection { get; }

    public IReadOnlyList<RegressionTestSnapshot> CurrentSnapshots { get; }

    public bool PromoteBaseline { get; }

    public IReadOnlyList<RegressionTestSnapshot> BaselineSnapshotsToSave { get; }

    public AiRunSummaryDto Ai { get; }

    public CoverageCalculationResult Coverage { get; }

    public RiskSummaryResult Risk { get; }
}

public sealed class TestsRunCaseResult
{
    public TestsRunCaseResult(
        GeneratedApiTestCase test,
        ApiTestExecutionResult execution,
        ContractValidationResult validation)
    {
        Test = test;
        Execution = execution;
        Validation = validation;
    }

    public GeneratedApiTestCase Test { get; }

    public ApiTestExecutionResult Execution { get; }

    public ContractValidationResult Validation { get; }
}
