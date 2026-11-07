namespace EmployeeTime.Web.Services;

public sealed class TimeEntriesOptions
{
    public const string SectionName = "TimeEntries";
    public string CSharpApiKey { get; init; } = string.Empty;
    public string AngularApiKey { get; init; } = string.Empty;
}

public enum TimeEntrySource
{
    CSharp,
    Angular
}
