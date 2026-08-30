using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SparkDash.StatusCore;

public sealed record StatusSummaryResult(string Json, bool IsFallback);

public sealed class StatusSummaryClient
{
    private const int MaximumPayloadBytes = 256 * 1024;
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly MediaTypeWithQualityHeaderValue JsonMediaType = new("application/json");
    private readonly HttpClient httpClient;
    private readonly Uri summaryUri;
    private readonly TimeSpan requestTimeout;

    public StatusSummaryClient(
        HttpClient httpClient,
        Uri baseAddress,
        TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(baseAddress);

        if (!baseAddress.IsAbsoluteUri ||
            (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps) ||
            !baseAddress.IsLoopback)
        {
            throw new ArgumentException(
                "The desktop status client only accepts an HTTP(S) loopback endpoint.",
                nameof(baseAddress));
        }

        this.httpClient = httpClient;
        this.requestTimeout = requestTimeout ?? DefaultRequestTimeout;
        if (this.requestTimeout <= TimeSpan.Zero || this.requestTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "Request timeout must be greater than zero and no more than five minutes.");
        }
        var root = new UriBuilder(baseAddress)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
        summaryUri = new Uri(root, "api/status/summary");
    }

    public async Task<StatusSummaryResult> GetSummaryJsonAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(requestTimeout);
            var requestToken = deadline.Token;
            using var request = new HttpRequestMessage(HttpMethod.Get, summaryUri);
            request.Headers.Accept.Add(JsonMediaType);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > MaximumPayloadBytes)
            {
                throw new InvalidDataException("sparkDash status summary is too large.");
            }

            var json = await ReadBoundedUtf8Async(response.Content, requestToken).ConfigureAwait(false);
            ValidateSummary(json);
            return new StatusSummaryResult(json, IsFallback: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new StatusSummaryResult(CreateOfflinePayload(), IsFallback: true);
        }
        catch (Exception error) when (
            error is HttpRequestException or
            JsonException or
            InvalidDataException)
        {
            return new StatusSummaryResult(CreateOfflinePayload(), IsFallback: true);
        }
    }

    private static async Task<string> ReadBoundedUtf8Async(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        using var buffer = new LimitedMemoryStream(MaximumPayloadBytes);
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }

    private sealed class LimitedMemoryStream(int maximumBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWithinLimit(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWithinLimit(buffer.Length);
            base.Write(buffer);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            EnsureWithinLimit(count);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            EnsureWithinLimit(buffer.Length);
            return base.WriteAsync(buffer, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            EnsureWithinLimit(1);
            base.WriteByte(value);
        }

        private void EnsureWithinLimit(int additionalBytes)
        {
            if (Position + additionalBytes > maximumBytes)
            {
                throw new InvalidDataException("sparkDash status summary is too large.");
            }
        }
    }

    private static void ValidateSummary(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out var version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var schemaVersion) ||
            schemaVersion != 1 ||
            !root.TryGetProperty("headline", out var headline) ||
            headline.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("units", out var units) ||
            units.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("sparkDash returned an unsupported status summary contract.");
        }
    }

    private static string CreateOfflinePayload()
    {
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedAt = DateTimeOffset.UtcNow.ToString("O"),
            refreshAfterSeconds = 1,
            state = "offline",
            title = "sparkDash",
            headline = "Dashboard unavailable",
            statusText = "Start the local sparkDash service",
            dashboardPath = "/",
            totalCount = 0,
            onlineCount = 0,
            offlineCount = 0,
            units = Array.Empty<object>(),
        });
    }
}
