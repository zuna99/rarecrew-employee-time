using System.Text.Json.Serialization;

namespace EmployeeTime.Web.Models;

public sealed record TimeEntry
{
    [JsonPropertyName("Id")]
    public string? Id { get; init; }

    [JsonPropertyName("EmployeeName")]
    public string? EmployeeName { get; init; }

    [JsonPropertyName("StarTimeUtc")]
    public string? StartTimeUtc { get; init; }

    [JsonPropertyName("EndTimeUtc")]
    public string? EndTimeUtc { get; init; }

    [JsonPropertyName("DeletedOn")]
    public string? DeletedOn { get; init; }
}
