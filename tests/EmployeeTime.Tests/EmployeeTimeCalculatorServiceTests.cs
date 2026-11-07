using System.Text.Json;
using EmployeeTime.Web.Models;
using EmployeeTime.Web.Services;

namespace EmployeeTime.Tests;

public sealed class EmployeeTimeCalculatorServiceTests
{
    private readonly EmployeeTimeCalculatorService calculator = new();

    [Fact]
    public void CalculatesSharedCasesWithoutPrematureRounding()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "report-cases.json")));
        var entries = fixture.RootElement.GetProperty("entries").Deserialize<TimeEntry?[]>()!;
        TimeReport report = calculator.Calculate(entries);
        JsonElement expected = fixture.RootElement.GetProperty("expected");
        var expectedEmployees = expected.GetProperty("employees").EnumerateArray().ToArray();

        Assert.Equal(expectedEmployees.Length, report.Employees.Count);
        for (int index = 0; index < expectedEmployees.Length; index++)
        {
            var employee = report.Employees[index];
            var item = expectedEmployees[index];
            Assert.Equal(item.GetProperty("name").GetString(), employee.Name);
            Assert.InRange(Math.Abs(employee.TotalHours - item.GetProperty("totalHours").GetDecimal()), 0m, 0.000000001m);
            Assert.Equal(item.GetProperty("isBelowThreshold").GetBoolean(), employee.IsBelowThreshold);
        }

        Assert.Equal(9, report.Quality.Accepted);
        Assert.Equal(6, report.Quality.Excluded);
        Assert.Equal(1, report.Quality.Deleted);
        Assert.Equal(2, report.Quality.MissingNames);
        Assert.Equal(2, report.Quality.InvalidTimestamps);
        Assert.Equal(1, report.Quality.NegativeDurations);
        Assert.InRange(Math.Abs(report.Employees.Sum(employee => employee.SharePercent) - 100m), 0m, 0.000000001m);
    }

    [Fact]
    public void EmptyInputProducesAnEmptyReport()
    {
        TimeReport report = calculator.Calculate([]);
        Assert.Empty(report.Employees);
        Assert.Equal(0m, report.TotalHours);
        Assert.Equal(0, report.BelowThresholdCount);
    }

    [Fact]
    public void FractionalHourSessionsSumToTheExactHundredHourThreshold()
    {
        DateTimeOffset beginning = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var entries = Enumerable.Range(0, 300).Select(index =>
        {
            DateTimeOffset start = beginning.AddMinutes(index * 20);
            return Entry("Alex", start.ToString("O"), start.AddMinutes(20).ToString("O"));
        });

        TimeReport report = calculator.Calculate(entries);
        EmployeeSummary employee = Assert.Single(report.Employees);

        Assert.Equal(100m, employee.TotalHours);
        Assert.False(employee.IsBelowThreshold);
        Assert.Equal(0, report.BelowThresholdCount);
    }

    [Fact]
    public void ZeroDurationIsIncludedWithoutDividingByZero()
    {
        TimeReport report = calculator.Calculate([Entry("Alex", "2026-01-01T00:00:00", "2026-01-01T00:00:00")]);
        var employee = Assert.Single(report.Employees);
        Assert.Equal(0m, employee.TotalHours);
        Assert.Equal(0m, employee.SharePercent);
        Assert.True(employee.IsBelowThreshold);
    }

    [Fact]
    public void NamesRemainCaseSensitiveAndTieOrderIsOrdinal()
    {
        TimeReport report = calculator.Calculate([
            Entry("alex", "2026-01-01T00:00:00", "2026-01-01T01:00:00"),
            Entry("Alex", "2026-01-01T00:00:00", "2026-01-01T01:00:00")]);
        Assert.Equal(["Alex", "alex"], report.Employees.Select(employee => employee.Name));
    }

    [Fact]
    public void DeletedEntryIsCountedOnceEvenWhenOtherFieldsAreInvalid()
    {
        var report = calculator.Calculate([new TimeEntry { DeletedOn = "2026-01-01", EmployeeName = null }]);
        Assert.Equal(1, report.Quality.Deleted);
        Assert.Equal(0, report.Quality.MissingNames);
        Assert.Equal(1, report.Quality.Excluded);
    }

    [Theory]
    [InlineData(null, "2026-01-01T00:00:00")]
    [InlineData("2026-01-01T00:00:00", "invalid")]
    [InlineData("2026-02-30T00:00:00", "2026-03-01T00:00:00")]
    [InlineData("2026-01-01T24:00:00", "2026-01-02T01:00:00")]
    [InlineData("2026-01-01", "2026-01-02")]
    public void InvalidOrMissingTimestampsAreExcluded(string? start, string? end)
    {
        var report = calculator.Calculate([new TimeEntry { EmployeeName = "Alex", StartTimeUtc = start, EndTimeUtc = end }]);
        Assert.Empty(report.Employees);
        Assert.Equal(1, report.Quality.InvalidTimestamps);
    }

    internal static TimeEntry Entry(string name, string start, string end) =>
        new() { EmployeeName = name, StartTimeUtc = start, EndTimeUtc = end };
}
