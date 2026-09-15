using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class OpenApiIngestorTests
{
    private const string ValidOpenApi3Spec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{id}": {
              "get": {
                "parameters": [
                  {
                    "name": "id",
                    "in": "path",
                    "required": true,
                    "schema": { "type": "integer" }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "A pet",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "required": ["id", "name"],
                          "properties": {
                            "id": { "type": "integer" },
                            "name": { "type": "string" }
                          }
                        }
                      }
                    }
                  },
                  "204": {
                    "description": "No content"
                  }
                }
              }
            },
            "/pets": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["name"],
                        "properties": {
                          "name": { "type": "string" }
                        }
                      }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "id": { "type": "integer" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private const string OpenApi3WithRefsSpec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Refs", "version": "1.0.0" },
          "paths": {
            "/items/{id}": {
              "put": {
                "parameters": [
                  { "$ref": "#/components/parameters/Id" }
                ],
                "requestBody": {
                  "$ref": "#/components/requestBodies/ItemBody"
                },
                "responses": {
                  "200": {
                    "$ref": "#/components/responses/ItemResponse"
                  },
                  "204": {
                    "description": "No content"
                  }
                }
              }
            }
          },
          "components": {
            "parameters": {
              "Id": {
                "name": "id",
                "in": "path",
                "required": true,
                "schema": { "$ref": "#/components/schemas/ItemId" }
              }
            },
            "requestBodies": {
              "ItemBody": {
                "required": true,
                "content": {
                  "application/json": {
                    "schema": { "$ref": "#/components/schemas/Item" }
                  }
                }
              }
            },
            "responses": {
              "ItemResponse": {
                "description": "Item",
                "content": {
                  "application/json": {
                    "schema": { "$ref": "#/components/schemas/Item" }
                  }
                }
              }
            },
            "schemas": {
              "ItemId": { "type": "integer" },
              "Item": {
                "type": "object",
                "required": ["name"],
                "properties": {
                  "name": { "type": "string" }
                }
              }
            }
          }
        }
        """;

    private const string Swagger2Spec = """
        {
          "swagger": "2.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{id}": {
              "get": {
                "parameters": [
                  {
                    "name": "id",
                    "in": "path",
                    "required": true,
                    "type": "integer"
                  }
                ],
                "responses": {
                  "200": {
                    "description": "A pet",
                    "schema": {
                      "type": "object",
                      "properties": {
                        "id": { "type": "integer" }
                      }
                    }
                  }
                }
              }
            },
            "/pets": {
              "post": {
                "parameters": [
                  {
                    "in": "body",
                    "name": "body",
                    "required": true,
                    "schema": {
                      "type": "object",
                      "required": ["name"],
                      "properties": {
                        "name": { "type": "string" }
                      }
                    }
                  }
                ],
                "responses": {
                  "201": {
                    "description": "Created"
                  }
                }
              }
            }
          }
        }
        """;

    private const string EmptyPathsSpec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Empty", "version": "1.0.0" },
          "paths": {}
        }
        """;

    private const string DiagnosticErrorSpec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Bad", "version": "1.0.0" },
          "paths": {
            "/broken": {
              "get": {
                "responses": {
                  "200": {
                    "$ref": "#/components/responses/DoesNotExist"
                  }
                }
              }
            }
          }
        }
        """;

    private readonly IOpenApiIngestor _ingestor = new OpenApiIngestor();

    [Fact]
    public void Import_ValidOpenApi3_ExtractsAtLeastTwoOperations()
    {
        var result = _ingestor.Import(ValidOpenApi3Spec);

        Assert.Equal(2, result.Operations.Count);
        Assert.Equal("Pets", result.Title);
        Assert.Equal("1.0.0", result.Version);
        Assert.Equal("Pets", result.SpecKey);
        Assert.All(result.Operations, operation => Assert.Equal("Pets", operation.SpecKey));
    }

    [Fact]
    public void Import_ValidOpenApi3_ExtractsHttpMethodAndPath()
    {
        var result = _ingestor.Import(ValidOpenApi3Spec);

        Assert.Contains(result.Operations, operation => operation.Method == "GET" && operation.Path == "/pets/{id}");
        Assert.Contains(result.Operations, operation => operation.Method == "POST" && operation.Path == "/pets");
    }

    [Fact]
    public void Import_ValidOpenApi3_ExtractsParameters()
    {
        var result = _ingestor.Import(ValidOpenApi3Spec);
        var getPet = Assert.Single(result.Operations, operation => operation.Method == "GET");
        var id = Assert.Single(getPet.Parameters);

        Assert.Equal("id", id.Name);
        Assert.Equal("path", id.Location);
        Assert.True(id.Required);
        Assert.Equal("integer", id.Schema?.Type);
    }

    [Fact]
    public void Import_ValidOpenApi3_ExtractsSchemasWherePresent()
    {
        var result = _ingestor.Import(ValidOpenApi3Spec);

        var getPet = Assert.Single(result.Operations, operation => operation.Method == "GET");
        Assert.True(getPet.Responses.ContainsKey("200"));
        var petSchema = getPet.Responses["200"];
        Assert.NotNull(petSchema);
        Assert.Equal("object", petSchema.Type);
        Assert.Contains("id", petSchema.Required);
        Assert.Contains("name", petSchema.Properties.Keys);

        var createPet = Assert.Single(result.Operations, operation => operation.Method == "POST");
        Assert.NotNull(createPet.RequestBody);
        Assert.Equal("object", createPet.RequestBody.Type);
        Assert.Contains("name", createPet.RequestBody.Required);
        Assert.True(createPet.Responses.ContainsKey("201"));
        var createdSchema = createPet.Responses["201"];
        Assert.NotNull(createdSchema);
        Assert.Equal("integer", createdSchema.Properties["id"].Type);
    }

    [Fact]
    public void Import_ValidOpenApi3_PreservesStatusCodesWithoutSchema()
    {
        var result = _ingestor.Import(ValidOpenApi3Spec);
        var getPet = Assert.Single(result.Operations, operation => operation.Method == "GET");

        Assert.True(getPet.Responses.ContainsKey("204"));
        Assert.Null(getPet.Responses["204"]);
    }

    [Fact]
    public void Import_OpenApi3WithRefs_ResolvesReferencedParameter()
    {
        var result = _ingestor.Import(OpenApi3WithRefsSpec);
        var put = Assert.Single(result.Operations);
        var id = Assert.Single(put.Parameters);

        Assert.Equal("id", id.Name);
        Assert.Equal("path", id.Location);
        Assert.True(id.Required);
        Assert.Equal("integer", id.Schema?.Type);
    }

    [Fact]
    public void Import_OpenApi3WithRefs_ResolvesReferencedSchemaOnRequestBody()
    {
        var result = _ingestor.Import(OpenApi3WithRefsSpec);
        var put = Assert.Single(result.Operations);

        Assert.NotNull(put.RequestBody);
        Assert.Equal("object", put.RequestBody.Type);
        Assert.Contains("name", put.RequestBody.Required);
        Assert.Equal("string", put.RequestBody.Properties["name"].Type);
    }

    [Fact]
    public void Import_OpenApi3WithRefs_ResolvesReferencedResponse()
    {
        var result = _ingestor.Import(OpenApi3WithRefsSpec);
        var put = Assert.Single(result.Operations);

        Assert.True(put.Responses.ContainsKey("200"));
        var schema = put.Responses["200"];
        Assert.NotNull(schema);
        Assert.Equal("object", schema.Type);
        Assert.Contains("name", schema.Required);
    }

    [Fact]
    public void Import_OpenApi3WithRefs_ResolvesReferencedSchema()
    {
        var result = _ingestor.Import(OpenApi3WithRefsSpec);
        var put = Assert.Single(result.Operations);

        Assert.Equal("integer", put.Parameters[0].Schema?.Type);
        Assert.Equal("object", put.RequestBody?.Type);
        Assert.Equal("object", put.Responses["200"]?.Type);
    }

    [Fact]
    public void Import_Swagger2_ExtractsOperations()
    {
        var result = _ingestor.Import(Swagger2Spec);

        Assert.Equal(2, result.Operations.Count);
        Assert.Contains(result.Operations, operation => operation.Method == "GET" && operation.Path == "/pets/{id}");
        Assert.Contains(result.Operations, operation => operation.Method == "POST" && operation.Path == "/pets");

        var getPet = Assert.Single(result.Operations, operation => operation.Method == "GET");
        var id = Assert.Single(getPet.Parameters);
        Assert.Equal("id", id.Name);
        Assert.Equal("path", id.Location);
    }

    [Fact]
    public void Import_EmptyOrWhitespaceSpec_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _ingestor.Import(null!));
        Assert.Throws<InvalidOperationException>(() => _ingestor.Import(""));
        Assert.Throws<InvalidOperationException>(() => _ingestor.Import("   "));
    }

    [Fact]
    public void Import_EmptyPaths_ReturnsNoOperations()
    {
        var result = _ingestor.Import(EmptyPathsSpec);

        Assert.Empty(result.Operations);
        Assert.Equal("Empty", result.SpecKey);
    }

    [Fact]
    public void Import_WhitespaceTitle_NormalizesSpecKey()
    {
        var spec = ValidOpenApi3Spec.Replace("\"Pets\"", "\"  Customer   API  \"", StringComparison.Ordinal);

        var result = _ingestor.Import(spec);

        Assert.Equal("Customer API", result.SpecKey);
        Assert.All(result.Operations, operation => Assert.Equal("Customer API", operation.SpecKey));
    }

    [Fact]
    public void Import_BlankTitle_UsesUntitledFallback()
    {
        var spec = ValidOpenApi3Spec.Replace("\"Pets\"", "\"   \"", StringComparison.Ordinal);

        var result = _ingestor.Import(spec);

        Assert.Equal(OpenApiSpecKey.UntitledFallback, result.SpecKey);
        Assert.All(result.Operations, operation => Assert.Equal(OpenApiSpecKey.UntitledFallback, operation.SpecKey));
    }

    [Fact]
    public void Import_VersionChange_DoesNotChangeSpecKey()
    {
        var v2 = ValidOpenApi3Spec.Replace("\"1.0.0\"", "\"2.0.0\"", StringComparison.Ordinal);

        var first = _ingestor.Import(ValidOpenApi3Spec);
        var second = _ingestor.Import(v2);

        Assert.Equal("1.0.0", first.Version);
        Assert.Equal("2.0.0", second.Version);
        Assert.Equal(first.SpecKey, second.SpecKey);
        Assert.Equal("Pets", second.SpecKey);
    }

    [Fact]
    public void Import_InvalidSpec_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => _ingestor.Import("this is not a valid OpenAPI document"));
    }

    [Fact]
    public void Import_ParserDiagnosticErrors_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => _ingestor.Import(DiagnosticErrorSpec));

        Assert.Contains("could not be parsed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
