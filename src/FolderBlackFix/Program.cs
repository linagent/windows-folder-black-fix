namespace FolderBlackFix;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
            Environment.ExitCode = SelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }

        int previewIndex = Array.FindIndex(args, a => a.Equals("--render-preview", StringComparison.OrdinalIgnoreCase));
        if (previewIndex >= 0 && previewIndex + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            Environment.ExitCode = RenderPreview(args[previewIndex + 1]);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static int RenderPreview(string outputPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using var form = new MainForm
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-12000, -12000),
                ShowInTaskbar = false
            };
            form.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
            form.Close();
            Console.WriteLine("PREVIEW SAVED: " + fullPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("PREVIEW FAILED: " + ex);
            return 1;
        }
    }
}

internal static class SelfTest
{
    public static async Task<int> RunAsync()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "FolderBlackFix-SelfTest-" + Guid.NewGuid().ToString("N"));
        string targetFolder = Path.Combine(testRoot, "示例文件夹");
        string backupRoot = Path.Combine(testRoot, "Backups");

        try
        {
            Directory.CreateDirectory(targetFolder);
            string desktopIni = Path.Combine(targetFolder, "desktop.ini");
            string expectedContent = "[.ShellClassInfo]\r\nIconResource=sample.ico,0\r\n";
            string personalFile = Path.Combine(targetFolder, "我的照片说明.txt");
            string personalContent = "这是个人文件，不应被修改。";
            await File.WriteAllTextAsync(personalFile, personalContent);
            string childFolder = Path.Combine(targetFolder, "子文件夹");
            Directory.CreateDirectory(childFolder);
            string childIni = Path.Combine(childFolder, "desktop.ini");
            await File.WriteAllTextAsync(childIni, "child-settings");
            await File.WriteAllTextAsync(desktopIni, expectedContent, System.Text.Encoding.Unicode);
            File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(targetFolder, File.GetAttributes(targetFolder) | FileAttributes.ReadOnly);

            var service = new BackupService(backupRoot);
            FolderRepairResult repair = await service.BackupAndDisableCustomizationAsync(targetFolder);
            Assert(repair.Success, "备份并停用应成功");
            Assert(!File.Exists(desktopIni), "原 desktop.ini 应已停用");
            Assert(repair.BackupCreated, "应已建立备份");
            Assert(repair.DisabledIniPath is not null && File.Exists(repair.DisabledIniPath), "停用副本应保留在原文件夹");
            Assert(await File.ReadAllTextAsync(personalFile) == personalContent, "个人文件不得被修改");
            Assert(await File.ReadAllTextAsync(childIni) == "child-settings", "不得递归处理子文件夹");

            RestoreResult restore = await service.RestoreLatestAsync(targetFolder);
            Assert(restore.Success, "恢复应成功");
            Assert(File.Exists(desktopIni), "desktop.ini 应已恢复");
            string actualContent = await File.ReadAllTextAsync(desktopIni, System.Text.Encoding.Unicode);
            Assert(actualContent == expectedContent, "恢复内容应与原文件一致");
            Assert(File.GetAttributes(desktopIni).HasFlag(FileAttributes.Hidden), "隐藏属性应恢复");

            string newerContent = "[.ShellClassInfo]\r\nInfoTip=新的设置\r\n";
            File.SetAttributes(desktopIni, FileAttributes.Normal);
            await File.WriteAllTextAsync(desktopIni, newerContent, System.Text.Encoding.Unicode);
            RestoreResult secondRestore = await service.RestoreLatestAsync(targetFolder);
            Assert(secondRestore.Success, "存在新 desktop.ini 时仍应安全恢复");
            string[] preserved = Directory.GetFiles(targetFolder, "desktop.ini.folder-black-fix-before-restore-*.bak");
            Assert(preserved.Length == 1, "恢复前的新 desktop.ini 应改名保留");
            Assert(await File.ReadAllTextAsync(preserved[0], System.Text.Encoding.Unicode) == newerContent, "保留的新设置内容应不变");

            FolderRepairResult protectedResult = await service.BackupAndDisableCustomizationAsync(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            Assert(!protectedResult.Success && protectedResult.Message.StartsWith("安全保护："), "系统目录必须被安全保护拒绝");

            using var form = new MainForm();
            Assert(form.ValidateUiContract(out string uiProblem), "界面交互契约失败：" + uiProblem);

            Console.WriteLine("SELF-TEST PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SELF-TEST FAILED: " + ex);
            return 1;
        }
        finally
        {
            string fullRoot = Path.GetFullPath(testRoot);
            string tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(fullRoot).StartsWith("FolderBlackFix-SelfTest-", StringComparison.Ordinal))
            {
                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(fullRoot, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(entry, FileAttributes.Normal); } catch { }
                    }
                    Directory.Delete(fullRoot, recursive: true);
                }
                catch { }
            }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
