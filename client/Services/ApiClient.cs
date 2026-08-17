using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameSync.Models;

namespace GameSync.Services;

public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private string? _token;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(10),
        };
    }

    public void SetToken(string? token)
    {
        _token = token;
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public void UpdateBaseUrl(string baseUrl)
    {
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public Task<AuthResponse> LoginAsync(string username, string password, CancellationToken ct = default) =>
        PostAuthAsync("auth/login", username, password, ct);

    public Task<AuthResponse> RegisterAsync(string username, string password, CancellationToken ct = default) =>
        PostAuthAsync("auth/register", username, password, ct);

    private async Task<AuthResponse> PostAuthAsync(string path, string username, string password, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(path, new { username, password }, ct);
        return await ReadAsync<AuthResponse>(response, ct);
    }

    public async Task<List<GameInfo>> GetGamesAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("games", ct);
        return await ReadAsync<List<GameInfo>>(response, ct);
    }

    public async Task<GameInfo> CreateGameAsync(string name, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("games", new { name }, ct);
        return await ReadAsync<GameInfo>(response, ct);
    }

    public async Task DeleteGameAsync(int gameId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"games/{gameId}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<ComputerInfo> RegisterComputerAsync(string name, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("computers", new { name }, ct);
        return await ReadAsync<ComputerInfo>(response, ct);
    }

    public async Task<List<SyncEntry>> GetSyncListAsync(int? gameId = null, CancellationToken ct = default)
    {
        var url = gameId is null ? "sync/list" : $"sync/list?gameId={gameId.Value}";
        var response = await _http.GetAsync(url, ct);
        return await ReadAsync<List<SyncEntry>>(response, ct);
    }

    public async Task<SyncEntry> UploadAsync(
        int gameId,
        string computerName,
        string localPath,
        long contentMtime,
        string zipFilePath,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(gameId.ToString()), "gameId");
        content.Add(new StringContent(computerName), "computerName");
        content.Add(new StringContent(localPath), "localPath");
        content.Add(new StringContent(contentMtime.ToString()), "contentMtime");

        await using var stream = File.OpenRead(zipFilePath);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", Path.GetFileName(zipFilePath));

        var response = await _http.PostAsync("sync/upload", content, ct);
        return await ReadAsync<SyncEntry>(response, ct);
    }

    public async Task DownloadToFileAsync(int entryId, string destinationPath, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"sync/download/{entryId}", HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessAsync(response, ct);

        await using var network = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(destinationPath);
        await network.CopyToAsync(file, ct);
    }

    public async Task DeleteSyncEntryAsync(int entryId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"sync/{entryId}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        var value = JsonSerializer.Deserialize<T>(body, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Empty response from server");
        }

        return value;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw CreateApiException(response.StatusCode, body);
    }

    private static Exception CreateApiException(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            var err = JsonSerializer.Deserialize<ApiError>(body, JsonOptions);
            if (!string.IsNullOrWhiteSpace(err?.Error))
            {
                return new InvalidOperationException($"{(int)status}: {err.Error}");
            }
        }
        catch
        {
            // fall through
        }

        return new InvalidOperationException($"{(int)status}: {Truncate(body)}");
    }

    private static string Truncate(string value, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Request failed";
        }

        return value.Length <= max ? value : value[..max] + "...";
    }

    public void Dispose() => _http.Dispose();
}
