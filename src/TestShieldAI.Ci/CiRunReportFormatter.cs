using System.Globalization;
using System.Text;

namespace TestShieldAI.Ci;

public static class CiRunReportFormatter
{
    public static string Format(CiRunReport? report)
    {
        report ??= CiRunReport.Empty;
        var builder = new StringBuilder();
        builder.AppendLine("TestShield AI Regression Result");
        builder.AppendLine($"Decision: {DisplayDecision(report.Decision)}");
        builder.AppendLine($"Risk: {DisplayRisk(report)}");
        builder.AppendLine($"Coverage: {DisplayCoverage(report.CoveragePercent)}");
        builder.AppendLine($"Findings: {report.FindingCount.ToString(CultureInfo.InvariantCulture)}");
        if (report.PassedCount + report.FailedCount + report.ErrorCount > 0)
        {
            builder.AppendLine(
                $"Results: {report.PassedCount.ToString(CultureInfo.InvariantCulture)} passed, {report.FailedCount.ToString(CultureInfo.InvariantCulture)} failed, {report.ErrorCount.ToString(CultureInfo.InvariantCulture)} errors");
        }

        var details = DetailLines(report);
        if (details.Count > 0)
        {
            builder.AppendLine();
            foreach (var line in details)
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string FindingTag(string? category) =>
        category?.Trim() switch
        {
            var value when value is not null &&
                           value.Equals("ContractDrift", StringComparison.OrdinalIgnoreCase) => "DRIFT",
            var value when value is not null &&
                           value.Equals("Regression", StringComparison.OrdinalIgnoreCase) => "BLOCK",
            var value when value is not null &&
                           value.Equals("ExecutionFailure", StringComparison.OrdinalIgnoreCase) => "EXEC",
            _ => "INFO"
        };

    private static IReadOnlyList<string> DetailLines(CiRunReport report)
    {
        var lines = new List<string>();
        if (report.RiskReasons.Count > 0)
        {
            for (var index = 0; index < report.RiskReasons.Count; index++)
            {
                var tag = index < report.Findings.Count
                    ? FindingTag(report.Findings[index].Category)
                    : "INFO";
                lines.Add($"[{tag}] {report.RiskReasons[index]}");
            }

            return lines;
        }

        foreach (var finding in report.Findings)
        {
            if (string.IsNullOrWhiteSpace(finding.Message))
            {
                continue;
            }

            lines.Add($"[{FindingTag(finding.Category)}] {finding.Message}");
        }

        return lines;
    }

    private static string DisplayDecision(string decision) =>
        string.IsNullOrWhiteSpace(decision) ? "UNKNOWN" : decision.Trim().ToUpperInvariant();

    private static string DisplayRisk(CiRunReport report)
    {
        if (string.IsNullOrWhiteSpace(report.RiskLevel) && string.IsNullOrWhiteSpace(report.RiskTitle))
        {
            return "unavailable";
        }

        if (string.IsNullOrWhiteSpace(report.RiskTitle))
        {
            return report.RiskLevel;
        }

        if (string.IsNullOrWhiteSpace(report.RiskLevel))
        {
            return report.RiskTitle;
        }

        return $"{report.RiskLevel} - {report.RiskTitle}";
    }

    private static string DisplayCoverage(decimal? percent)
    {
        if (percent is null)
        {
            return "unavailable";
        }

        var value = percent.Value;
        var text = value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{text}%";
    }
}
