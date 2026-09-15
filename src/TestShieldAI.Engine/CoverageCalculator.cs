namespace TestShieldAI.Engine;

public sealed class CoverageCalculator : ICoverageCalculator
{
    public const string NoDeterministicTest = "NO_DETERMINISTIC_TEST";

    public CoverageCalculationResult Calculate(
        IReadOnlyList<ImportedOperation> operations,
        IReadOnlyList<GeneratedApiTestCase> generatedTests)
    {
        operations ??= [];
        generatedTests ??= [];

        var indexed = new Dictionary<OperationIdentity, ImportedOperation>();
        foreach (var operation in operations)
        {
            indexed.TryAdd(Identity(operation), operation);
        }

        var deterministicallyCovered = new HashSet<OperationIdentity>();
        var aiCovered = new HashSet<OperationIdentity>();
        var happyPath = 0;
        var negative = 0;
        var aiPositive = 0;
        var aiNegative = 0;
        var aiEdge = 0;

        foreach (var test in generatedTests)
        {
            switch (test.Kind)
            {
                case GeneratedApiTestKind.HappyPath:
                    happyPath++;
                    break;
                case GeneratedApiTestKind.NegativeMissingRequiredBody:
                    negative++;
                    break;
                case GeneratedApiTestKind.AiPositive:
                    aiPositive++;
                    break;
                case GeneratedApiTestKind.AiNegative:
                    aiNegative++;
                    break;
                case GeneratedApiTestKind.AiEdge:
                    aiEdge++;
                    break;
            }

            var identity = Identity(test);
            if (!indexed.ContainsKey(identity))
            {
                continue;
            }

            if (IsDeterministic(test.Kind))
            {
                deterministicallyCovered.Add(identity);
            }
            else if (IsAi(test.Kind))
            {
                aiCovered.Add(identity);
            }
        }

        var gaps = indexed
            .Where(pair => !deterministicallyCovered.Contains(pair.Key))
            .Select(pair => new CoverageGap(
                OpenApiSpecKey.CanonicalDisplay(pair.Value.SpecKey),
                pair.Value.Method,
                pair.Value.Path,
                NoDeterministicTest))
            .OrderBy(gap => gap.SpecKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(gap => gap.Method, StringComparer.Ordinal)
            .ThenBy(gap => gap.Path, StringComparer.Ordinal)
            .ToList();

        var total = indexed.Count;
        var covered = deterministicallyCovered.Count;
        return new CoverageCalculationResult(
            total,
            covered,
            gaps.Count,
            Percent(covered, total),
            aiCovered.Count,
            new CoverageScenarioCounts(happyPath, negative, aiPositive, aiNegative, aiEdge),
            gaps);
    }

    public static bool IsDeterministic(GeneratedApiTestKind kind) =>
        kind is GeneratedApiTestKind.HappyPath or GeneratedApiTestKind.NegativeMissingRequiredBody;

    public static bool IsAi(GeneratedApiTestKind kind) =>
        kind is GeneratedApiTestKind.AiPositive or GeneratedApiTestKind.AiNegative or GeneratedApiTestKind.AiEdge;

    private static decimal Percent(int covered, int total) =>
        total == 0
            ? 100m
            : decimal.Round(covered * 100m / total, 2, MidpointRounding.AwayFromZero);

    private static OperationIdentity Identity(ImportedOperation operation) =>
        new(
            OpenApiSpecKey.ComparisonKey(operation.SpecKey),
            NormalizeMethod(operation.Method),
            operation.Path);

    private static OperationIdentity Identity(GeneratedApiTestCase test) =>
        new(
            OpenApiSpecKey.ComparisonKey(test.SpecKey),
            NormalizeMethod(test.SourceMethod),
            test.SourcePath);

    private static string NormalizeMethod(string? method) =>
        string.IsNullOrWhiteSpace(method) ? "" : method.Trim().ToUpperInvariant();

    private readonly record struct OperationIdentity(string SpecKey, string Method, string Path);
}
