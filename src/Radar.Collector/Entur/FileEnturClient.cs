using Microsoft.Extensions.Options;
namespace Radar.Collector.Entur;
public class FileEnturClient : IEnturClient
{
    private readonly string[] _files;
    private int _currentFileIndex = 0;

    public FileEnturClient(IOptions<CollectorOptions> options)
    {
        if (string.IsNullOrEmpty(options.Value.ReplayFolder))
        {
            throw new ArgumentException("ReplayFolder must be set in CollectorOptions when using FileEnturClient");
        }

        _files = Directory.GetFiles(options.Value.ReplayFolder, "*.xml")
            .OrderBy(f => f)
            .ToArray();

        if (_files.Length == 0)
        {
            throw new ArgumentException($"No XML files found in ReplayFolder: {options.Value.ReplayFolder}");
        }

    }
    public Task<Stream> GetEstimatedTimetableAsync(CancellationToken ct)
    {

        var filePath = _files[_currentFileIndex % _files.Length];
        _currentFileIndex++;

        return Task.FromResult<Stream>(File.OpenRead(filePath));
    }

}