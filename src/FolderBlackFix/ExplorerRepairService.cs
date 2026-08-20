using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FolderBlackFix;

internal static class ExplorerRepairService
{
    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNE_UPDATEITEM = 0x00002000;
    private const uint SHCNF_IDLIST = 0x0000;
    private const uint SHCNF_PATHW = 0x0005;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, string? dwItem1, IntPtr dwItem2);

    public static void RefreshFolder(string folder)
    {
        SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, folder, IntPtr.Zero);
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, null, IntPtr.Zero);
    }

    public static async Task<CacheRepairResult> RebuildCachesAndRestartExplorerAsync(Action<string> log)
    {
        bool explorerWasRunning = Process.GetProcessesByName("explorer").Length > 0;
        bool explorerRestarted = false;
        int deleted = 0;
        int missing = 0;
        var failures = new List<string>();

        try
        {
            log("正在暂停资源管理器，任务栏和已打开的文件夹窗口会短暂消失……");
            await StopExplorerAsync(log);
            await RunIe4uinitAsync("-ClearIconCache", log);

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(local, "IconCache.db")
            };

            string explorerCacheDir = Path.Combine(local, "Microsoft", "Windows", "Explorer");
            if (Directory.Exists(explorerCacheDir))
            {
                foreach (string file in Directory.EnumerateFiles(explorerCacheDir, "iconcache*.db", SearchOption.TopDirectoryOnly))
                    cacheFiles.Add(file);
                foreach (string file in Directory.EnumerateFiles(explorerCacheDir, "thumbcache*.db", SearchOption.TopDirectoryOnly))
                    cacheFiles.Add(file);
            }

            foreach (string file in cacheFiles)
            {
                if (!File.Exists(file))
                {
                    missing++;
                    continue;
                }

                bool removed = false;
                Exception? lastError = null;
                for (int attempt = 0; attempt < 3 && !removed; attempt++)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        removed = true;
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        await Task.Delay(250);
                    }
                }

                if (!removed)
                    failures.Add($"{Path.GetFileName(file)}：{lastError?.Message ?? "无法删除"}");
            }
        }
        catch (Exception ex)
        {
            failures.Add("缓存处理：" + ex.Message);
        }
        finally
        {
            if (explorerWasRunning)
            {
                log("正在重新启动资源管理器……");
                explorerRestarted = await EnsureExplorerRunningAsync(log);
            }

            await RunIe4uinitAsync("-show", log);
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, null, IntPtr.Zero);
        }

        bool success = failures.Count == 0 && (!explorerWasRunning || explorerRestarted);
        string message = success
            ? $"缓存重建完成：已清理 {deleted} 个缓存文件，资源管理器已恢复。"
            : $"缓存修复完成但有 {failures.Count} 个警告；已清理 {deleted} 个缓存文件。";
        return new CacheRepairResult(success, deleted, missing, failures, explorerRestarted, message);
    }

    private static async Task StopExplorerAsync(Action<string> log)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            Process[] processes = Process.GetProcessesByName("explorer");
            if (processes.Length == 0) return;

            foreach (Process process in processes)
            {
                try
                {
                    process.Kill(entireProcessTree: false);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    log($"提示：结束一个资源管理器进程时出现 {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
            await Task.Delay(350);
        }
    }

    private static async Task<bool> EnsureExplorerRunningAsync(Action<string> log)
    {
        try
        {
            if (Process.GetProcessesByName("explorer").Length == 0)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                    UseShellExecute = true
                });
            }

            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(300);
                if (Process.GetProcessesByName("explorer").Length > 0) return true;
            }
        }
        catch (Exception ex)
        {
            log("资源管理器自动重启失败：" + ex.Message);
        }
        return false;
    }

    private static async Task RunIe4uinitAsync(string argument, Action<string> log)
    {
        try
        {
            string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ie4uinit.exe");
            if (!File.Exists(executable)) return;
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = argument,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is not null)
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            log("图标缓存刷新命令未完成：" + ex.Message);
        }
    }
}
