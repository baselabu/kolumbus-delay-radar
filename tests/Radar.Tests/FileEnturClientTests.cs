using Microsoft.Extensions.Options;
using Radar.Collector;
using Radar.Collector.Entur;
using Xunit;

namespace Radar.Tests;
public class FileEnturClientTests
{

    private static async Task<string> ReadNextFileContentAsync(FileEnturClient client, CancellationToken ct)
    {
        using var stream = await client.GetEstimatedTimetableAsync(ct);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
    [Fact]
    public async Task Files_are_returned_in_order_and_wrap_around()
    {
        var dir = Directory.CreateTempSubdirectory("entur-replay");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "file1.xml"), "A");
            File.WriteAllText(Path.Combine(dir.FullName, "file2.xml"), "B");

            var options = Options.Create(new CollectorOptions
            {
                ReplayFolder = dir.FullName
            });

            var client = new FileEnturClient(options);
            var result1 = await ReadNextFileContentAsync(client, CancellationToken.None);
            var result2 = await ReadNextFileContentAsync(client, CancellationToken.None);
            var result3 = await ReadNextFileContentAsync(client, CancellationToken.None);

            //check order and wrap around
            Assert.Equal("A",  result1);
            Assert.Equal("B",  result2);
            Assert.Equal("A",  result3);
        }
        finally
        {
            dir.Delete(recursive:true);
        }

    }

    [Fact]
    public void Empty_folder_throws_exception()
    {
        var dir = Directory.CreateTempSubdirectory("entur-replay");
        try
        {
            var options = Options.Create(new CollectorOptions
            {
                ReplayFolder = dir.FullName
            });

            Assert.Throws<ArgumentException>(() => new FileEnturClient(options));
        }
        finally
        {
            dir.Delete(recursive:true);
        }
    }
}