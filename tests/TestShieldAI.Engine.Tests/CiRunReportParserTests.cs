using TestShieldAI.Ci;

namespace TestShieldAI.Engine.Tests;

public class CiRunReportParserTests
{
    [Fact]
    public void TryRead_BlockPayload_ReadsDecisionRiskCoverageFindingsAndCounts()
    {
        const string json = """
            {
              "decision": "Block",
              "risk": {
                "level": "High",
                "title": "Release should be blocked",
                "summary": "Release should be blocked because regressions were detected.",
                "reasons": [
                  "Previously passing validation now fails for GET /customers/{id}.",
                  "Response schema changed for GET /orders/{id}."
                ]
              },
              "coverage": { "coveragePercent": 80, "uncoveredOperations": 2 },
              "findings": [
                { "category": "Regression", "message": "Previously passing validation now fails for GET /customers/{id}." },
                { "category": "ContractDrift", "message": "Response schema changed for GET /orders/{id}." }
              ],
              "results": [
                { "validation": { "outcome": "Passed" } },
                { "validation": { "outcome": "Failed" } },
                { "validation": { "outcome": "Error" } }
              ]
            }
            """;

        Assert.True(CiRunReportParser.TryRead(json, out var report, out var error));
        Assert.Null(error);
        Assert.Equal("Block", report.Decision);
        Assert.Equal("High", report.RiskLevel);
        Assert.Equal("Release should be blocked", report.RiskTitle);
        Assert.Equal(80m, report.CoveragePercent);
        Assert.Equal(2, report.FindingCount);
        Assert.Equal(1, report.PassedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.ErrorCount);
        Assert.Equal(CiRunExitCode.Failure, CiRunExitCode.ForRun(200, report.Decision));
    }

    [Fact]
    public void TryRead_MissingFields_DoesNotThrow()
    {
        Assert.True(CiRunReportParser.TryRead("{}", out var report, out var error));
        Assert.Null(error);
        Assert.Equal("", report.Decision);
        Assert.Equal("", report.RiskLevel);
        Assert.Null(report.CoveragePercent);
        Assert.Empty(report.Findings);
        Assert.Empty(report.RiskReasons);
        Assert.Equal(0, report.FindingCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("[]")]
    public void TryRead_MalformedOrUnexpectedJson_FailsSafely(string? json)
    {
        Assert.False(CiRunReportParser.TryRead(json, out var report, out var error));
        Assert.NotNull(error);
        Assert.Equal(CiRunReport.Empty, report);
        Assert.DoesNotContain("Bearer", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_PrintsDecisionRiskCoverageAndFindings()
    {
        Assert.True(CiRunReportParser.TryRead(
            """
            {
              "decision": "Block",
              "risk": {
                "level": "High",
                "title": "Release should be blocked",
                "reasons": [
                  "Previously passing validation now fails for GET /customers/{id}.",
                  "Response schema changed for GET /orders/{id}."
                ]
              },
              "coverage": { "coveragePercent": 80 },
              "findings": [
                { "category": "Regression", "message": "validation" },
                { "category": "ContractDrift", "message": "schema" }
              ]
            }
            """,
            out var report,
            out _));

        var text = CiRunReportFormatter.Format(report);
        Assert.Contains("TestShield AI Regression Result", text, StringComparison.Ordinal);
        Assert.Contains("Decision: BLOCK", text, StringComparison.Ordinal);
        Assert.Contains("Risk: High - Release should be blocked", text, StringComparison.Ordinal);
        Assert.Contains("Coverage: 80%", text, StringComparison.Ordinal);
        Assert.Contains("Findings: 2", text, StringComparison.Ordinal);
        Assert.Contains("[BLOCK] Previously passing validation now fails for GET /customers/{id}.", text, StringComparison.Ordinal);
        Assert.Contains("[DRIFT] Response schema changed for GET /orders/{id}.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NullReport_DoesNotCrash()
    {
        var text = CiRunReportFormatter.Format(null);
        Assert.Contains("Decision: UNKNOWN", text, StringComparison.Ordinal);
        Assert.Contains("Coverage: unavailable", text, StringComparison.Ordinal);
        Assert.Contains("Risk: unavailable", text, StringComparison.Ordinal);
    }
}
