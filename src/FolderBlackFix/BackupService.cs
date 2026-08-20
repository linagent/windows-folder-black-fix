using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FolderBlackFix;

internal sealed class BackupService
{
    private readonly string _backupRoot;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public BackupService(string? backupRoot = null)
    {
        _backupRoot = backupRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderBlackFix",
            "Backups");
    }

    public string BackupRoot => _backupRoot;

    public async Task<FolderRepairResult> BackupAndDisableCustomizationAsync(string folder)
    {
        string normalized = NormalizeAndValidateFolder(folder);
        if (IsProtectedFolder(normalized, out string reason))
        {
            return new FolderRepairResult(normalized, false, false, false, false, "安全保护：" + reason);
        }

        string iniPath = Path.Combine(normalized, "desktop.ini");
        FileAttributes folderAttributes = File.GetAttributes(normalized);
        bool iniExists = File.Exists(iniPath);
        bool needsAttributeChange = (folderAttributes & (FileAttributes.ReadOnly | FileAttributes.System)) != 0;

        if (!iniExists && !needsAttributeChange)
        {
            return new FolderRepairResult(normalized, true, false, false, false,
                "未发现 desktop.ini 或自定义外观属性；已保留文件夹原状。 ");
        }

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string backupDir = Path.Combine(_backupRoot, GetFolderId(normalized), stamp);
        Directory.CreateDirectory(backupDir);

        var manifest = new BackupManifest
        {
            OriginalFolder = normalized,
            CreatedAt = DateTimeOffset.Now,
            OriginalFolderAttributes = (int)folderAttributes,
            DesktopIniExisted = iniExists
        };

        string manifestPath = Path.Combine(backupDir, "manifest.json");
        string? disabledPath = null;
        bool iniDisabled = false;
        bool attributesCleared = false;

        try
        {
            if (iniExists)
            {
                FileAttributes iniAttributes = File.GetAttributes(iniPath);
                string backupIniPath = Path.Combine(backupDir, "desktop.ini");
                File.Copy(iniPath, backupIniPath, overwrite: false);

                string originalHash = await ComputeSha256Async(iniPath);
                string backupHash = await ComputeSha256Async(backupIniPath);
                if (!string.Equals(originalHash, backupHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("desktop.ini 备份校验失败，未改动原文件。");
                }

                manifest.OriginalDesktopIniAttributes = (int)iniAttributes;
                manifest.BackupDesktopIniPath = backupIniPath;
                manifest.DesktopIniSha256 = originalHash;

                disabledPath = GetUniqueDisabledPath(normalized, stamp);
                manifest.DisabledDesktopIniPath = disabledPath;
            }

            await WriteManifestAsync(manifestPath, manifest);

            if (iniExists && disabledPath is not null)
            {
                File.Move(iniPath, disabledPath);
                iniDisabled = true;
            }

            FileAttributes cleared = folderAttributes & ~(FileAttributes.ReadOnly | FileAttributes.System);
            if (cleared != folderAttributes)
            {
                File.SetAttributes(normalized, cleared);
                attributesCleared = true;
            }

            manifest.State = "Completed";
            await WriteManifestAsync(manifestPath, manifest);

            string action = iniDisabled
                ? "已备份并停用 desktop.ini；不会删除其引用的图标文件。"
                : "未发现 desktop.ini；已备份并清除文件夹的自定义外观属性。";

            return new FolderRepairResult(normalized, true, true, iniDisabled, attributesCleared,
                action, backupDir, disabledPath);
        }
        catch (Exception ex)
        {
            manifest.State = iniDisabled ? "PartiallyCompleted" : "FailedBeforeChange";
            try { await WriteManifestAsync(manifestPath, manifest); } catch { }
            return new FolderRepairResult(normalized, false, true, iniDisabled, attributesCleared,
                "处理未完全完成：" + ex.Message, backupDir, disabledPath);
        }
    }

    public async Task<RestoreResult> RestoreLatestAsync(string folder)
    {
        string normalized = NormalizeAndValidateFolder(folder);
        if (!Directory.Exists(_backupRoot))
            return new RestoreResult(false, "尚未找到任何备份。 ");

        var candidates = new List<(string Path, BackupManifest Manifest)>();
        foreach (string manifestPath in Directory.EnumerateFiles(_backupRoot, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                BackupManifest? item = JsonSerializer.Deserialize<BackupManifest>(
                    await File.ReadAllTextAsync(manifestPath), JsonOptions);
                if (item is not null &&
                    string.Equals(Path.GetFullPath(item.OriginalFolder), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((manifestPath, item));
                }
            }
            catch { }
        }

        var selected = candidates.OrderByDescending(x => x.Manifest.CreatedAt).FirstOrDefault();
        if (selected.Manifest is null)
            return new RestoreResult(false, "没有找到该文件夹的备份记录。 ");

        BackupManifest manifest = selected.Manifest;
        try
        {
            string iniPath = Path.Combine(normalized, "desktop.ini");
            if (manifest.DesktopIniExisted)
            {
                if (string.IsNullOrWhiteSpace(manifest.BackupDesktopIniPath) ||
                    !File.Exists(manifest.BackupDesktopIniPath))
                {
                    return new RestoreResult(false, "备份中的 desktop.ini 不存在，未执行恢复。", selected.Path);
                }

                string backupHash = await ComputeSha256Async(manifest.BackupDesktopIniPath);
                if (!string.Equals(backupHash, manifest.DesktopIniSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new RestoreResult(false, "备份校验不一致，未执行恢复。", selected.Path);
                }

                if (File.Exists(iniPath))
                {
                    string preserved = Path.Combine(normalized,
                        $"desktop.ini.folder-black-fix-before-restore-{DateTime.Now:yyyyMMdd-HHmmss-fff}.bak");
                    File.Move(iniPath, preserved);
                }

                File.Copy(manifest.BackupDesktopIniPath, iniPath, overwrite: false);
                if (manifest.OriginalDesktopIniAttributes.HasValue)
                    File.SetAttributes(iniPath, (FileAttributes)manifest.OriginalDesktopIniAttributes.Value);
            }

            FileAttributes current = File.GetAttributes(normalized);
            FileAttributes originalAppearanceBits =
                (FileAttributes)manifest.OriginalFolderAttributes & (FileAttributes.ReadOnly | FileAttributes.System);
            File.SetAttributes(normalized,
                (current & ~(FileAttributes.ReadOnly | FileAttributes.System)) | originalAppearanceBits);

            manifest.State = "Restored";
            manifest.RestoredAt = DateTimeOffset.Now;
            await WriteManifestAsync(selected.Path, manifest);
            return new RestoreResult(true,
                "已恢复最近一次备份。为避免误删，之前停用的 .bak 副本仍保留。", selected.Path);
        }
        catch (Exception ex)
        {
            return new RestoreResult(false, "恢复失败：" + ex.Message, selected.Path);
        }
    }

    public static string NormalizeAndValidateFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("文件夹路径为空。", nameof(folder));
        string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Trim()));
        if (!Directory.Exists(normalized)) throw new DirectoryNotFoundException("文件夹不存在：" + normalized);
        return normalized;
    }

    private static bool IsProtectedFolder(string folder, out string reason)
    {
        string root = Path.GetPathRoot(folder) ?? string.Empty;
        if (string.Equals(Path.TrimEndingDirectorySeparator(folder), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
        {
            reason = "不处理磁盘根目录。";
            return true;
        }

        string[] protectedRoots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        ];

        foreach (string protectedRoot in protectedRoots.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedRoot));
            if (folder.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                folder.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"不处理系统或程序目录：{normalizedRoot}";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static string GetFolderId(string folder)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(folder.ToUpperInvariant()));
        return Convert.ToHexString(hash)[..16];
    }

    private static string GetUniqueDisabledPath(string folder, string stamp)
    {
        string candidate = Path.Combine(folder, $"desktop.ini.folder-black-fix-disabled-{stamp}.bak");
        int suffix = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(folder, $"desktop.ini.folder-black-fix-disabled-{stamp}-{suffix++}.bak");
        return candidate;
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static Task WriteManifestAsync(string path, BackupManifest manifest) =>
        File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, JsonOptions), new UTF8Encoding(false));
}
