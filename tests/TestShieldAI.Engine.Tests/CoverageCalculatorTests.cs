using System.Text.Json;
using System.Text.Json.Nodes;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class CoverageCalculatorTests
{
    private readonly ICoverageCalculator _calculator = new CoverageCalculator();

    [Fact]
    public void Calculate_EightOfTenDeterministic_Returns80Percent()
    {
        var operations = Enumerable.Range(1, 10).Select(index => Operation("GET", $"/p{index}")).ToList();
        var tests = Enumerable.Range(1, 8)
            .Select(index => Deterministic("GET", $"/p{index}"))
            .ToList();

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(10, result.TotalOperations);
        Assert.Equal(8, result.CoveredOperations);
        Assert.Equal(2, result.UncoveredOperations);
        Assert.Equal(80m, result.CoveragePercent);
        Assert.Equal(2, result.Gaps.Count);
        Assert.All(result.Gaps, gap => Assert.Equal(CoverageCalculator.NoDeterministicTest, gap.Reason));
        Assert.Contains(result.Gaps, gap => gap.Path == "/p9");
        Assert.Contains(result.Gaps, gap => gap.Path == "/p10");
    }

    [Fact]
    public void Calculate_TenOperationsNoneCovered_Returns0Percent()
    {
        var operations = Enumerable.Range(1, 10).Select(index => Operation("GET", $"/p{index}")).ToList();

        var result = _calculator.Calculate(operations, []);

        Assert.Equal(10, result.TotalOperations);
        Assert.Equal(0, result.CoveredOperations);
        Assert.Equal(10, result.UncoveredOperations);
        Assert.Equal(0m, result.CoveragePercent);
        Assert.Equal(10, result.Gaps.Count);
    }

    [Fact]
    public void Calculate_ZeroOperations_Returns100Percent()
    {
        var result = _calculator.Calculate([], [Deterministic("GET", "/pets")]);

        Assert.Equal(0, result.TotalOperations);
        Assert.Equal(0, result.CoveredOperations);
        Assert.Equal(0, result.UncoveredOperations);
        Assert.Equal(100m, result.CoveragePercent);
        Assert.Empty(result.Gaps);
        Assert.Equal(1, result.Scenarios.HappyPath);
    }

    [Fact]
    public void Calculate_DoesNotUseExecutionOutcome()
    {
        var operations = new[] { Operation("GET", "/pets") };
        var tests = new[] { Deterministic("GET", "/pets") };

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(1, result.CoveredOperations);
        Assert.Equal(100m, result.CoveragePercent);
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Calculate_AiTestsAlone_DoNotIncreasePrimaryCoverage()
    {
        var operations = new[] { Operation("GET", "/pets") };
        var tests = new[]
        {
            Test(GeneratedApiTestKind.AiPositive, "GET", "/pets"),
            Test(GeneratedApiTestKind.AiNegative, "GET", "/pets"),
            Test(GeneratedApiTestKind.AiEdge, "GET", "/pets")
        };

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(1, result.TotalOperations);
        Assert.Equal(0, result.CoveredOperations);
        Assert.Equal(1, result.UncoveredOperations);
        Assert.Equal(0m, result.CoveragePercent);
        Assert.Equal(1, result.AiCoveredOperations);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal(CoverageCalculator.NoDeterministicTest, gap.Reason);
        Assert.Equal("GET", gap.Method);
        Assert.Equal("/pets", gap.Path);
    }

    [Fact]
    public void Calculate_AiScenarioCounts_AreCorrect()
    {
        var tests = new[]
        {
            Deterministic("GET", "/pets"),
            Test(GeneratedApiTestKind.NegativeMissingRequiredBody, "POST", "/pets"),
            Test(GeneratedApiTestKind.AiPositive, "GET", "/pets"),
            Test(GeneratedApiTestKind.AiPositive, "POST", "/pets"),
            Test(GeneratedApiTestKind.AiNegative, "POST", "/pets"),
            Test(GeneratedApiTestKind.AiEdge, "GET", "/pets")
        };

        var result = _calculator.Calculate([], tests);

        Assert.Equal(1, result.Scenarios.HappyPath);
        Assert.Equal(1, result.Scenarios.Negative);
        Assert.Equal(2, result.Scenarios.AiPositive);
        Assert.Equal(1, result.Scenarios.AiNegative);
        Assert.Equal(1, result.Scenarios.AiEdge);
        Assert.Equal(0, result.AiCoveredOperations);
    }

    [Fact]
    public void Calculate_AiCoveredOperations_CountsDistinctOperations()
    {
        var operations = new[]
        {
            Operation("GET", "/pets"),
            Operation("POST", "/pets")
        };
        var tests = new[]
        {
            Test(GeneratedApiTestKind.AiPositive, "GET", "/pets"),
            Test(GeneratedApiTestKind.AiNegative, "GET", "/pets"),
            Test(GeneratedApiTestKind.AiEdge, "GET", "/pets"),
            Test(GeneratedApiTestKind.AiPositive, "POST", "/pets")
        };

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(2, result.AiCoveredOperations);
        Assert.Equal(0, result.CoveredOperations);
    }

    [Fact]
    public void Calculate_DuplicateDeterministicTests_CountOperationOnce()
    {
        var operations = new[] { Operation("GET", "/pets") };
        var tests = new[]
        {
            Deterministic("GET", "/pets"),
            Test(GeneratedApiTestKind.NegativeMissingRequiredBody, "GET", "/pets")
        };

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(1, result.TotalOperations);
        Assert.Equal(1, result.CoveredOperations);
        Assert.Equal(100m, result.CoveragePercent);
        Assert.Equal(1, result.Scenarios.HappyPath);
        Assert.Equal(1, result.Scenarios.Negative);
    }

    [Fact]
    public void Calculate_DuplicateAiTests_CountOneAiCoveredOperation()
    {
        var operations = new[] { Operation("GET", "/pets") };
        var tests = new[]
        {
            Deterministic("GET", "/pets"),
            Test(GeneratedApiTestKind.AiPositive, "GET", "/pets"),
            Test(GeneratedApiTestKind.AiNegative, "GET", "/pets")
        };

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(1, result.AiCoveredOperations);
        Assert.Equal(1, result.CoveredOperations);
    }

    [Fact]
    public void Calculate_SameMethodAndPath_DifferentSpecKeys_AreSeparateOperations()
    {
        var operations = new[]
        {
            Operation("GET", "/pets", "Customer"),
            Operation("POST", "/pets", "Customer"),
            Operation("GET", "/pets", "Order")
        };
        var tests = new[]
        {
            Deterministic("GET", "/pets", "Customer"),
            Deterministic("POST", "/pets", "Customer")
        };

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(3, result.TotalOperations);
        Assert.Equal(2, result.CoveredOperations);
        Assert.Equal(1, result.UncoveredOperations);
        Assert.Equal(66.67m, result.CoveragePercent);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal("Order", gap.SpecKey);
        Assert.Equal("GET", gap.Method);
        Assert.Equal("/pets", gap.Path);
        Assert.Equal(CoverageCalculator.NoDeterministicTest, gap.Reason);
    }

    [Fact]
    public void Calculate_Gap_ContainsOnlySpecKeyMethodPathReason()
    {
        var result = _calculator.Calculate([Operation("GET", "/pets", "Customer")], []);
        var gap = Assert.Single(result.Gaps);
        var json = JsonSerializer.SerializeToNode(gap)!.AsObject();

        Assert.Equal(4, json.Count);
        Assert.Equal("Customer", json["SpecKey"]!.GetValue<string>());
        Assert.Equal("GET", json["Method"]!.GetValue<string>());
        Assert.Equal("/pets", json["Path"]!.GetValue<string>());
        Assert.Equal(CoverageCalculator.NoDeterministicTest, json["Reason"]!.GetValue<string>());
        Assert.False(json.ContainsKey("RequestBody"));
        Assert.False(json.ContainsKey("Headers"));
        Assert.False(json.ContainsKey("Body"));
    }

    [Fact]
    public void Calculate_MultipleSpecifications_UseCombinedOperationTotal()
    {
        var operations = new[]
        {
            Operation("GET", "/pets", "A"),
            Operation("POST", "/pets", "A"),
            Operation("GET", "/pets", "B")
        };

        var result = _calculator.Calculate(operations, [Deterministic("GET", "/pets", "A")]);

        Assert.Equal(3, result.TotalOperations);
        Assert.Equal(1, result.CoveredOperations);
        Assert.Equal(2, result.UncoveredOperations);
        Assert.Equal(33.33m, result.CoveragePercent);
    }

    [Fact]
    public void Calculate_MissingSpecKey_MatchesGate1Identity()
    {
        var operations = new[] { Operation("GET", "/pets") };
        var tests = new[] { Deterministic("GET", "/pets") };

        var result = _calculator.Calculate(operations, tests);

        Assert.Equal(1, result.CoveredOperations);
        Assert.Empty(result.Gaps);
        Assert.Equal("", OpenApiSpecKey.CanonicalDisplay(operations[0].SpecKey));
    }

    [Fact]
    public void Calculate_UsesSourcePathNotSubstitutedPath()
    {
        var operations = new[] { Operation("GET", "/pets/{id}", "Customer") };
        var covered = _calculator.Calculate(
            operations,
            [
                new GeneratedApiTestCase(
                    GeneratedApiTestKind.HappyPath,
                    "GET",
                    "/pets/{id}",
                    "/pets/1",
                    [],
                    null,
                    200,
                    null,
                    "GET",
                    "/pets/{id}",
                    specKey: "Customer")
            ]);
        var notCovered = _calculator.Calculate(
            operations,
            [
                new GeneratedApiTestCase(
                    GeneratedApiTestKind.HappyPath,
                    "GET",
                    "/pets/{id}",
                    "/pets/1",
                    [],
                    null,
                    200,
                    null,
                    "GET",
                    "/pets/1",
                    specKey: "Customer")
            ]);

        Assert.Equal(1, covered.CoveredOperations);
        Assert.Empty(covered.Gaps);
        Assert.Equal(0, notCovered.CoveredOperations);
        Assert.Equal("/pets/{id}", Assert.Single(notCovered.Gaps).Path);
    }

    [Fact]
    public void Calculate_DuplicateOperations_CountOnce()
    {
        var operations = new[]
        {
            Operation("GET", "/pets", "Pets"),
            Operation("GET", "/pets", "pets")
        };

        var result = _calculator.Calculate(operations, [Deterministic("GET", "/pets", "PETS")]);

        Assert.Equal(1, result.TotalOperations);
        Assert.Equal(1, result.CoveredOperations);
        Assert.Empty(result.Gaps);
    }

    private static ImportedOperation Operation(string method, string path, string? specKey = null) =>
        new(method, path, [], null, new Dictionary<string, ImportedSchema?>(), specKey);

    private static GeneratedApiTestCase Deterministic(string method, string path, string? specKey = null) =>
        Test(GeneratedApiTestKind.HappyPath, method, path, specKey);

    private static GeneratedApiTestCase Test(
        GeneratedApiTestKind kind,
        string method,
        string sourcePath,
        string? specKey = null) =>
        new(
            kind,
            method,
            sourcePath,
            sourcePath,
            [],
            null,
            200,
            null,
            method,
            sourcePath,
            specKey: specKey);
}
