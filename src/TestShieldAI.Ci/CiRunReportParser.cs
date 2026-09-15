using System.Text.Json;

namespace TestShieldAI.Ci;

public static class CiRunReportParser
{
    public static bool TryRead(string? json, out CiRunReport report, out string? error)
    {
        report = CiRunReport.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The TestShield response body was empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                error = "The TestShield response was not a JSON object.";
                return false;
            }

            report = Read(document.RootElement);
            error = null;
            return true;
        }
        catch (JsonException)
        {
            error = "The TestShield response was not valid JSON.";
            return false;
        }
    }

    private static CiRunReport Read(JsonElement root)
    {
        var findings = ReadFindings(root);
        var (passed, failed, errors) = ReadResultCounts(root);
        var reasons = ReadStringList(Property(root, "risk"), "reasons");
        return new CiRunReport
        {
            Decision = ReadString(root, "decision"),
            RiskLevel = ReadString(Property(root, "risk"), "level"),
            RiskTitle = ReadString(Property(root, "risk"), "title"),
            RiskSummary = ReadString(Property(root, "risk"), "summary"),
            RiskReasons = reasons,
            CoveragePercent = ReadDecimal(Property(root, "coverage"), "coveragePercent"),
            FindingCount = findings.Count,
            Findings = findings,
            PassedCount = passed,
            FailedCount = failed,
            ErrorCount = errors
        };
    }

    private static IReadOnlyList<CiRunFinding> ReadFindings(JsonElement root)
    {
        if (!TryGet(root, "findings", out var findings) || findings.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<CiRunFinding>();
        foreach (var item in findings.EnumerateArray())
        {
            if (item.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }

            list.Add(new CiRunFinding
            {
                Category = ReadString(item, "category"),
                Message = FirstNonEmpty(ReadString(item, "message"), ReadString(item, "code"))
            });
        }

        return list;
    }

    private static (int Passed, int Failed, int Errors) ReadResultCounts(JsonElement root)
    {
        if (!TryGet(root, "results", out var results) || results.ValueKind is not JsonValueKind.Array)
        {
            return (0, 0, 0);
        }

        var passed = 0;
        var failed = 0;
        var errors = 0;
        foreach (var item in results.EnumerateArray())
        {
            var outcome = ReadString(Property(item, "validation"), "outcome");
            if (outcome.Equals("Passed", StringComparison.OrdinalIgnoreCase))
            {
                passed++;
            }
            else if (outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                failed++;
            }
            else if (outcome.Equals("Error", StringComparison.OrdinalIgnoreCase))
            {
                errors++;
            }
        }

        return (passed, failed, errors);
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement? parent, string name)
    {
        if (parent is null ||
            !TryGet(parent.Value, name, out var values) ||
            values.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<string>();
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    list.Add(text.Trim());
                }
            }
        }

        return list;
    }

    private static JsonElement? Property(JsonElement parent, string name) =>
        TryGet(parent, name, out var value) && value.ValueKind is JsonValueKind.Object ? value : null;

    private static string ReadString(JsonElement? parent, string name)
    {
        if (parent is null || !TryGet(parent.Value, name, out var value))
        {
            return "";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? "",
            JsonValueKind.Null => "",
            _ => value.ToString()
        };
    }

    private static decimal? ReadDecimal(JsonElement? parent, string name)
    {
        if (parent is null || !TryGet(parent.Value, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }

        if (element.ValueKind is JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}
