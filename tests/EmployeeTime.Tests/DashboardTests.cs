using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmployeeTime.Web.Models;
using EmployeeTime.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace EmployeeTime.Tests;

public sealed class DashboardTests
{
    [Fact]
    public async Task MvcRendersTheTableAndARealPngFromOneFetch()
    {
        var source = new StubClient([
            EmployeeTimeCalculatorServiceTests.Entry("Alex", "2026-01-01T00:00:00", "2026-01-06T00:00:00"),
            EmployeeTimeCalculatorServiceTests.Entry("Blair", "2026-01-01T00:00:00", "2026-01-02T00:00:00")]);
        await using var factory = CreateFactory(source);
        using var http = factory.CreateClient();
        var response = await http.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("120.00", html);
        Assert.Contains("24.00", html);
        Assert.Contains("class=\"below-threshold\"", html);
        Assert.DoesNotContain("download=", html);
        Assert.True(html.IndexOf("Alex", StringComparison.Ordinal) < html.IndexOf("Blair", StringComparison.Ordinal));
        string downloadUrl = Regex.Match(html, "href=\"(/reports/[^\"]+/chart.png)\"").Groups[1].Value;
        Assert.NotEmpty(downloadUrl);
        using var download = await http.GetAsync(downloadUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("employee-time.png", download.Content.Headers.ContentDisposition!.ToString());
        byte[] png = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);
        Assert.Equal(1200, bitmap.Width);
        Assert.Equal(760, bitmap.Height);
        Assert.Equal([TimeEntrySource.CSharp], source.Calls);
    }

    [Fact]
    public async Task MissingChartReturnsNotFoundWithoutFetchingAnotherDataset()
    {
        var source = new StubClient([]);
        await using var factory = CreateFactory(source);
        using var http = factory.CreateClient();

        var response = await http.GetAsync($"/reports/{Guid.NewGuid()}/chart.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Refresh the report", await response.Content.ReadAsStringAsync());
        Assert.Empty(source.Calls);
    }

    [Fact]
    public async Task AngularEndpointUsesItsOwnSourceAndPreservesTheJsonContract()
    {
        var source = new StubClient([EmployeeTimeCalculatorServiceTests.Entry("Alex", "2026-01-01T00:00:00", "2026-01-01T01:00:00")]);
        await using var factory = CreateFactory(source);
        using var http = factory.CreateClient();
        using JsonDocument json = JsonDocument.Parse(await http.GetStringAsync("/api/angular/time-entries"));
        Assert.Equal("2026-01-01T00:00:00", json.RootElement[0].GetProperty("StarTimeUtc").GetString());
        Assert.Equal([TimeEntrySource.Angular], source.Calls);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/api/angular/time-entries")]
    public async Task ProviderErrorsHaveAUsefulStatusAndMessage(string path)
    {
        var source = new StubClient([]) { Error = new TimeEntriesException("Provider unavailable. Please try again.", 502) };
        await using var factory = CreateFactory(source);
        using var http = factory.CreateClient();
        var response = await http.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("Provider unavailable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmptyResponseDoesNotRenderABrokenChart()
    {
        await using var factory = CreateFactory(new StubClient([]));
        using var http = factory.CreateClient();
        string html = await http.GetStringAsync("/");
        Assert.Contains("No working time to display", html);
        Assert.DoesNotContain("data:image/png", html);
    }

    [Fact]
    public async Task ZeroHoursRendersATableWithoutAFabricatedPie()
    {
        await using var factory = CreateFactory(new StubClient([
            EmployeeTimeCalculatorServiceTests.Entry("Alex", "2026-01-01T00:00:00", "2026-01-01T00:00:00")]));
        using var http = factory.CreateClient();
        string html = await http.GetStringAsync("/");
        Assert.Contains("Alex", html);
        Assert.Contains("No positive hours yet", html);
        Assert.DoesNotContain("data:image/png", html);
    }

    [Fact]
    public async Task EmployeeNamesAreHtmlEncoded()
    {
        await using var factory = CreateFactory(new StubClient([
            EmployeeTimeCalculatorServiceTests.Entry("<script>alert(1)</script>", "2026-01-01T00:00:00", "2026-01-01T01:00:00")]));
        using var http = factory.CreateClient();
        string html = await http.GetStringAsync("/");
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
    }

    private static WebApplicationFactory<Program> CreateFactory(StubClient source) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITimeEntriesClientService>();
            services.AddSingleton<ITimeEntriesClientService>(source);
        }));

    private sealed class StubClient(IReadOnlyList<TimeEntry?> entries) : ITimeEntriesClientService
    {
        public List<TimeEntrySource> Calls { get; } = [];
        public TimeEntriesException? Error { get; init; }

        public Task<IReadOnlyList<TimeEntry?>> GetAsync(TimeEntrySource source, CancellationToken cancellationToken)
        {
            Calls.Add(source);
            return Error is null ? Task.FromResult(entries) : Task.FromException<IReadOnlyList<TimeEntry?>>(Error);
        }
    }
}
