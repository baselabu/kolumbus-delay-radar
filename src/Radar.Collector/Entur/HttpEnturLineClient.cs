using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Radar.Data;

namespace Radar.Collector.Entur;

public class HttpEnturLineClient(HttpClient http, IOptions<CollectorOptions> options) : IEnturLineClient
{
    private const string Query =
        "query($authorities: [String]) { lines(authorities: $authorities) { id publicCode name transportMode } }";

    public async Task<IReadOnlyList<Line>> GetLinesAsync(CancellationToken ct)
    {
        var variables = new { authorities = new[] { options.Value.AuthorityId } };
        using var response = await http.PostAsJsonAsync(options.Value.GraphQlUrl, new { query = Query, variables }, ct);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new HttpRequestException("Entur GraphQL request failed with status code: " + response.StatusCode);
        }
        var graphQlResponse = await response.Content.ReadFromJsonAsync<GraphQlResponse>(ct);
        var lines = graphQlResponse?.Data?.Lines ?? throw new InvalidOperationException("Entur GraphQL response did not contain lines data.");

        return lines.Select(dto => new Line(dto.Id, dto.PublicCode, dto.Name, dto.TransportMode)).ToList();
    }

    private record GraphQlResponse(LinesData? Data);
    private record LinesData(List<LineDto> Lines);
    private record LineDto(string Id, string? PublicCode, string? Name, string? TransportMode);
}