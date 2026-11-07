using EmployeeTime.Web.Models;

namespace EmployeeTime.Web.Services;

public interface ITimeEntriesClientService
{
    Task<IReadOnlyList<TimeEntry?>> GetAsync(TimeEntrySource source, CancellationToken cancellationToken);
}
