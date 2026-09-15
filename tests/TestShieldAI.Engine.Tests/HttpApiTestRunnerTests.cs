using System.Net;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class HttpApiTestRunnerTests
{
    [Fact]
    public async Task Run_EmptyTests_ReturnsEmptyList()
    {
        var runner = new HttpApiTestRunner(new HttpClient(new StubHandler()));

        var results = await runner.RunAsync("https://api.example.com", []);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Run_GetWithPathParameter_SendsAbsoluteUri()
    {
        var handler = new StubHandler();
        var runner = new HttpApiTestRunner(new HttpClient(handler));
        var test = Case("GET", "/pets/{id}", "/pets/1", expectedStatus: 200);

        var results = await runner.RunAsync("https://api.example.com", [test]);

        var result = Assert.Single(results);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("https://api.example.com/pets/1", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal(200, result.ActualStatus);
        Assert.True(result.StatusMatched);
        Assert.Equal("{\"ok\":true}", result.Body);
        Assert.True(result.DurationMs >= 0);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Run_RequiredQueryParameter_IsAppended()
    {
        var handler = new StubHandler();
        var runner = new HttpApiTestRunner(new HttpClient(handler));
        var test = Case(
            "GET",
            "/pets",
            "/pets",
            expectedStatus: 200,
            parameters: [new GeneratedApiParameter("limit", "query", true, "1")]);

        await runner.RunAsync("https://api.example.com/v1", [test]);

        Assert.Equal("https://api.example.com/v1/pets?limit=1", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Run_PostWithBody_SendsJsonContent()
    {
        var handler = new StubHandler(HttpStatusCode.Created);
        var runner = new HttpApiTestRunner(new HttpClient(handler));
        var test = Case("POST", "/pets", "/pets", expectedStatus: 201, requestBody: """{"name":"a"}""");

        var result = Assert.Single(await runner.RunAsync("https://api.example.com", [test]));

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("""{"name":"a"}""", handler.LastRequestBody);
        Assert.Equal("application/json", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(201, result.ActualStatus);
        Assert.True(result.StatusMatched);
    }

    [Fact]
    public async Task Run_StatusMismatch_SetsStatusMatchedFalse()
    {
        var runner = new HttpApiTestRunner(new HttpClient(new StubHandler(HttpStatusCode.NotFound)));
        var test = Case("GET", "/pets", "/pets", expectedStatus: 200);

        var result = Assert.Single(await runner.RunAsync("https://api.example.com", [test]));

        Assert.Equal(404, result.ActualStatus);
        Assert.False(result.StatusMatched);
    }

    [Fact]
    public async Task Run_InvalidBaseUrl_DoesNotSendRequest()
    {
        var handler = new StubHandler();
        var runner = new HttpApiTestRunner(new HttpClient(handler));
        var test = Case("GET", "/pets", "/pets", expectedStatus: 200);

        var result = Assert.Single(await runner.RunAsync("not-a-url", [test]));

        Assert.Null(handler.LastRequest);
        Assert.False(result.StatusMatched);
        Assert.Null(result.ActualStatus);
        Assert.Contains("Base URL", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_HttpFailure_CapturesErrorWithoutThrowing()
    {
        var runner = new HttpApiTestRunner(new HttpClient(new ThrowingHandler()));
        var test = Case("GET", "/pets", "/pets", expectedStatus: 200);

        var result = Assert.Single(await runner.RunAsync("https://api.example.com", [test]));

        Assert.False(result.StatusMatched);
        Assert.Null(result.ActualStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Run_CapturesResponseHeaders()
    {
        var handler = new StubHandler();
        handler.ResponseHeaders.Add("X-Test", "yes");
        var runner = new HttpApiTestRunner(new HttpClient(handler));

        var result = Assert.Single(
            await runner.RunAsync("https://api.example.com", [Case("GET", "/pets", "/pets", 200)]));

        Assert.True(result.Headers.ContainsKey("X-Test"));
        Assert.Equal("yes", result.Headers["X-Test"]);
    }

    private static GeneratedApiTestCase Case(
        string method,
        string pathTemplate,
        string path,
        int expectedStatus,
        IReadOnlyList<GeneratedApiParameter>? parameters = null,
        string? requestBody = null) =>
        new(
            GeneratedApiTestKind.HappyPath,
            method,
            pathTemplate,
            path,
            parameters ?? [],
            requestBody,
            expectedStatus,
            expectedResponseSchema: null,
            method,
            pathTemplate);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public StubHandler(HttpStatusCode status = HttpStatusCode.OK)
        {
            _status = status;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        public Dictionary<string, string> ResponseHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent("{\"ok\":true}")
            };
            foreach (var header in ResponseHeaders)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
