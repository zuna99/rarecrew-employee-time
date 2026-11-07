namespace EmployeeTime.Web.Services;

public sealed class TimeEntriesException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
