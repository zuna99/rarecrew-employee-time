using EmployeeTime.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeTime.Web.Controllers;

[ApiController]
[Route("api/angular/time-entries")]
public sealed class TimeEntriesController(ITimeEntriesClientService client) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await client.GetAsync(TimeEntrySource.Angular, cancellationToken));
        }
        catch (TimeEntriesException exception)
        {
            return Problem(title: "Unable to load time entries", detail: exception.Message,
                statusCode: exception.StatusCode);
        }
    }
}
