using TestShieldAI.Engine;

namespace TestShieldAI.Api.TestsRun;

public static class TestsRunRegressionSelection
{
    public static bool IsDeterministic(GeneratedApiTestKind kind) =>
        kind is GeneratedApiTestKind.HappyPath or GeneratedApiTestKind.NegativeMissingRequiredBody;

    public static bool IsAi(GeneratedApiTestKind kind) =>
        kind is GeneratedApiTestKind.AiPositive or GeneratedApiTestKind.AiNegative or GeneratedApiTestKind.AiEdge;

    public static (IReadOnlyList<RegressionTestSnapshot> Baseline, IReadOnlyList<RegressionTestSnapshot> Current)
        SelectComparable(
            IReadOnlyList<RegressionTestSnapshot> baseline,
            IReadOnlyList<RegressionTestSnapshot> current,
            bool aiGenerationAvailable)
    {
        baseline ??= [];
        current ??= [];

        if (!aiGenerationAvailable)
        {
            return (Deterministic(baseline), Deterministic(current));
        }

        var comparableKeys = BaselineAiKeys(baseline)
            .Intersect(CurrentAiKeys(current), StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        return (
            baseline.Where(snapshot => Include(snapshot, comparableKeys)).ToList(),
            current.Where(snapshot => Include(snapshot, comparableKeys)).ToList());
    }

    public static IReadOnlyList<RegressionTestSnapshot> SnapshotsToPromote(
        IReadOnlyList<RegressionTestSnapshot> current)
    {
        current ??= [];
        return current
            .Where(snapshot =>
                IsDeterministic(snapshot.Kind) ||
                (IsAi(snapshot.Kind) && snapshot.Outcome is ContractValidationOutcome.Passed))
            .ToList();
    }

    public static bool ShouldPromote(IReadOnlyList<RegressionTestSnapshot> current) =>
        Deterministic(current).All(snapshot => snapshot.Outcome is ContractValidationOutcome.Passed);

    private static bool Include(RegressionTestSnapshot snapshot, HashSet<string> comparableAiKeys) =>
        IsDeterministic(snapshot.Kind) ||
        (IsAi(snapshot.Kind) && comparableAiKeys.Contains(AiKey(snapshot)));

    private static IReadOnlyList<RegressionTestSnapshot> Deterministic(
        IReadOnlyList<RegressionTestSnapshot> snapshots) =>
        snapshots.Where(snapshot => IsDeterministic(snapshot.Kind)).ToList();

    private static IEnumerable<string> BaselineAiKeys(IReadOnlyList<RegressionTestSnapshot> snapshots) =>
        snapshots
            .Where(snapshot => IsAi(snapshot.Kind) && NormalizeKey(snapshot.ScenarioKey).Length > 0)
            .Select(AiKey);

    private static IEnumerable<string> CurrentAiKeys(IReadOnlyList<RegressionTestSnapshot> snapshots) =>
        snapshots
            .Where(snapshot => IsAi(snapshot.Kind) && NormalizeKey(snapshot.ScenarioKey).Length > 0)
            .Select(AiKey);

    private static string AiKey(RegressionTestSnapshot snapshot) =>
        $"{OpenApiSpecKey.ComparisonKey(snapshot.SpecKey)}\u001f{NormalizeKey(snapshot.ScenarioKey)}";

    private static string NormalizeKey(string? scenarioKey) =>
        string.IsNullOrWhiteSpace(scenarioKey) ? "" : scenarioKey.Trim();
}
