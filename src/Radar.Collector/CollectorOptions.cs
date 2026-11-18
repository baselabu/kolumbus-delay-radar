using System.ComponentModel.DataAnnotations;
namespace Radar.Collector;

public class CollectorOptions
{
    public const string Section = "Collector";

    [Required] public string DatasetId { get; set; } = "";
    [Required, Url] public string BaseUrl { get; set; } = "";
    [Range(10, 600)] public int PollingIntervalSeconds { get; set; }
    [Required] public string ClientName { get; set; } = "";
    public string? ReplayFolder { get; set; }
    [Required, Url] public string GraphQlUrl { get; set; } = "";
    [Required] public string AuthorityId { get; set; } = "";
}