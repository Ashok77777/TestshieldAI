using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace TestShieldAI.Engine;

public sealed class HttpApiTestRunner : IApiTestRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _httpClient;

    public HttpApiTestRunner(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan)
        {
            _httpClient.Timeout = DefaultTimeout;
        }
    }

    public async Task<IReadOnlyList<ApiTestExecutionResult>> RunAsync(
        string baseUrl,
        IReadOnlyList<GeneratedApiTestCase> tests,
        CancellationToken cancellationToken = default)
    {
        if (tests is null || tests.Count == 0)
        {
            return [];
        }

        if (!TryCreateBaseUri(baseUrl, out var baseUri, out var baseUrlError))
        {
            return tests.Select(test => Failed(test, durationMs: 0, baseUrlError)).ToList();
        }

        var results = new List<ApiTestExecutionResult>(tests.Count);
        foreach (var test in tests)
        {
            results.Add(await ExecuteAsync(baseUri, test, cancellationToken));
        }

        return results;
    }

    private async Task<ApiTestExecutionResult> ExecuteAsync(
        Uri baseUri,
        GeneratedApiTestCase test,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = BuildRequest(baseUri, test);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var actualStatus = (int)response.StatusCode;
            return new ApiTestExecutionResult(
                test.Kind,
                test.Method,
                test.Path,
                test.SourceMethod,
                test.SourcePath,
                test.ExpectedStatus,
                actualStatus,
                ReadHeaders(response),
                body,
                (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
                actualStatus == test.ExpectedStatus,
                error: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return Failed(test, (int)stopwatch.ElapsedMilliseconds, "The request timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return Failed(test, (int)stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    private static HttpRequestMessage BuildRequest(Uri baseUri, GeneratedApiTestCase test)
    {
        var uri = BuildUri(baseUri, test);
        var request = new HttpRequestMessage(new HttpMethod(test.Method), uri);

        foreach (var parameter in test.Parameters)
        {
            if (!string.Equals(parameter.Location, "header", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(parameter.Name, parameter.Placeholder);
        }

        if (test.RequestBody is not null)
        {
            request.Content = new StringContent(test.RequestBody, Encoding.UTF8, "application/json");
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static Uri BuildUri(Uri baseUri, GeneratedApiTestCase test)
    {
        var relativePath = test.Path.TrimStart('/');
        var uri = new Uri(baseUri, relativePath);

        var query = test.Parameters
            .Where(parameter => string.Equals(parameter.Location, "query", StringComparison.OrdinalIgnoreCase))
            .Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Placeholder)}")
            .ToList();

        if (query.Count == 0)
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", query)
        };
        return builder.Uri;
    }

    private static bool TryCreateBaseUri(string baseUrl, out Uri baseUri, out string error)
    {
        baseUri = null!;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            error = "Base URL is required.";
            return false;
        }

        var normalized = baseUrl.Trim();
        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out baseUri!) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Base URL is not a valid absolute HTTP(S) URI.";
            return false;
        }

        error = "";
        return true;
    }

    private static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        return headers;
    }

    private static ApiTestExecutionResult Failed(GeneratedApiTestCase test, int durationMs, string error) =>
        new(
            test.Kind,
            test.Method,
            test.Path,
            test.SourceMethod,
            test.SourcePath,
            test.ExpectedStatus,
            actualStatus: null,
            EmptyHeaders,
            body: null,
            durationMs,
            statusMatched: false,
            error);
}
