namespace EmployeeTime.Web.Models;

public sealed record DataQuality(
    int Received,
    int Deleted,
    int MissingNames,
    int InvalidTimestamps,
    int NegativeDurations)
{
    public int Excluded => Deleted + MissingNames + InvalidTimestamps + NegativeDurations;
    public int Accepted => Received - Excluded;
}

public sealed record TimeReport(
    IReadOnlyList<EmployeeSummary> Employees,
    DataQuality Quality,
    decimal TotalHours)
{
    public int BelowThresholdCount => Employees.Count(employee => employee.IsBelowThreshold);
}
