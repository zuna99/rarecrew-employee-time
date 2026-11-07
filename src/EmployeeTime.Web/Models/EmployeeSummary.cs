namespace EmployeeTime.Web.Models;

public sealed record EmployeeSummary(string Name, decimal TotalHours, decimal SharePercent)
{
    public bool IsBelowThreshold => TotalHours < 100m;
}
