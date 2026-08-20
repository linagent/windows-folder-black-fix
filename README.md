# 文件夹黑块修复

一个简单、可恢复的 Windows 小工具，用来修复资源管理器中文件夹缩略图或图标出现黑色背景、黑块的问题。

![界面预览](docs/ui-preview.png)

## 下载

前往 [Releases](https://github.com/linagent/windows-folder-black-fix/releases/latest) 下载 `FolderBlackFix-v1.0.0-win-x64.exe`。

这是 Windows 10/11 64 位单文件版，已包含 .NET 8 运行环境，无需安装。EXE 没有商业代码签名证书；下载后可用 Release 中的 `SHA256.txt` 校验完整性。

## 使用

1. 把出现黑块的文件夹拖进窗口，或点“选择文件夹”。
2. 点“一键修复”。
3. 文件夹窗口短暂消失并恢复后，重新打开原文件夹查看。

没有需要配置的选项，也不会重启电脑。

## 安全设计

- 只处理用户明确选择的文件夹，不递归修改子文件夹。
- 不处理磁盘根目录、Windows、Program Files、Program Files (x86) 和 ProgramData。
- `desktop.ini` 先备份并进行 SHA-256 校验，再改名停用。
- 不删除照片、视频、文档、自定义 `.ico` 图标或其他个人文件。
- 只清理当前用户的 Windows 图标/缩略图自动缓存。
- 备份默认位于 `%LOCALAPPDATA%\FolderBlackFix\Backups`，界面提供“恢复上次修改”。
- 资源管理器的重启放在 `finally` 保护流程中；失败项会显示为提示，不会被误报为成功。
- 不联网、不上传数据、不安装服务、不修改注册表。

## 适用范围

本工具针对以下常见原因：

- `desktop.ini` 或自定义文件夹图标配置异常；
- Windows 图标缓存或缩略图缓存损坏；
- 资源管理器没有及时刷新外观。

如果黑块同时出现在桌面、任务栏和多个应用中，问题更可能来自显卡驱动或系统渲染，本工具不能替代那类排查。

## 从源码构建

要求：Windows 10/11、.NET 8 SDK。

```powershell
cd src/FolderBlackFix
dotnet restore -p:NuGetAudit=false
dotnet build -c Release --no-restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --no-restore
```

无破坏自检：

```powershell
dotnet run -c Release -- --self-test
```

自检只使用系统临时目录，不重启资源管理器，不清理真实缓存；它验证备份/恢复、个人文件保持不变、非递归边界、系统目录保护和主界面交互契约。

## 技术栈

- C# / .NET 8
- Windows Forms
- 无第三方 NuGet 依赖

## 许可证

[MIT License](LICENSE)
