namespace EmployeeTime.Web.Models;

public sealed record DashboardViewModel(TimeReport? Report, string? ChartDownloadUrl, string? PreviewDataUri, string? Error)
{
    public static DashboardViewModel Failure(string message) => new(null, null, null, message);
}
