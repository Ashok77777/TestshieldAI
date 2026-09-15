using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class ApiContractValidatorTests
{
    private readonly IApiContractValidator _validator = new ApiContractValidator();

    [Fact]
    public void Validate_MatchingStatusAndValidObject_ReturnsPassed()
    {
        var schema = ObjectSchema(["id", "name"], new Dictionary<string, ImportedSchema>
        {
            ["id"] = IntegerSchema(),
            ["name"] = StringSchema()
        });
        var result = _validator.Validate(
            TestCase(200, schema),
            Execution(200, """{"id":1,"name":"a","extra":true}"""));

        Assert.Equal(ContractValidationOutcome.Passed, result.Outcome);
        Assert.True(result.StatusValid);
        Assert.True(result.SchemaValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Validate_WrongStatus_ReturnsFailedWithoutSchemaFailures()
    {
        var schema = ObjectSchema(["name"], new Dictionary<string, ImportedSchema>
        {
            ["name"] = StringSchema()
        });
        var result = _validator.Validate(
            TestCase(200, schema),
            Execution(201, """{"id":1}"""));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        Assert.False(result.StatusValid);
        Assert.Null(result.SchemaValid);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(ApiContractValidator.StatusMismatch, failure.Code);
        Assert.DoesNotContain(result.Failures, item => item.Code == ApiContractValidator.SchemaRequired);
        Assert.DoesNotContain(result.Failures, item => item.Code == ApiContractValidator.SchemaType);
    }

    [Fact]
    public void Validate_204NullSchemaEmptyBody_ReturnsPassed()
    {
        var result = _validator.Validate(TestCase(204, null), Execution(204, body: null));

        Assert.Equal(ContractValidationOutcome.Passed, result.Outcome);
        Assert.True(result.StatusValid);
        Assert.Null(result.SchemaValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Validate_204UnexpectedBody_ReturnsFailed()
    {
        var result = _validator.Validate(TestCase(204, null), Execution(204, "still here"));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        Assert.True(result.StatusValid);
        Assert.False(result.SchemaValid);
        Assert.Equal(ApiContractValidator.UnexpectedBody, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Validate_ValidObjectSchema_ReturnsPassed()
    {
        var schema = ObjectSchema(["ok"], new Dictionary<string, ImportedSchema>
        {
            ["ok"] = BooleanSchema()
        });
        var result = _validator.Validate(TestCase(200, schema), Execution(200, """{"ok":true}"""));

        Assert.Equal(ContractValidationOutcome.Passed, result.Outcome);
        Assert.True(result.SchemaValid);
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReturnsFailed()
    {
        var schema = ObjectSchema(["name"], new Dictionary<string, ImportedSchema>
        {
            ["name"] = StringSchema()
        });
        var result = _validator.Validate(TestCase(200, schema), Execution(200, """{"id":1}"""));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(ApiContractValidator.SchemaRequired, failure.Code);
        Assert.Equal("$.name", failure.JsonPath);
    }

    [Fact]
    public void Validate_WrongPropertyType_ReturnsFailed()
    {
        var schema = ObjectSchema(["age"], new Dictionary<string, ImportedSchema>
        {
            ["age"] = IntegerSchema()
        });
        var result = _validator.Validate(TestCase(200, schema), Execution(200, """{"age":"old"}"""));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(ApiContractValidator.SchemaType, failure.Code);
        Assert.Equal("$.age", failure.JsonPath);
    }

    [Fact]
    public void Validate_InvalidEnum_ReturnsFailed()
    {
        var schema = new ImportedSchema(
            "string",
            [],
            new Dictionary<string, ImportedSchema>(),
            ["active", "inactive"],
            null);
        var result = _validator.Validate(TestCase(200, schema), Execution(200, "\"paused\""));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(ApiContractValidator.SchemaEnum, failure.Code);
        Assert.Equal("$", failure.JsonPath);
    }

    [Fact]
    public void Validate_InvalidArrayItem_ReturnsFailed()
    {
        var schema = new ImportedSchema(
            "array",
            [],
            new Dictionary<string, ImportedSchema>(),
            [],
            IntegerSchema());
        var result = _validator.Validate(TestCase(200, schema), Execution(200, "[1, \"x\"]"));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(ApiContractValidator.SchemaType, failure.Code);
        Assert.Equal("$[1]", failure.JsonPath);
    }

    [Fact]
    public void Validate_NestedObject_ValidatesChildProperties()
    {
        var pet = ObjectSchema(["name"], new Dictionary<string, ImportedSchema>
        {
            ["name"] = StringSchema()
        });
        var schema = ObjectSchema(["pet"], new Dictionary<string, ImportedSchema>
        {
            ["pet"] = pet
        });

        var passed = _validator.Validate(
            TestCase(200, schema),
            Execution(200, """{"pet":{"name":"a"}}"""));
        Assert.Equal(ContractValidationOutcome.Passed, passed.Outcome);

        var failed = _validator.Validate(
            TestCase(200, schema),
            Execution(200, """{"pet":{"name":1}}"""));
        Assert.Equal(ContractValidationOutcome.Failed, failed.Outcome);
        var failure = Assert.Single(failed.Failures);
        Assert.Equal(ApiContractValidator.SchemaType, failure.Code);
        Assert.Equal("$.pet.name", failure.JsonPath);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsFailed()
    {
        var result = _validator.Validate(
            TestCase(200, ObjectSchema()),
            Execution(200, "not-json"));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        Assert.Equal(ApiContractValidator.BodyNotJson, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Validate_EmptyBodyWhenSchemaRequiresContent_ReturnsFailed()
    {
        var result = _validator.Validate(TestCase(200, ObjectSchema(["name"], new Dictionary<string, ImportedSchema>
        {
            ["name"] = StringSchema()
        })), Execution(200, "  "));

        Assert.Equal(ContractValidationOutcome.Failed, result.Outcome);
        Assert.Equal(ApiContractValidator.SchemaEmpty, Assert.Single(result.Failures).Code);
        Assert.True(result.StatusValid);
        Assert.False(result.SchemaValid);
    }

    [Fact]
    public void Validate_ExtraJsonProperties_AreAllowed()
    {
        var schema = ObjectSchema(["id"], new Dictionary<string, ImportedSchema>
        {
            ["id"] = IntegerSchema()
        });
        var result = _validator.Validate(
            TestCase(200, schema),
            Execution(200, """{"id":1,"ignored":true,"also":[1]}"""));

        Assert.Equal(ContractValidationOutcome.Passed, result.Outcome);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Validate_ExecutionError_ReturnsErrorWithoutStatusOrSchemaFailures()
    {
        var schema = ObjectSchema(["name"], new Dictionary<string, ImportedSchema>
        {
            ["name"] = StringSchema()
        });
        var result = _validator.Validate(
            TestCase(200, schema),
            Execution(null, """{"name":"a"}""", error: "The request timed out."));

        Assert.Equal(ContractValidationOutcome.Error, result.Outcome);
        Assert.False(result.StatusValid);
        Assert.Null(result.SchemaValid);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(ApiContractValidator.ExecutionError, failure.Code);
        Assert.Equal("The request timed out.", failure.Message);
        Assert.DoesNotContain(result.Failures, item => item.Code == ApiContractValidator.StatusMismatch);
        Assert.DoesNotContain(result.Failures, item => item.Code == ApiContractValidator.SchemaRequired);
    }

    [Fact]
    public void Validate_NoResponseSchema_SkipsSchemaValidation()
    {
        var result = _validator.Validate(TestCase(200, null), Execution(200, """{"anything":1}"""));

        Assert.Equal(ContractValidationOutcome.Passed, result.Outcome);
        Assert.True(result.StatusValid);
        Assert.Null(result.SchemaValid);
        Assert.Empty(result.Failures);
    }

    private static GeneratedApiTestCase TestCase(int expectedStatus, ImportedSchema? schema) =>
        new(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "/pets",
            [],
            requestBody: null,
            expectedStatus,
            schema,
            "GET",
            "/pets");

    private static ApiTestExecutionResult Execution(int? actualStatus, string? body, string? error = null) =>
        new(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "GET",
            "/pets",
            expectedStatus: 200,
            actualStatus,
            new Dictionary<string, string>(),
            body,
            durationMs: 1,
            statusMatched: actualStatus == 200,
            error);

    private static ImportedSchema ObjectSchema(
        IReadOnlyList<string>? required = null,
        IReadOnlyDictionary<string, ImportedSchema>? properties = null) =>
        new("object", required ?? [], properties ?? new Dictionary<string, ImportedSchema>(), [], null);

    private static ImportedSchema StringSchema() =>
        new("string", [], new Dictionary<string, ImportedSchema>(), [], null);

    private static ImportedSchema IntegerSchema() =>
        new("integer", [], new Dictionary<string, ImportedSchema>(), [], null);

    private static ImportedSchema BooleanSchema() =>
        new("boolean", [], new Dictionary<string, ImportedSchema>(), [], null);
}
