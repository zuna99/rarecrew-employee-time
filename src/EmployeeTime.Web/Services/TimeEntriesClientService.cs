using System.Net.Http.Json;
using System.Text.Json;
using EmployeeTime.Web.Models;
using Microsoft.Extensions.Options;

namespace EmployeeTime.Web.Services;

public sealed class TimeEntriesClientService(
    HttpClient httpClient,
    IOptions<TimeEntriesOptions> options,
    ILogger<TimeEntriesClientService> logger) : ITimeEntriesClientService
{
    public async Task<IReadOnlyList<TimeEntry?>> GetAsync(
        TimeEntrySource source, CancellationToken cancellationToken)
    {
        string key = source == TimeEntrySource.CSharp
            ? options.Value.CSharpApiKey
            : options.Value.AngularApiKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new TimeEntriesException(
                "The API key is not configured. Follow the local setup steps in the README.",
                StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                $"gettimeentries?code={Uri.EscapeDataString(key)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Time entry provider returned status {StatusCode}.", (int)response.StatusCode);
                throw ProviderUnavailable();
            }

            return await response.Content.ReadFromJsonAsync<List<TimeEntry?>>(
                cancellationToken: cancellationToken) ?? throw ProviderUnavailable();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Time entry request failed ({Failure}).", exception.HttpRequestError);
            throw ProviderUnavailable();
        }
        catch (JsonException)
        {
            throw new TimeEntriesException("The time entry provider returned an invalid response.",
                StatusCodes.Status502BadGateway);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeEntriesException("The time entry provider took too long to respond. Please try again.",
                StatusCodes.Status504GatewayTimeout);
        }
    }

    private static TimeEntriesException ProviderUnavailable() => new(
        "Time entries could not be loaded from the provider. Please try again.",
        StatusCodes.Status502BadGateway);
}
