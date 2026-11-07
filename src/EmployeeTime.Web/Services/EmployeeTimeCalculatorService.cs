using System.Globalization;
using System.Text.RegularExpressions;
using EmployeeTime.Web.Models;

namespace EmployeeTime.Web.Services;

public sealed class EmployeeTimeCalculatorService
{
    private static readonly Regex TimestampPattern = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public TimeReport Calculate(IEnumerable<TimeEntry?> entries)
    {
        Dictionary<string, long> ticksByName = new(StringComparer.Ordinal);
        int received = 0, deleted = 0, missingNames = 0, invalidTimestamps = 0, negativeDurations = 0;

        foreach (TimeEntry? entry in entries)
        {
            received++;
            if (entry is null)
            {
                invalidTimestamps++;
                continue;
            }

            if (entry.DeletedOn is not null)
            {
                deleted++;
                continue;
            }

            string? name = entry.EmployeeName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                missingNames++;
                continue;
            }

            if (!TryParseUtc(entry.StartTimeUtc, out DateTimeOffset start) ||
                !TryParseUtc(entry.EndTimeUtc, out DateTimeOffset end))
            {
                invalidTimestamps++;
                continue;
            }

            if (end < start)
            {
                negativeDurations++;
                continue;
            }

            long durationTicks = (end - start).Ticks;
            ticksByName[name] = checked(ticksByName.GetValueOrDefault(name) + durationTicks);
        }

        long totalTicks = ticksByName.Values.Sum();
        EmployeeSummary[] employees = ticksByName
            .Select(pair => new EmployeeSummary(
                pair.Key,
                pair.Value / (decimal)TimeSpan.TicksPerHour,
                totalTicks == 0 ? 0 : pair.Value / (decimal)totalTicks * 100m))
            .OrderByDescending(employee => employee.TotalHours)
            .ThenBy(employee => employee.Name, StringComparer.Ordinal)
            .ToArray();

        return new TimeReport(employees,
            new DataQuality(received, deleted, missingNames, invalidTimestamps, negativeDurations),
            totalTicks / (decimal)TimeSpan.TicksPerHour);
    }

    private static bool TryParseUtc(string? value, out DateTimeOffset result)
    {
        result = default;
        return value is not null && TimestampPattern.IsMatch(value) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    }
}
