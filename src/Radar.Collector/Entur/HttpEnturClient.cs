using Microsoft.Extensions.Options;

namespace Radar.Collector.Entur;

public class HttpEnturClient(HttpClient http, IOptions<CollectorOptions> options) : IEnturClient
{
    // ONE id for the whole program run (static!)
    private static readonly string RequestorId = Guid.NewGuid().ToString();

    public async Task<Stream> GetEstimatedTimetableAsync(CancellationToken ct)
    {
        var url = $"{options.Value.BaseUrl}?datasetId={options.Value.DatasetId}&requestorId={RequestorId}";

        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead ,ct);

       if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new HttpRequestException($"Entur API returned error: {response.StatusCode}");
        }
        return await response.Content.ReadAsStreamAsync(ct);
    }
}