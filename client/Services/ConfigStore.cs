using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameSync.Services;

public sealed class GameLocalSetting
{
    public string Path { get; set; } = "";

    /// <summary>
    /// Paths relative to <see cref="Path"/> that are skipped when creating a backup zip.
    /// Files and directories are both allowed.
    /// </summary>
    public List<string> ExcludeRelativePaths { get; set; } = new();
}

public sealed class AppConfig
{
    public string ServerUrl { get; set; } = "http://localhost:3000";
    public string? Token { get; set; }
    public string? Username { get; set; }

    /// <summary>Legacy path map. Kept in sync so older clients can still read the config.</summary>
    public Dictionary<int, string> GameLocalPaths { get; set; } = new();

    public Dictionary<int, GameLocalSetting> GameSettings { get; set; } = new();

    public GameLocalSetting GetOrCreateGameSetting(int gameId)
    {
        if (!GameSettings.TryGetValue(gameId, out var setting))
        {
            setting = new GameLocalSetting();
            if (GameLocalPaths.TryGetValue(gameId, out var legacyPath))
            {
                setting.Path = legacyPath;
            }

            GameSettings[gameId] = setting;
        }

        return setting;
    }

    public string GetGamePath(int gameId)
    {
        if (GameSettings.TryGetValue(gameId, out var setting) && !string.IsNullOrWhiteSpace(setting.Path))
        {
            return setting.Path;
        }

        return GameLocalPaths.TryGetValue(gameId, out var path) ? path : "";
    }

    public IReadOnlyList<string> GetGameExcludes(int gameId) =>
        GameSettings.TryGetValue(gameId, out var setting)
            ? setting.ExcludeRelativePaths
            : Array.Empty<string>();

    public void SetGamePath(int gameId, string? path)
    {
        var setting = GetOrCreateGameSetting(gameId);
        setting.Path = path?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(setting.Path))
        {
            GameLocalPaths.Remove(gameId);
            if (setting.ExcludeRelativePaths.Count == 0)
            {
                GameSettings.Remove(gameId);
            }

            return;
        }

        GameLocalPaths[gameId] = setting.Path;
    }

    public void SetGameExcludes(int gameId, IEnumerable<string> excludes)
    {
        var setting = GetOrCreateGameSetting(gameId);
        setting.ExcludeRelativePaths = BackupExclude.NormalizeList(setting.Path, excludes);
    }

    public void RemoveGameSetting(int gameId)
    {
        GameLocalPaths.Remove(gameId);
        GameSettings.Remove(gameId);
    }

    public void MigrateLegacyPaths()
    {
        foreach (var (gameId, path) in GameLocalPaths.ToList())
        {
            var setting = GetOrCreateGameSetting(gameId);
            if (string.IsNullOrWhiteSpace(setting.Path))
            {
                setting.Path = path;
            }
        }

        foreach (var (gameId, setting) in GameSettings.ToList())
        {
            if (!string.IsNullOrWhiteSpace(setting.Path))
            {
                GameLocalPaths[gameId] = setting.Path;
            }

            setting.ExcludeRelativePaths = BackupExclude.NormalizeList(setting.Path, setting.ExcludeRelativePaths);
        }
    }
}

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameSync");

    private static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            config.MigrateLegacyPaths();
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
