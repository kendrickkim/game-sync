using System.IO.Compression;

namespace GameSync.Services;

public static class ZipHelper
{
    public static long GetDirectoryContentMtime(string directory, IReadOnlyCollection<string>? excludes = null)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        long max = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (BackupExclude.IsExcluded(file, directory, excludes))
            {
                continue;
            }

            var ms = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds();
            if (ms > max)
            {
                max = ms;
            }
        }

        return max;
    }

    public static string CreateZipFromDirectory(
        string directory,
        IReadOnlyCollection<string>? excludes = null,
        string? tempDir = null)
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

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (BackupExclude.IsExcluded(file, directory, excludes))
                {
                    continue;
                }

                if (!BackupExclude.TryMakeRelative(directory, file, out var relative))
                {
                    continue;
                }

                var entryName = relative.Replace(Path.DirectorySeparatorChar, '/');
                zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            }
        }

        return zipPath;
    }

    public static void ExtractZipToDirectory(
        string zipPath,
        string targetDirectory,
        bool clearExisting,
        IReadOnlyCollection<string>? excludes = null)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Zip file not found", zipPath);
        }

        if (clearExisting && Directory.Exists(targetDirectory))
        {
            ClearDirectoryPreservingExcludes(targetDirectory, excludes);
        }

        Directory.CreateDirectory(targetDirectory);
        ZipFile.ExtractToDirectory(zipPath, targetDirectory, overwriteFiles: true);
    }

    private static void ClearDirectoryPreservingExcludes(string directory, IReadOnlyCollection<string>? excludes)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (BackupExclude.IsExcluded(file, directory, excludes))
            {
                continue;
            }

            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var dir in Directory
                     .EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (BackupExclude.IsExcluded(dir, directory, excludes))
            {
                continue;
            }

            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir, recursive: false);
            }
        }
    }
}
