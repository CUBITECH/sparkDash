using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SparkDash.StatusCore;
using Xunit;

namespace SparkDash.StatusCore.Tests;

public sealed class StatusSummaryClientTests
{
    private const string ValidSummary = """
        {
          "schemaVersion": 1,
          "generatedAt": "2026-08-29T18:00:00.000Z",
          "refreshAfterSeconds": 1,
          "state": "healthy",
          "title": "sparkDash",
          "headline": "All 1 system online",
          "statusText": "Live status",
          "dashboardPath": "/",
          "totalCount": 1,
          "onlineCount": 1,
          "offlineCount": 0,
          "units": []
        }
        """;

    [Fact]
    public async Task GetSummaryJsonAsync_UsesOnlyTheLoopbackSummaryEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = CreateHttpClient(request =>
        {
            capturedRequest = request;
            return JsonResponse(HttpStatusCode.OK, ValidSummary);
        });
        var subject = new StatusSummaryClient(client, new Uri("http://127.0.0.1:5555/"));

        var result = await subject.GetSummaryJsonAsync();

        Assert.False(result.IsFallback);
        Assert.Equal(ValidSummary, result.Json);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("http://127.0.0.1:5555/api/status/summary", capturedRequest.RequestUri?.AbsoluteUri);
        Assert.Contains(new MediaTypeWithQualityHeaderValue("application/json"), capturedRequest.Headers.Accept);
    }

    [Fact]
    public async Task GetSummaryJsonAsync_ReturnsOfflinePayloadWhenServerFails()
    {
        using var client = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var subject = new StatusSummaryClient(client, new Uri("http://localhost:5555/"));

        var result = await subject.GetSummaryJsonAsync();

        Assert.True(result.IsFallback);
        using var json = JsonDocument.Parse(result.Json);
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("refreshAfterSeconds").GetInt32());
        Assert.Equal("offline", json.RootElement.GetProperty("state").GetString());
        Assert.Equal("Dashboard unavailable", json.RootElement.GetProperty("headline").GetString());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("units").ValueKind);
    }

    [Fact]
    public async Task GetSummaryJsonAsync_RejectsUnsupportedSchema()
    {
        const string unsupported = """
            { "schemaVersion": 2, "headline": "Wrong contract", "units": [] }
            """;
        using var client = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, unsupported));
        var subject = new StatusSummaryClient(client, new Uri("http://127.0.0.1:5555/"));

        var result = await subject.GetSummaryJsonAsync();

        Assert.True(result.IsFallback);
        using var json = JsonDocument.Parse(result.Json);
        Assert.Equal("offline", json.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task GetSummaryJsonAsync_RejectsOutOfRangeSchemaWithoutThrowing()
    {
        const string unsupported = """
            { "schemaVersion": 999999999999999999999, "headline": "Wrong", "units": [] }
            """;
        using var client = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, unsupported));
        var subject = new StatusSummaryClient(client, new Uri("http://127.0.0.1:5555/"));

        var result = await subject.GetSummaryJsonAsync();

        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GetSummaryJsonAsync_RejectsOversizedPayload()
    {
        var oversized = $$"""
            {
              "schemaVersion": 1,
              "headline": "{{new string('x', 300_000)}}",
              "units": []
            }
            """;
        using var client = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, oversized));
        var subject = new StatusSummaryClient(client, new Uri("http://127.0.0.1:5555/"));

        var result = await subject.GetSummaryJsonAsync();

        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GetSummaryJsonAsync_TimesOutAStalledResponseBody()
    {
        using var client = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StallingContent(),
        });
        var subject = new StatusSummaryClient(
            client,
            new Uri("http://127.0.0.1:5555/"),
            TimeSpan.FromMilliseconds(50));
        var stopwatch = Stopwatch.StartNew();

        var result = await subject.GetSummaryJsonAsync();

        Assert.True(result.IsFallback);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetSummaryJsonAsync_StopsBufferingChunkedOversizeContent()
    {
        var oversized = new ChunkedContent(300_000);
        using var client = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = oversized,
        });
        var subject = new StatusSummaryClient(
            client,
            new Uri("http://127.0.0.1:5555/"),
            TimeSpan.FromSeconds(1));

        var result = await subject.GetSummaryJsonAsync();

        Assert.True(result.IsFallback);
        Assert.True(oversized.BytesWritten <= 256 * 1024);
    }

    [Theory]
    [InlineData("http://192.168.1.2:5555/")]
    [InlineData("https://example.com/")]
    [InlineData("file:///C:/sparkDash/")]
    public void Constructor_RejectsNonLoopbackEndpoints(string endpoint)
    {
        using var client = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidSummary));

        var error = Assert.Throws<ArgumentException>(() =>
            new StatusSummaryClient(client, new Uri(endpoint)));

        Assert.Equal("baseAddress", error.ParamName);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return new HttpClient(new StubHandler(respond));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StallingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return Task.Delay(Timeout.InfiniteTimeSpan);
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class ChunkedContent : HttpContent
    {
        private readonly int totalBytes;

        internal ChunkedContent(int totalBytes)
        {
            this.totalBytes = totalBytes;
        }

        internal int BytesWritten { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return WriteAsync(stream, CancellationToken.None);
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            return WriteAsync(stream, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            while (BytesWritten < totalBytes)
            {
                var count = Math.Min(buffer.Length, totalBytes - BytesWritten);
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                BytesWritten += count;
            }
        }
    }
}
