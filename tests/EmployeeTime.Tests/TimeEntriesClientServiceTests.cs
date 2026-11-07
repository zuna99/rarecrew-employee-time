using System.Net;
using EmployeeTime.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EmployeeTime.Tests;

public sealed class TimeEntriesClientServiceTests
{
    [Theory]
    [InlineData(TimeEntrySource.CSharp, "csharp-test-key")]
    [InlineData(TimeEntrySource.Angular, "angular-test-key")]
    public async Task SelectsTheCorrectKeyAndMapsTheUpstreamSpelling(TimeEntrySource source, string expectedKey)
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal($"?code={expectedKey}", request.RequestUri!.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"EmployeeName":"Alex","StarTimeUtc":"2026-01-01T00:00:00","EndTimeUtc":"2026-01-01T01:00:00","DeletedOn":null}]""")
            });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/api/") };
        var entries = await CreateClient(http).GetAsync(source, CancellationToken.None);
        Assert.Equal("2026-01-01T00:00:00", Assert.Single(entries)!.StartTimeUtc);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task InvalidPayloadProducesASafeError(string body)
    {
        using var http = HttpReturning(HttpStatusCode.OK, body);
        var error = await Assert.ThrowsAsync<TimeEntriesException>(() =>
            CreateClient(http).GetAsync(TimeEntrySource.CSharp, CancellationToken.None));
        Assert.Equal(502, error.StatusCode);
        Assert.DoesNotContain("test-key", error.ToString());
    }

    [Fact]
    public async Task UpstreamFailureDoesNotExposeResponseBodyOrCredentials()
    {
        using var http = HttpReturning(HttpStatusCode.Forbidden, "sensitive upstream details");
        var error = await Assert.ThrowsAsync<TimeEntriesException>(() =>
            CreateClient(http).GetAsync(TimeEntrySource.CSharp, CancellationToken.None));
        Assert.Equal(502, error.StatusCode);
        Assert.DoesNotContain("sensitive", error.ToString());
        Assert.DoesNotContain("csharp-test-key", error.ToString());
    }

    [Fact]
    public async Task MissingConfigurationDoesNotCallTheProvider()
    {
        using var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException("Must not call")));
        var client = new TimeEntriesClientService(http, Options.Create(new TimeEntriesOptions()), NullLogger<TimeEntriesClientService>.Instance);
        var error = await Assert.ThrowsAsync<TimeEntriesException>(() => client.GetAsync(TimeEntrySource.CSharp, CancellationToken.None));
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task TimeoutIsReportedAsGatewayTimeout()
    {
        using var http = new HttpClient(new StubHandler((_, _) => throw new TaskCanceledException()));
        http.BaseAddress = new Uri("https://example.test/api/");
        var error = await Assert.ThrowsAsync<TimeEntriesException>(() =>
            CreateClient(http).GetAsync(TimeEntrySource.CSharp, CancellationToken.None));
        Assert.Equal(504, error.StatusCode);
    }

    [Fact]
    public async Task RequestCancellationIsNotTurnedIntoAProviderError()
    {
        using var http = new HttpClient(new StubHandler((_, token) => Task.FromCanceled<HttpResponseMessage>(token)));
        http.BaseAddress = new Uri("https://example.test/api/");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(http).GetAsync(TimeEntrySource.CSharp, cancellation.Token));
    }

    private static TimeEntriesClientService CreateClient(HttpClient http) => new(http,
        Options.Create(new TimeEntriesOptions { CSharpApiKey = "csharp-test-key", AngularApiKey = "angular-test-key" }),
        NullLogger<TimeEntriesClientService>.Instance);

    private static HttpClient HttpReturning(HttpStatusCode status, string body) => new(new StubHandler((_, _) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) })))
        { BaseAddress = new Uri("https://example.test/api/") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
