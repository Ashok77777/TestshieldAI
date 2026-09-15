using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class RegressionDetectorTests
{
    private readonly IRegressionDetector _detector = new RegressionDetector();

    [Fact]
    public void Detect_EmptyBaselineAllCurrentPassed_ReturnsSafe()
    {
        var current = new[]
        {
            Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed)
        };

        var result = _detector.Detect([], current);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_UnchangedBaseline_ReturnsSafe()
    {
        var snapshot = Snapshot("GET", "/pets", 200, ObjectSchema(["id"]), ContractValidationOutcome.Passed);

        var result = _detector.Detect([snapshot], [snapshot]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_ExpectedStatusChanged_ReportsHighDriftAndReviewWhenStillPassed()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 201, ObjectSchema(), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Review, result.Decision);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionFindingCategory.ContractDrift, finding.Category);
        Assert.Equal(RegressionDetector.ExpectedStatusChanged, finding.Code);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
        Assert.Equal("200", finding.Expected);
        Assert.Equal("201", finding.Current);
        Assert.DoesNotContain(result.Findings, item => item.Category == RegressionFindingCategory.Regression);
    }

    [Fact]
    public void Detect_OperationRemoved_ReportsOperationRemovedOnce()
    {
        var baseline = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, "GET", "/pets", 200, null, ContractValidationOutcome.Passed),
            Snapshot(GeneratedApiTestKind.NegativeMissingRequiredBody, "GET", "/pets", 400, null, ContractValidationOutcome.Passed)
        };
        var current = new[]
        {
            Snapshot("GET", "/health", 200, null, ContractValidationOutcome.Passed)
        };

        var result = _detector.Detect(baseline, current);

        Assert.Equal(RegressionDecision.Review, result.Decision);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.OperationRemoved, finding.Code);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
        Assert.Null(finding.Kind);
        Assert.Equal("/pets", finding.Path);
        Assert.DoesNotContain(result.Findings, item => item.Code == RegressionDetector.TestRemoved);
    }

    [Fact]
    public void Detect_TestKindRemoved_ReportsTestRemoved()
    {
        var baseline = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, "POST", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed),
            Snapshot(GeneratedApiTestKind.NegativeMissingRequiredBody, "POST", "/pets", 400, null, ContractValidationOutcome.Passed)
        };
        var current = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, "POST", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed)
        };

        var result = _detector.Detect(baseline, current);

        Assert.Equal(RegressionDecision.Review, result.Decision);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.TestRemoved, finding.Code);
        Assert.Equal(GeneratedApiTestKind.NegativeMissingRequiredBody, finding.Kind);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
    }

    [Fact]
    public void Detect_SchemaRootTypeChanged_ReportsSchemaTypeChanged()
    {
        var baseline = Snapshot("GET", "/pets", 200, ArraySchema(IntegerSchema()), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings, item => item.Code == RegressionDetector.SchemaTypeChanged);
        Assert.Equal("array", finding.Expected);
        Assert.Equal("object", finding.Current);
        Assert.Equal("$", finding.JsonPath);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
        Assert.Equal(RegressionDecision.Review, result.Decision);
    }

    [Fact]
    public void Detect_RequiredPropertyRemoved_ReportsSchemaRequiredRemoved()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(["name"]), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.SchemaRequiredRemoved, finding.Code);
        Assert.Equal("name", finding.Expected);
        Assert.Equal("$.name", finding.JsonPath);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
    }

    [Fact]
    public void Detect_RequiredPropertyAdded_ReportsMediumDrift()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(["id"]), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.SchemaRequiredAdded, finding.Code);
        Assert.Equal("id", finding.Current);
        Assert.Equal(RegressionFindingSeverity.Medium, finding.Severity);
        Assert.Equal(RegressionDecision.Review, result.Decision);
    }

    [Fact]
    public void Detect_PropertyRemoved_ReportsSchemaPropertyRemoved()
    {
        var baseline = Snapshot(
            "GET",
            "/pets",
            200,
            ObjectSchema(required: [], properties: new Dictionary<string, ImportedSchema> { ["age"] = IntegerSchema() }),
            ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.SchemaPropertyRemoved, finding.Code);
        Assert.Equal("age", finding.Expected);
        Assert.Equal("$.age", finding.JsonPath);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
    }

    [Fact]
    public void Detect_EnumChanged_ReportsSchemaEnumChanged()
    {
        var baseline = Snapshot("GET", "/pets", 200, EnumSchema("active", "inactive"), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, EnumSchema("active", "paused"), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.SchemaEnumChanged, finding.Code);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
        Assert.Contains("inactive", finding.Expected);
        Assert.Contains("paused", finding.Current);
    }

    [Fact]
    public void Detect_EnumValueAddedOnly_ReportsMediumSeverity()
    {
        var baseline = Snapshot("GET", "/pets", 200, EnumSchema("active"), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, EnumSchema("active", "paused"), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.SchemaEnumChanged, finding.Code);
        Assert.Equal(RegressionFindingSeverity.Medium, finding.Severity);
        Assert.Equal(RegressionDecision.Review, result.Decision);
    }

    [Fact]
    public void Detect_ArrayItemSchemaChanged_ReportsSchemaItemsChanged()
    {
        var baseline = Snapshot("GET", "/pets", 200, ArraySchema(IntegerSchema()), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ArraySchema(StringSchema()), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        Assert.Contains(result.Findings, item => item.Code == RegressionDetector.SchemaItemsChanged);
        Assert.Equal(RegressionDecision.Review, result.Decision);
        Assert.All(result.Findings, item => Assert.Equal(RegressionFindingCategory.ContractDrift, item.Category));
    }

    [Fact]
    public void Detect_NestedSchemaChange_ReportsNestedTypeChange()
    {
        var baseline = Snapshot(
            "GET",
            "/pets",
            200,
            ObjectSchema(
                ["pet"],
                new Dictionary<string, ImportedSchema>
                {
                    ["pet"] = ObjectSchema(["name"], new Dictionary<string, ImportedSchema> { ["name"] = StringSchema() })
                }),
            ContractValidationOutcome.Passed);
        var current = Snapshot(
            "GET",
            "/pets",
            200,
            ObjectSchema(
                ["pet"],
                new Dictionary<string, ImportedSchema>
                {
                    ["pet"] = ObjectSchema(["name"], new Dictionary<string, ImportedSchema> { ["name"] = IntegerSchema() })
                }),
            ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.SchemaTypeChanged, finding.Code);
        Assert.Equal("$.pet.name", finding.JsonPath);
        Assert.Equal("string", finding.Expected);
        Assert.Equal("integer", finding.Current);
    }

    [Fact]
    public void Detect_BaselinePassedCurrentFailed_ReportsValidationRegressedAndBlock()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Failed);

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Block, result.Decision);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionFindingCategory.Regression, finding.Category);
        Assert.Equal(RegressionDetector.ValidationRegressed, finding.Code);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
        Assert.DoesNotContain(result.Findings, item => item.Category == RegressionFindingCategory.ContractDrift);
    }

    [Fact]
    public void Detect_BaselinePassedCurrentError_ReportsExecutionRegressedAndBlock()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Error);

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Block, result.Decision);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.ExecutionRegressed, finding.Code);
        Assert.Equal(RegressionFindingCategory.Regression, finding.Category);
        Assert.DoesNotContain(result.Findings, item => item.Code == RegressionDetector.ExecutionError);
    }

    [Fact]
    public void Detect_CurrentFailedWithNoBaseline_IsNotRegression()
    {
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Failed);

        var result = _detector.Detect([], [current]);

        Assert.Equal(RegressionDecision.Review, result.Decision);
        Assert.Empty(result.Findings);
        Assert.DoesNotContain(result.Findings, item => item.Code == RegressionDetector.ValidationRegressed);
    }

    [Fact]
    public void Detect_CurrentErrorWithNoBaseline_IsExecutionFailureReview()
    {
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Error);

        var result = _detector.Detect([], [current]);

        Assert.Equal(RegressionDecision.Review, result.Decision);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionFindingCategory.ExecutionFailure, finding.Category);
        Assert.Equal(RegressionDetector.ExecutionError, finding.Code);
        Assert.Equal(RegressionFindingSeverity.Medium, finding.Severity);
        Assert.DoesNotContain(result.Findings, item => item.Category == RegressionFindingCategory.Regression);
    }

    [Fact]
    public void Detect_NewOperation_IsNotAFinding()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);
        var current = new[]
        {
            Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed),
            Snapshot("GET", "/health", 200, ObjectSchema(), ContractValidationOutcome.Passed)
        };

        var result = _detector.Detect([baseline], current);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_Findings_AreDeterministicallyOrdered()
    {
        var baseline = new[]
        {
            Snapshot("GET", "/zebra", 200, null, ContractValidationOutcome.Passed),
            Snapshot("GET", "/aardvark", 200, null, ContractValidationOutcome.Passed)
        };
        var current = new[]
        {
            Snapshot("GET", "/health", 200, null, ContractValidationOutcome.Passed)
        };

        var first = _detector.Detect(baseline, current);
        var second = _detector.Detect(baseline.Reverse().ToList(), current);

        Assert.Equal(2, first.Findings.Count);
        Assert.Equal(first.Findings.Select(item => item.Path), second.Findings.Select(item => item.Path));
        Assert.Equal("/aardvark", first.Findings[0].Path);
        Assert.Equal("/zebra", first.Findings[1].Path);
        Assert.All(first.Findings, item => Assert.Equal(RegressionDetector.OperationRemoved, item.Code));
    }

    [Fact]
    public void Detect_SubstitutedPathIsNotIdentity()
    {
        var baseline = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets/{id}",
            "GET",
            "/pets/{id}",
            200,
            ObjectSchema(),
            ContractValidationOutcome.Passed);
        var current = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets/{id}",
            "GET",
            "/pets/{id}",
            200,
            ObjectSchema(),
            ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_SchemaPresenceChanged_ReportsSchemaChanged()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, null, ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.SchemaChanged, finding.Code);
        Assert.Equal(RegressionFindingSeverity.High, finding.Severity);
        Assert.Equal("$", finding.JsonPath);
    }

    [Fact]
    public void Detect_RequiredOrderDoesNotMatter()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(["id", "name"]), ContractValidationOutcome.Passed);
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(["name", "id"]), ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_DifferentAiScenarioKeys_AreDistinctTests()
    {
        var first = AiSnapshot("edge-empty", 200, ContractValidationOutcome.Passed);
        var second = AiSnapshot("edge-long", 200, ContractValidationOutcome.Passed);

        var unchanged = _detector.Detect([first, second], [first, second]);
        Assert.Equal(RegressionDecision.Safe, unchanged.Decision);
        Assert.Empty(unchanged.Findings);

        var missingSecond = _detector.Detect([first, second], [first]);
        Assert.Equal(RegressionDecision.Review, missingSecond.Decision);
        var removed = Assert.Single(missingSecond.Findings);
        Assert.Equal(RegressionDetector.TestRemoved, removed.Code);
        Assert.Equal(GeneratedApiTestKind.AiEdge, removed.Kind);
        Assert.DoesNotContain(missingSecond.Findings, item => item.Code == RegressionDetector.OperationRemoved);
    }

    [Fact]
    public void Detect_SameAiScenarioKey_IsTheSameTest()
    {
        var baseline = AiSnapshot("edge-empty", 200, ContractValidationOutcome.Passed);
        var current = AiSnapshot("edge-empty", 201, ContractValidationOutcome.Passed);

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Review, result.Decision);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.ExpectedStatusChanged, finding.Code);
        Assert.DoesNotContain(result.Findings, item => item.Code == RegressionDetector.TestRemoved);
    }

    [Fact]
    public void Detect_NullAndEmptyScenarioKey_MatchAsGate1Identity()
    {
        var baseline = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "GET",
            "/pets",
            200,
            ObjectSchema(),
            ContractValidationOutcome.Passed,
            scenarioKey: null);
        var current = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "GET",
            "/pets",
            200,
            ObjectSchema(),
            ContractValidationOutcome.Passed,
            scenarioKey: "  ");

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_DifferentSpecKeys_SameMethodAndPath_AreDistinctTests()
    {
        var customer = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Customer");
        var order = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Order");

        var result = _detector.Detect([customer], [customer, order]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_CustomerGetPets_IsNotEqualTo_OrderGetPets()
    {
        var customer = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Customer");
        var order = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Order");

        var missingOrder = _detector.Detect([customer, order], [customer]);
        var finding = Assert.Single(missingOrder.Findings);
        Assert.Equal(RegressionDetector.OperationRemoved, finding.Code);
        Assert.Equal("/pets", finding.Path);

        var swapped = _detector.Detect([customer], [order]);
        Assert.Contains(swapped.Findings, item => item.Code == RegressionDetector.OperationRemoved);
        Assert.DoesNotContain(swapped.Findings, item => item.Code == RegressionDetector.ExpectedStatusChanged);
    }

    [Fact]
    public void Detect_EmptySpecKey_MatchesGate1Identity()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed);
        var current = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "GET",
            "/pets",
            200,
            ObjectSchema(),
            ContractValidationOutcome.Passed,
            scenarioKey: null,
            specKey: "");

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_SpecKeyComparison_IsCaseInsensitiveAndIgnoresVersionDisplayDifferences()
    {
        var baseline = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Customer API");
        var current = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "customer api");

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Detect_NewUnrelatedSpecKey_DoesNotReportTestRemoved()
    {
        var existing = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Customer");
        var added = Snapshot("GET", "/orders", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Order");

        var result = _detector.Detect([existing], [existing, added]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.DoesNotContain(result.Findings, item => item.Code == RegressionDetector.TestRemoved);
        Assert.DoesNotContain(result.Findings, item => item.Code == RegressionDetector.OperationRemoved);
    }

    [Fact]
    public void Detect_RemovedOperationInsideExistingSpecKey_IsDetected()
    {
        var pets = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Customer");
        var health = Snapshot("GET", "/health", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Customer");
        var orders = Snapshot("GET", "/orders", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Order");

        var result = _detector.Detect([pets, health, orders], [pets, orders]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RegressionDetector.OperationRemoved, finding.Code);
        Assert.Equal("/health", finding.Path);
        Assert.DoesNotContain(result.Findings, item => item.Path == "/pets" || item.Path == "/orders");
    }

    [Fact]
    public void Detect_RemovedEntireSpecKey_ReportsOnlyThatSpecification()
    {
        var customer = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Customer");
        var orderPets = Snapshot("GET", "/pets", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Order");
        var orderItems = Snapshot("GET", "/items", 200, ObjectSchema(), ContractValidationOutcome.Passed, specKey: "Order");

        var result = _detector.Detect([customer, orderPets, orderItems], [customer]);

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, finding => Assert.Equal(RegressionDetector.OperationRemoved, finding.Code));
        Assert.Contains(result.Findings, finding => finding.Path == "/pets");
        Assert.Contains(result.Findings, finding => finding.Path == "/items");
        Assert.DoesNotContain(result.Findings, item => item.Code == RegressionDetector.TestRemoved);
    }

    [Fact]
    public void Detect_SubstitutedPath_IsNotUsedAsIdentity()
    {
        var baseline = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets/{id}",
            "GET",
            "/pets/{id}",
            200,
            ObjectSchema(),
            ContractValidationOutcome.Passed,
            specKey: "Customer");
        var current = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets/{id}",
            "GET",
            "/pets/999",
            200,
            ObjectSchema(),
            ContractValidationOutcome.Passed,
            specKey: "Customer");

        var result = _detector.Detect([baseline], [current]);

        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    private static RegressionTestSnapshot Snapshot(
        string method,
        string path,
        int expectedStatus,
        ImportedSchema? schema,
        ContractValidationOutcome outcome,
        string? specKey = null) =>
        Snapshot(GeneratedApiTestKind.HappyPath, method, path, expectedStatus, schema, outcome, specKey);

    private static RegressionTestSnapshot Snapshot(
        GeneratedApiTestKind kind,
        string method,
        string path,
        int expectedStatus,
        ImportedSchema? schema,
        ContractValidationOutcome outcome,
        string? specKey = null) =>
        new(
            kind,
            method,
            path,
            method,
            path,
            expectedStatus,
            schema,
            outcome,
            specKey: specKey);

    private static RegressionTestSnapshot AiSnapshot(
        string scenarioKey,
        int expectedStatus,
        ContractValidationOutcome outcome) =>
        new(
            GeneratedApiTestKind.AiEdge,
            "GET",
            "/pets",
            "GET",
            "/pets",
            expectedStatus,
            ObjectSchema(),
            outcome,
            scenarioKey);

    private static ImportedSchema ObjectSchema(
        IReadOnlyList<string>? required = null,
        IReadOnlyDictionary<string, ImportedSchema>? properties = null) =>
        new("object", required ?? [], properties ?? new Dictionary<string, ImportedSchema>(), [], null);

    private static ImportedSchema ArraySchema(ImportedSchema items) =>
        new("array", [], new Dictionary<string, ImportedSchema>(), [], items);

    private static ImportedSchema EnumSchema(params string[] values) =>
        new("string", [], new Dictionary<string, ImportedSchema>(), values, null);

    private static ImportedSchema StringSchema() =>
        new("string", [], new Dictionary<string, ImportedSchema>(), [], null);

    private static ImportedSchema IntegerSchema() =>
        new("integer", [], new Dictionary<string, ImportedSchema>(), [], null);
}
