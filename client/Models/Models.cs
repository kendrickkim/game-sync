using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameSync.Models;

public sealed class AuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("user")]
    public UserInfo User { get; set; } = new();
}

public sealed class UserInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
}

public sealed class GameInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    public override string ToString() => Name;
}

public sealed class ComputerInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("lastSeenAt")]
    [JsonConverter(typeof(FlexibleNullableDateTimeConverter))]
    public DateTime? LastSeenAt { get; set; }

    [JsonPropertyName("isOnline")]
    public int IsOnlineValue { get; set; }

    [JsonIgnore]
    public bool IsOnline => IsOnlineValue == 1;

    public override string ToString() => $"{Name} {(IsOnline ? "(온라인)" : "(오프라인)")}";
}

public sealed class RemoteUploadRequest
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("gameName")]
    public string GameName { get; set; } = "";

    [JsonPropertyName("requesterComputerName")]
    public string RequesterComputerName { get; set; } = "";

    [JsonPropertyName("targetComputerId")]
    public int TargetComputerId { get; set; }

    [JsonPropertyName("targetComputerName")]
    public string TargetComputerName { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("syncEntryId")]
    public int? SyncEntryId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(FlexibleNullableDateTimeConverter))]
    public DateTime? CreatedAt { get; set; }
}

public sealed class SyncEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("gameName")]
    public string GameName { get; set; } = "";

    [JsonPropertyName("computerId")]
    public int ComputerId { get; set; }

    [JsonPropertyName("computerName")]
    public string ComputerName { get; set; } = "";

    [JsonPropertyName("localPath")]
    public string LocalPath { get; set; } = "";

    [JsonPropertyName("zipFilename")]
    public string ZipFilename { get; set; } = "";

    [JsonPropertyName("contentMtime")]
    public long ContentMtime { get; set; }

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(FlexibleNullableDateTimeConverter))]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    [JsonConverter(typeof(FlexibleNullableDateTimeConverter))]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ApiError
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = "Unknown error";
}

/// <summary>
/// Accepts ISO-8601 and SQLite datetime('now') style strings like "2026-08-11 18:06:00".
/// </summary>
public sealed class FlexibleNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly string[] Formats =
    {
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
    };

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (DateTime.TryParseExact(
                    text,
                    Formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var exact))
            {
                return exact.ToLocalTime();
            }

            if (DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed.ToLocalTime();
            }

            throw new JsonException($"Unsupported datetime format: '{text}'");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var unixMs))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
        }

        throw new JsonException($"Unexpected token for DateTime?: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
    }
}
