using TestShieldAI.Api.OpenApi;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class CurrentOpenApiSpecificationSelectorTests
{
    private readonly IOpenApiIngestor _ingestor = new OpenApiIngestor();

    [Fact]
    public void SelectLatestPerSpecKey_PicksNewestRowForEachTitle()
    {
        var projectId = Guid.NewGuid();
        var olderPets = Record(projectId, Spec("Pets", "1.0.0", "/legacy"), importedAt: Utc(1), id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var orders = Record(projectId, Spec("Order", "1.0.0", "/orders"), importedAt: Utc(2), id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var newestPets = Record(projectId, Spec("Pets", "2.0.0", "/pets"), importedAt: Utc(3), id: Guid.Parse("00000000-0000-0000-0000-000000000003"));

        var selected = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey(
            [olderPets, orders, newestPets],
            _ingestor);

        Assert.Equal(2, selected.Count);
        var pets = Assert.Single(selected, item => item.SpecKey == "Pets");
        var order = Assert.Single(selected, item => item.SpecKey == "Order");
        Assert.Contains(pets.Import.Operations, operation => operation.Path == "/pets");
        Assert.DoesNotContain(pets.Import.Operations, operation => operation.Path == "/legacy");
        Assert.Contains(order.Import.Operations, operation => operation.Path == "/orders");
        Assert.Equal("2.0.0", pets.Import.Version);
        Assert.Equal(Utc(3), pets.ImportedAt);
    }

    [Fact]
    public void SelectLatestPerSpecKey_SameTitleDifferentCase_IsOneHistory()
    {
        var projectId = Guid.NewGuid();
        var older = Record(projectId, Spec("Pets", "1.0.0", "/legacy"), Utc(1));
        var newer = Record(projectId, Spec("pets", "1.1.0", "/pets"), Utc(2));

        var selected = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey([older, newer], _ingestor);

        var spec = Assert.Single(selected);
        Assert.Equal("pets", spec.SpecKey);
        Assert.DoesNotContain(spec.Import.Operations, operation => operation.Path == "/legacy");
        Assert.Contains(spec.Import.Operations, operation => operation.Path == "/pets");
    }

    [Fact]
    public void SelectLatestPerSpecKey_VersionChangeAlone_DoesNotCreateASecondCurrentSpec()
    {
        var projectId = Guid.NewGuid();
        var v1 = Record(projectId, Spec("Pets", "1.0.0", "/pets"), Utc(1));
        var v2 = Record(projectId, Spec("Pets", "2.0.0", "/pets"), Utc(2));

        var selected = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey([v1, v2], _ingestor);

        var spec = Assert.Single(selected);
        Assert.Equal("Pets", spec.SpecKey);
        Assert.Equal("2.0.0", spec.Import.Version);
    }

    [Fact]
    public void SelectLatestPerSpecKey_OlderRowForSameSpecKey_IsNotSelected()
    {
        var projectId = Guid.NewGuid();
        var older = Record(projectId, Spec("Pets", "1.0.0", "/legacy"), Utc(5));
        var newer = Record(projectId, Spec("Pets", "1.0.0", "/pets"), Utc(6));

        var selected = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey([newer, older], _ingestor);

        var spec = Assert.Single(selected);
        Assert.Equal("/pets", Assert.Single(spec.Import.Operations).Path);
    }

    [Fact]
    public void SelectLatestPerSpecKey_DifferentTitles_AreBothCurrent()
    {
        var projectId = Guid.NewGuid();
        var customer = Record(projectId, Spec("Customer", "1.0.0", "/pets"), Utc(1));
        var order = Record(projectId, Spec("Order", "1.0.0", "/pets"), Utc(2));

        var selected = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey([customer, order], _ingestor);

        Assert.Equal(2, selected.Count);
        Assert.All(selected, item => Assert.Equal("/pets", Assert.Single(item.Import.Operations).Path));
        Assert.Contains(selected, item => item.SpecKey == "Customer");
        Assert.Contains(selected, item => item.SpecKey == "Order");
    }

    [Fact]
    public void SelectLatestPerSpecKey_SkipsUnparseableHistoryRows()
    {
        var projectId = Guid.NewGuid();
        var valid = Record(projectId, Spec("Pets", "1.0.0", "/pets"), Utc(1));
        var invalid = Record(projectId, "not-open-api", Utc(2));

        var selected = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey([valid, invalid], _ingestor);

        var spec = Assert.Single(selected);
        Assert.Equal("Pets", spec.SpecKey);
    }

    private static OpenApiSpecificationRecord Record(
        Guid projectId,
        string spec,
        DateTimeOffset importedAt,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ProjectId = projectId,
            RawSpecification = spec,
            ImportedAt = importedAt
        };

    private static DateTimeOffset Utc(int seconds) =>
        new(2026, 9, 14, 12, 0, seconds, TimeSpan.Zero);

    private static string Spec(string title, string version, string path) =>
        $$"""
        {
          "openapi": "3.0.3",
          "info": { "title": "{{title}}", "version": "{{version}}" },
          "paths": {
            "{{path}}": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """;
}
