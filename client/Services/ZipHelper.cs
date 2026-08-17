using System.IO.Compression;

namespace GameSync.Services;

public static class ZipHelper
{
    public static long GetDirectoryContentMtime(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        long max = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var ms = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds();
            if (ms > max)
            {
                max = ms;
            }
        }

        return max;
    }

    public static string CreateZipFromDirectory(string directory, string? tempDir = null)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory}");
        }

        tempDir ??= Path.GetTempPath();
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, $"gamesync-{Guid.NewGuid():N}.zip");

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(directory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return zipPath;
    }

    public static void ExtractZipToDirectory(string zipPath, string targetDirectory, bool clearExisting)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Zip file not found", zipPath);
        }

        if (clearExisting && Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);
        ZipFile.ExtractToDirectory(zipPath, targetDirectory, overwriteFiles: true);
    }
}
