using TestShieldAI.Api.Contracts;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class AiScenarioModelsTests
{
    [Fact]
    public void GeneratedApiTestKind_IncludesAiValuesWithoutChangingGate1Values()
    {
        Assert.Equal(0, (int)GeneratedApiTestKind.HappyPath);
        Assert.Equal(1, (int)GeneratedApiTestKind.NegativeMissingRequiredBody);
        Assert.Equal(GeneratedApiTestKind.AiPositive, Enum.Parse<GeneratedApiTestKind>("AiPositive"));
        Assert.Equal(GeneratedApiTestKind.AiNegative, Enum.Parse<GeneratedApiTestKind>("AiNegative"));
        Assert.Equal(GeneratedApiTestKind.AiEdge, Enum.Parse<GeneratedApiTestKind>("AiEdge"));
    }

    [Fact]
    public void GeneratedApiTestCase_Gate1Construction_LeavesScenarioKeyAndRationaleNull()
    {
        var test = new GeneratedApiTestCase(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets/{id}",
            "/pets/1",
            [],
            requestBody: null,
            expectedStatus: 200,
            expectedResponseSchema: null,
            "GET",
            "/pets/{id}");

        Assert.Null(test.ScenarioKey);
        Assert.Null(test.Rationale);
        Assert.Equal(GeneratedApiTestKind.HappyPath, test.Kind);
        Assert.Equal("/pets/{id}", test.SourcePath);
        Assert.Equal("/pets/1", test.Path);
    }

    [Fact]
    public void GeneratedApiTestCase_PreservesScenarioKeyAndRationale()
    {
        var test = new GeneratedApiTestCase(
            GeneratedApiTestKind.AiEdge,
            "POST",
            "/pets",
            "/pets",
            [new GeneratedApiParameter("limit", "query", false, "1")],
            """{"name":""}""",
            400,
            expectedResponseSchema: null,
            "POST",
            "/pets",
            "edge-empty-name",
            "Empty name is an edge case for required strings.");

        Assert.Equal("edge-empty-name", test.ScenarioKey);
        Assert.Equal("Empty name is an edge case for required strings.", test.Rationale);
        Assert.Equal(GeneratedApiTestKind.AiEdge, test.Kind);
    }

    [Fact]
    public void RegressionTestSnapshot_PreservesScenarioKey()
    {
        var snapshot = new RegressionTestSnapshot(
            GeneratedApiTestKind.AiPositive,
            "GET",
            "/pets",
            "GET",
            "/pets",
            200,
            expectedResponseSchema: null,
            ContractValidationOutcome.Passed,
            "pos-1");

        Assert.Equal("pos-1", snapshot.ScenarioKey);
    }

    [Fact]
    public void RunTestsMapper_CopiesScenarioKeyFromGeneratedTest()
    {
        var test = new GeneratedApiTestCase(
            GeneratedApiTestKind.AiNegative,
            "POST",
            "/pets",
            "/pets",
            [],
            "{}",
            400,
            expectedResponseSchema: null,
            "POST",
            "/pets",
            "neg-missing-name",
            "Omit name.");
        var validation = new ContractValidationResult(
            ContractValidationOutcome.Passed,
            statusValid: true,
            schemaValid: null,
            []);

        var snapshot = RunTestsMapper.ToSnapshot(test, validation);

        Assert.Equal("neg-missing-name", snapshot.ScenarioKey);
        Assert.Equal(GeneratedApiTestKind.AiNegative, snapshot.Kind);
        Assert.Equal("POST", snapshot.SourceMethod);
        Assert.Equal("/pets", snapshot.SourcePath);
        Assert.Equal(ContractValidationOutcome.Passed, snapshot.Outcome);
    }

    [Fact]
    public void AiTestScenarioAndResult_HoldProposedScenariosAndOutcome()
    {
        var scenario = new AiTestScenario(
            AiScenarioType.Positive,
            "GET",
            "/pets",
            "/pets",
            [],
            requestBody: null,
            expectedStatus: 200,
            expectedResponseSchema: null,
            "Return the list of pets.",
            "GET",
            "/pets");
        var result = new AiScenarioGenerationResult([scenario], ["truncated"], succeeded: true);

        Assert.Equal(AiScenarioType.Positive, Assert.Single(result.Scenarios).Type);
        Assert.Equal("truncated", Assert.Single(result.Warnings));
        Assert.True(result.Succeeded);
    }
}
