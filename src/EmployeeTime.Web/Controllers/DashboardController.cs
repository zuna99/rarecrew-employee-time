using EmployeeTime.Web.Models;
using EmployeeTime.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace EmployeeTime.Web.Controllers;

public sealed class DashboardController(
    ITimeEntriesClientService client,
    EmployeeTimeCalculatorService calculator,
    PieChartRendererService chartRenderer,
    IMemoryCache chartCache) : Controller
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var entries = await client.GetAsync(TimeEntrySource.CSharp, cancellationToken);
            TimeReport report = calculator.Calculate(entries);
            string? downloadUrl = null;
            string? preview = null;
            if (report.TotalHours > 0)
            {
                // Cache the small report; the full-size PNG is only rendered if it is downloaded.
                Guid chartId = Guid.NewGuid();
                chartCache.Set(chartId, report, TimeSpan.FromMinutes(10));
                downloadUrl = Url.Action(nameof(Download), new { id = chartId });
                preview = "data:image/png;base64," +
                    Convert.ToBase64String(chartRenderer.Render(report, includeLegend: false));
            }
            return View(new DashboardViewModel(report, downloadUrl, preview, null));
        }
        catch (TimeEntriesException exception)
        {
            Response.StatusCode = exception.StatusCode;
            return View(DashboardViewModel.Failure(exception.Message));
        }
    }

    [HttpGet("/reports/{id:guid}/chart.png")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Download(Guid id)
    {
        if (!chartCache.TryGetValue(id, out TimeReport? report) || report is null)
        {
            return NotFound("This chart is no longer available. Refresh the report and download it again.");
        }

        return File(chartRenderer.Render(report), "image/png", "employee-time.png");
    }

    [HttpGet("/error")]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Index", DashboardViewModel.Failure("The report could not be displayed. Please try again."));
    }
}
