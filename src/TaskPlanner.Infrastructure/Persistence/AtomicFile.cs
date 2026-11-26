namespace TaskPlanner.Infrastructure.Persistence;

/// <summary>
/// Writes a file so that readers only ever see the complete old or the complete new
/// version: the data goes to a temporary file in the same folder, is flushed to disk,
/// and then replaces the original in one step. A crash mid-write leaves the original intact.
/// </summary>
internal static class AtomicFile
{
    public static async Task WriteAsync(string filePath, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = filePath + ".tmp";

        try
        {
            await using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await write(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
