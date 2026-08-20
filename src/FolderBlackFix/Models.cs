using System.Text.Json.Serialization;

namespace FolderBlackFix;

internal sealed class BackupManifest
{
    public int FormatVersion { get; set; } = 1;
    public string OriginalFolder { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public int OriginalFolderAttributes { get; set; }
    public bool DesktopIniExisted { get; set; }
    public int? OriginalDesktopIniAttributes { get; set; }
    public string? BackupDesktopIniPath { get; set; }
    public string? DisabledDesktopIniPath { get; set; }
    public string? DesktopIniSha256 { get; set; }
    public string State { get; set; } = "Prepared";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? RestoredAt { get; set; }
}

internal sealed record FolderRepairResult(
    string Folder,
    bool Success,
    bool BackupCreated,
    bool DesktopIniDisabled,
    bool FolderAttributesCleared,
    string Message,
    string? BackupDirectory = null,
    string? DisabledIniPath = null);

internal sealed record RestoreResult(bool Success, string Message, string? ManifestPath = null);

internal sealed record CacheRepairResult(
    bool Success,
    int DeletedCount,
    int MissingCount,
    IReadOnlyList<string> FailedFiles,
    bool ExplorerRestarted,
    string Message);
