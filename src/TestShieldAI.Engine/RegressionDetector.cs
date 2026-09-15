using System.Globalization;

namespace TestShieldAI.Engine;

public sealed class RegressionDetector : IRegressionDetector
{
    public const string OperationRemoved = "OPERATION_REMOVED";
    public const string TestRemoved = "TEST_REMOVED";
    public const string ExpectedStatusChanged = "EXPECTED_STATUS_CHANGED";
    public const string SchemaTypeChanged = "SCHEMA_TYPE_CHANGED";
    public const string SchemaRequiredRemoved = "SCHEMA_REQUIRED_REMOVED";
    public const string SchemaRequiredAdded = "SCHEMA_REQUIRED_ADDED";
    public const string SchemaPropertyRemoved = "SCHEMA_PROPERTY_REMOVED";
    public const string SchemaEnumChanged = "SCHEMA_ENUM_CHANGED";
    public const string SchemaItemsChanged = "SCHEMA_ITEMS_CHANGED";
    public const string SchemaChanged = "SCHEMA_CHANGED";
    public const string ValidationRegressed = "VALIDATION_REGRESSED";
    public const string ExecutionRegressed = "EXECUTION_REGRESSED";
    public const string ExecutionError = "EXECUTION_ERROR";

    public RegressionDetectionResult Detect(
        IReadOnlyList<RegressionTestSnapshot> baseline,
        IReadOnlyList<RegressionTestSnapshot> current)
    {
        baseline ??= [];
        current ??= [];

        var baselineById = IndexByIdentity(baseline);
        var currentById = IndexByIdentity(current);
        var findings = new List<RegressionFinding>();

        AddRemovalFindings(baselineById, currentById, findings);

        foreach (var (id, baselineTest) in baselineById)
        {
            if (!currentById.TryGetValue(id, out var currentTest))
            {
                continue;
            }

            AddExpectationDrift(baselineTest, currentTest, findings);
            AddRegressionFinding(baselineTest, currentTest, findings);
        }

        AddExecutionFailuresWithoutBaseline(baselineById, currentById, findings);

        findings.Sort(CompareFindings);
        return new RegressionDetectionResult(findings, Decide(current, findings));
    }

    private static Dictionary<TestIdentity, RegressionTestSnapshot> IndexByIdentity(
        IReadOnlyList<RegressionTestSnapshot> snapshots)
    {
        var indexed = new Dictionary<TestIdentity, RegressionTestSnapshot>();
        foreach (var snapshot in snapshots)
        {
            indexed.TryAdd(Identity(snapshot), snapshot);
        }

        return indexed;
    }

    private static void AddRemovalFindings(
        IReadOnlyDictionary<TestIdentity, RegressionTestSnapshot> baselineById,
        IReadOnlyDictionary<TestIdentity, RegressionTestSnapshot> currentById,
        List<RegressionFinding> findings)
    {
        var currentOperations = currentById.Keys
            .Select(id => id.Operation)
            .ToHashSet();
        var removedOperations = new HashSet<OperationIdentity>();

        foreach (var baselineTest in baselineById.Values)
        {
            var operation = Operation(baselineTest);
            if (currentOperations.Contains(operation) || !removedOperations.Add(operation))
            {
                continue;
            }

            findings.Add(new RegressionFinding(
                RegressionFindingCategory.ContractDrift,
                OperationRemoved,
                RegressionFindingSeverity.High,
                kind: null,
                baselineTest.SourceMethod,
                baselineTest.SourcePath,
                "present",
                "removed",
                jsonPath: null,
                $"{baselineTest.SourceMethod} {baselineTest.SourcePath} was removed from the generated suite."));
        }

        foreach (var (id, baselineTest) in baselineById)
        {
            if (currentById.ContainsKey(id) || removedOperations.Contains(id.Operation))
            {
                continue;
            }

            findings.Add(new RegressionFinding(
                RegressionFindingCategory.ContractDrift,
                TestRemoved,
                RegressionFindingSeverity.High,
                baselineTest.Kind,
                baselineTest.SourceMethod,
                baselineTest.SourcePath,
                baselineTest.Kind.ToString(),
                "removed",
                jsonPath: null,
                $"{baselineTest.Kind} for {baselineTest.SourceMethod} {baselineTest.SourcePath} was removed."));
        }
    }

    private static void AddExpectationDrift(
        RegressionTestSnapshot baseline,
        RegressionTestSnapshot current,
        List<RegressionFinding> findings)
    {
        if (baseline.ExpectedStatus != current.ExpectedStatus)
        {
            findings.Add(new RegressionFinding(
                RegressionFindingCategory.ContractDrift,
                ExpectedStatusChanged,
                RegressionFindingSeverity.High,
                current.Kind,
                current.SourceMethod,
                current.SourcePath,
                baseline.ExpectedStatus.ToString(CultureInfo.InvariantCulture),
                current.ExpectedStatus.ToString(CultureInfo.InvariantCulture),
                jsonPath: null,
                $"{current.SourceMethod} {current.SourcePath} expected status changed from {baseline.ExpectedStatus} to {current.ExpectedStatus}."));
        }

        CompareSchema(
            baseline.ExpectedResponseSchema,
            current.ExpectedResponseSchema,
            "$",
            current,
            findings);
    }

    private static void CompareSchema(
        ImportedSchema? baseline,
        ImportedSchema? current,
        string jsonPath,
        RegressionTestSnapshot snapshot,
        List<RegressionFinding> findings)
    {
        if (baseline is null && current is null)
        {
            return;
        }

        if (baseline is null || current is null)
        {
            findings.Add(Drift(
                snapshot,
                SchemaChanged,
                baseline is null ? RegressionFindingSeverity.Medium : RegressionFindingSeverity.High,
                DescribeSchema(baseline),
                DescribeSchema(current),
                jsonPath,
                baseline is null
                    ? $"{snapshot.SourceMethod} {snapshot.SourcePath} response schema was added."
                    : $"{snapshot.SourceMethod} {snapshot.SourcePath} response schema was removed."));
            return;
        }

        var baselineType = NormalizeType(baseline);
        var currentType = NormalizeType(current);
        if (baselineType != currentType)
        {
            findings.Add(Drift(
                snapshot,
                SchemaTypeChanged,
                RegressionFindingSeverity.High,
                baselineType.Length == 0 ? "none" : baselineType,
                currentType.Length == 0 ? "none" : currentType,
                jsonPath,
                $"{snapshot.SourceMethod} {snapshot.SourcePath} response type changed from '{FormatType(baselineType)}' to '{FormatType(currentType)}'."));
            return;
        }

        CompareRequired(baseline, current, jsonPath, snapshot, findings);
        CompareProperties(baseline, current, jsonPath, snapshot, findings);
        CompareEnum(baseline, current, jsonPath, snapshot, findings);
        CompareItems(baseline, current, jsonPath, snapshot, findings);
    }

    private static void CompareRequired(
        ImportedSchema baseline,
        ImportedSchema current,
        string jsonPath,
        RegressionTestSnapshot snapshot,
        List<RegressionFinding> findings)
    {
        var baselineRequired = ToSet(baseline.Required);
        var currentRequired = ToSet(current.Required);

        foreach (var name in baselineRequired.Except(currentRequired).OrderBy(value => value, StringComparer.Ordinal))
        {
            findings.Add(Drift(
                snapshot,
                SchemaRequiredRemoved,
                RegressionFindingSeverity.High,
                name,
                "removed",
                RequiredPath(jsonPath, name),
                $"{snapshot.SourceMethod} {snapshot.SourcePath} required property '{name}' was removed from the response schema."));
        }

        foreach (var name in currentRequired.Except(baselineRequired).OrderBy(value => value, StringComparer.Ordinal))
        {
            findings.Add(Drift(
                snapshot,
                SchemaRequiredAdded,
                RegressionFindingSeverity.Medium,
                "absent",
                name,
                RequiredPath(jsonPath, name),
                $"{snapshot.SourceMethod} {snapshot.SourcePath} required property '{name}' was added to the response schema."));
        }
    }

    private static void CompareProperties(
        ImportedSchema baseline,
        ImportedSchema current,
        string jsonPath,
        RegressionTestSnapshot snapshot,
        List<RegressionFinding> findings)
    {
        var baselineProperties = baseline.Properties;
        var currentProperties = current.Properties;

        foreach (var name in baselineProperties.Keys.Except(currentProperties.Keys, StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            findings.Add(Drift(
                snapshot,
                SchemaPropertyRemoved,
                RegressionFindingSeverity.High,
                name,
                "removed",
                ChildPath(jsonPath, name),
                $"{snapshot.SourceMethod} {snapshot.SourcePath} documented property '{name}' was removed from the response schema."));
        }

        foreach (var name in baselineProperties.Keys.Intersect(currentProperties.Keys, StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            CompareSchema(
                baselineProperties[name],
                currentProperties[name],
                ChildPath(jsonPath, name),
                snapshot,
                findings);
        }
    }

    private static void CompareEnum(
        ImportedSchema baseline,
        ImportedSchema current,
        string jsonPath,
        RegressionTestSnapshot snapshot,
        List<RegressionFinding> findings)
    {
        var baselineEnum = ToSet(baseline.Enum);
        var currentEnum = ToSet(current.Enum);
        if (baselineEnum.SetEquals(currentEnum))
        {
            return;
        }

        var removed = baselineEnum.Except(currentEnum).Any();
        findings.Add(Drift(
            snapshot,
            SchemaEnumChanged,
            removed ? RegressionFindingSeverity.High : RegressionFindingSeverity.Medium,
            FormatSet(baselineEnum),
            FormatSet(currentEnum),
            jsonPath == "$" ? "$.enum" : $"{jsonPath}.enum",
            $"{snapshot.SourceMethod} {snapshot.SourcePath} enum values changed from [{FormatSet(baselineEnum)}] to [{FormatSet(currentEnum)}]."));
    }

    private static void CompareItems(
        ImportedSchema baseline,
        ImportedSchema current,
        string jsonPath,
        RegressionTestSnapshot snapshot,
        List<RegressionFinding> findings)
    {
        if (baseline.Items is null && current.Items is null)
        {
            return;
        }

        var itemsPath = jsonPath == "$" ? "$.items" : $"{jsonPath}.items";
        if (baseline.Items is null || current.Items is null || !SchemaEquals(baseline.Items, current.Items))
        {
            findings.Add(Drift(
                snapshot,
                SchemaItemsChanged,
                RegressionFindingSeverity.High,
                DescribeSchema(baseline.Items),
                DescribeSchema(current.Items),
                itemsPath,
                $"{snapshot.SourceMethod} {snapshot.SourcePath} array item schema changed from '{DescribeSchema(baseline.Items)}' to '{DescribeSchema(current.Items)}'."));
        }

        if (baseline.Items is not null && current.Items is not null)
        {
            CompareSchema(baseline.Items, current.Items, itemsPath, snapshot, findings);
        }
    }

    private static void AddRegressionFinding(
        RegressionTestSnapshot baseline,
        RegressionTestSnapshot current,
        List<RegressionFinding> findings)
    {
        _ = baseline;
        if (current.Outcome is ContractValidationOutcome.Failed)
        {
            findings.Add(new RegressionFinding(
                RegressionFindingCategory.Regression,
                ValidationRegressed,
                RegressionFindingSeverity.High,
                current.Kind,
                current.SourceMethod,
                current.SourcePath,
                nameof(ContractValidationOutcome.Passed),
                nameof(ContractValidationOutcome.Failed),
                jsonPath: null,
                $"{current.SourceMethod} {current.SourcePath} passed on the last baseline and now fails contract validation."));
        }
        else if (current.Outcome is ContractValidationOutcome.Error)
        {
            findings.Add(new RegressionFinding(
                RegressionFindingCategory.Regression,
                ExecutionRegressed,
                RegressionFindingSeverity.High,
                current.Kind,
                current.SourceMethod,
                current.SourcePath,
                nameof(ContractValidationOutcome.Passed),
                nameof(ContractValidationOutcome.Error),
                jsonPath: null,
                $"{current.SourceMethod} {current.SourcePath} passed on the last baseline and now fails with an execution error."));
        }
    }

    private static void AddExecutionFailuresWithoutBaseline(
        IReadOnlyDictionary<TestIdentity, RegressionTestSnapshot> baselineById,
        IReadOnlyDictionary<TestIdentity, RegressionTestSnapshot> currentById,
        List<RegressionFinding> findings)
    {
        foreach (var (id, current) in currentById)
        {
            if (baselineById.ContainsKey(id) || current.Outcome is not ContractValidationOutcome.Error)
            {
                continue;
            }

            findings.Add(new RegressionFinding(
                RegressionFindingCategory.ExecutionFailure,
                ExecutionError,
                RegressionFindingSeverity.Medium,
                current.Kind,
                current.SourceMethod,
                current.SourcePath,
                "none",
                nameof(ContractValidationOutcome.Error),
                jsonPath: null,
                $"{current.SourceMethod} {current.SourcePath} failed with an execution error and has no prior passing baseline."));
        }
    }

    private static RegressionDecision Decide(
        IReadOnlyList<RegressionTestSnapshot> current,
        IReadOnlyList<RegressionFinding> findings)
    {
        var allPassed = current.All(test => test.Outcome is ContractValidationOutcome.Passed);
        var hasRegression = findings.Any(finding => finding.Category is RegressionFindingCategory.Regression);
        var hasHighDriftWhileNotKnownGood = !allPassed &&
            findings.Any(finding =>
                finding.Category is RegressionFindingCategory.ContractDrift &&
                finding.Severity is RegressionFindingSeverity.High);

        if (hasRegression || hasHighDriftWhileNotKnownGood)
        {
            return RegressionDecision.Block;
        }

        if (!allPassed || findings.Count > 0)
        {
            return RegressionDecision.Review;
        }

        return RegressionDecision.Safe;
    }

    private static RegressionFinding Drift(
        RegressionTestSnapshot snapshot,
        string code,
        RegressionFindingSeverity severity,
        string expected,
        string current,
        string jsonPath,
        string message) =>
        new(
            RegressionFindingCategory.ContractDrift,
            code,
            severity,
            snapshot.Kind,
            snapshot.SourceMethod,
            snapshot.SourcePath,
            expected,
            current,
            jsonPath,
            message);

    private static bool SchemaEquals(ImportedSchema left, ImportedSchema right)
    {
        if (NormalizeType(left) != NormalizeType(right))
        {
            return false;
        }

        if (!ToSet(left.Required).SetEquals(ToSet(right.Required)))
        {
            return false;
        }

        if (!ToSet(left.Enum).SetEquals(ToSet(right.Enum)))
        {
            return false;
        }

        if (left.Properties.Count != right.Properties.Count)
        {
            return false;
        }

        foreach (var property in left.Properties)
        {
            if (!right.Properties.TryGetValue(property.Key, out var other) ||
                !SchemaEquals(property.Value, other))
            {
                return false;
            }
        }

        if (left.Items is null && right.Items is null)
        {
            return true;
        }

        return left.Items is not null && right.Items is not null && SchemaEquals(left.Items, right.Items);
    }

    private static string NormalizeType(ImportedSchema schema)
    {
        var type = schema.Type?.Trim().ToLowerInvariant() ?? "";
        if (type.Length > 0)
        {
            return type;
        }

        if (schema.Properties.Count > 0 || schema.Required.Count > 0)
        {
            return "object";
        }

        if (schema.Items is not null)
        {
            return "array";
        }

        return "";
    }

    private static string FormatType(string type) => type.Length == 0 ? "none" : type;

    private static string DescribeSchema(ImportedSchema? schema)
    {
        if (schema is null)
        {
            return "none";
        }

        var type = NormalizeType(schema);
        return type.Length == 0 ? "schema" : type;
    }

    private static HashSet<string> ToSet(IReadOnlyList<string> values) =>
        values.ToHashSet(StringComparer.Ordinal);

    private static string FormatSet(IReadOnlyCollection<string> values) =>
        string.Join(", ", values.OrderBy(value => value, StringComparer.Ordinal));

    private static string ChildPath(string jsonPath, string name) =>
        jsonPath == "$" ? $"$.{name}" : $"{jsonPath}.{name}";

    private static string RequiredPath(string jsonPath, string name) =>
        jsonPath == "$" ? $"$.{name}" : $"{jsonPath}.{name}";

    private static TestIdentity Identity(RegressionTestSnapshot snapshot) =>
        new(
            OpenApiSpecKey.ComparisonKey(snapshot.SpecKey),
            snapshot.Kind,
            snapshot.SourceMethod,
            snapshot.SourcePath,
            NormalizeScenarioKey(snapshot.ScenarioKey));

    private static string NormalizeScenarioKey(string? scenarioKey) =>
        string.IsNullOrWhiteSpace(scenarioKey) ? "" : scenarioKey.Trim();

    private static OperationIdentity Operation(RegressionTestSnapshot snapshot) =>
        new(
            OpenApiSpecKey.ComparisonKey(snapshot.SpecKey),
            snapshot.SourceMethod,
            snapshot.SourcePath);

    private static int CompareFindings(RegressionFinding left, RegressionFinding right)
    {
        var path = string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        if (path != 0)
        {
            return path;
        }

        var method = string.Compare(left.Method, right.Method, StringComparison.Ordinal);
        if (method != 0)
        {
            return method;
        }

        var kind = string.Compare(left.Kind?.ToString(), right.Kind?.ToString(), StringComparison.Ordinal);
        if (kind != 0)
        {
            return kind;
        }

        var code = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
        if (code != 0)
        {
            return code;
        }

        var jsonPath = string.Compare(left.JsonPath, right.JsonPath, StringComparison.Ordinal);
        if (jsonPath != 0)
        {
            return jsonPath;
        }

        return string.Compare(left.Message, right.Message, StringComparison.Ordinal);
    }

    private readonly record struct TestIdentity(
        string SpecKey,
        GeneratedApiTestKind Kind,
        string SourceMethod,
        string SourcePath,
        string ScenarioKey)
    {
        public OperationIdentity Operation => new(SpecKey, SourceMethod, SourcePath);
    }

    private readonly record struct OperationIdentity(string SpecKey, string SourceMethod, string SourcePath);
}
