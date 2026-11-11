# 媒体文件清单（Windows 10 / 11）

一个完全本地运行的 Windows 桌面程序：扫描文件夹并生成 TXT、CSV 文件清单，同时为音频、照片和视频补充元数据。

**最终使用者不需要安装 Python、.NET Runtime、ExifTool、MediaInfo 或 FFmpeg。** GitHub Actions 会在构建时准备依赖，并将自包含应用与元数据工具一起打包成 ZIP。下载 ZIP、完整解压后，双击 `媒体文件清单.exe` 即可使用。ExifTool 和 MediaInfo 作为随包工具放在 `tools` 子目录中（ExifTool 还需要其配套文件夹），不是把所有第三方组件硬塞进一个 EXE。

## 功能

| 文件类型 | 清单附加内容 |
|---|---|
| MP3 / M4A / FLAC | 艺术家（Artist / Performer / Album Artist）与专辑（Album） |
| JPG / JPEG / TIFF 及常见 RAW | EXIF 拍摄时间；依次尝试原始拍摄时间、数字化时间、创建时间等元数据 |
| MP4 / MOV / MKV / AVI 等视频 | 容器类型、视频编码、总码率；必要时回退视频流码率或注明估算值 |
| 其他文件 | 文件名、相对路径、扩展名、文件大小 |

程序提供图形界面，可选择目录，并选择是否递归扫描子文件夹。扫描完成后在目标文件夹写入：

- `文件清单.txt`：易读的逐行清单。
- `文件清单.csv`：可由 Excel、LibreOffice 等表格程序打开。

已有的同名清单会被覆盖；清单文件自身不会被再次列入扫描结果。程序只读取源文件，不修改、重命名或删除媒体文件。

## 如何在 GitHub 上构建便携版

1. 在 GitHub 新建一个空仓库。
2. 将本仓库 ZIP 解压。
3. 用 GitHub Desktop 克隆刚创建的空仓库，再把解压后**里面的所有文件和文件夹**复制到仓库根目录（尤其要保留 `.github/workflows` 工作流目录），然后提交并推送。特别注意：`.github` 是必需目录，不能遗漏，否则 GitHub 不会出现自动构建工作流。
4. 提交到 `main` 或 `master` 分支。
5. 打开仓库的 **Actions** 页面，进入 **Build Windows portable ZIP** 工作流。
6. 等待该次工作流成功后，打开运行记录页面最下方的 **Artifacts**，下载 `MediaFileLister-Windows-x64`。
7. 解压生成的便携 ZIP，双击 `媒体文件清单.exe`。

也可以在 Actions 页面手动运行 `workflow_dispatch`。若创建并推送 `v1.0.0` 这类 `v*` 标签，工作流还会自动把 ZIP 附加到对应的 GitHub Release。

## 本地开发

本 ZIP 是 GitHub 仓库源码，不是已经编译的成品 EXE。首次推送到 GitHub 后，需等 Actions 工作流完成，再从该次运行的 Artifacts 下载真正给最终用户使用的 Windows 便携 ZIP。

项目使用 C# / Windows Forms / .NET 10，当前工作流目标是 `win-x64`。构建机由 GitHub Actions 安装 .NET 10 SDK，并以 self-contained 单文件方式发布；最终用户不需要安装 .NET Runtime。

GitHub Actions 会从官方地址下载并打包：

- ExifTool 13.59 Windows x64：<https://exiftool.org/>
- MediaInfo CLI 26.10 Windows x64：<https://mediaarea.net/MediaInfo/Download/Windows>

下载的工具放在 `src/MediaFileLister/tools/`，通过 `.csproj` 发布设置复制到输出目录。工具文件没有提交进源代码仓库，避免把体积较大的第三方二进制提交到 Git 历史中。

## 系统与限制

- 当前工作流发布的是 Windows x64 版本，目标为常见 Intel / AMD 64 位电脑；不包含 32 位或 ARM64 专用构建。
- Windows 11 x64 在支持范围内。需要特别说明：微软当前 .NET 10 支持表对 Windows 10 的正式支持仅列出 Enterprise / LTSC 版本；Windows 10 Home / Pro 即使可能在较新系统版本上运行这个自包含程序，也不属于微软列出的正式支持组合，因此不能保证所有 Win10 Home / Pro 设备均可运行。详见 <https://learn.microsoft.com/dotnet/core/install/windows>。
- 不要求目标电脑联网；程序运行期间不进行网络请求。
- 如果照片本身没有拍摄时间元数据，会显示“未记录”，不会用文件修改时间假冒拍摄时间。
- 视频码率优先读取容器总码率；若缺失，则尝试视频流码率，仍缺失时根据文件大小与时长估算并标注“估算”。
- 不支持的媒体格式仍会被列出，但不会附加对应元数据。
- 一些厂商私有 RAW 格式是否能读出具体标签，取决于 ExifTool 对该机型 MakerNotes 的支持程度。

## 隐私

所有分析都在本机完成。程序把文件路径作为参数交给随包提供的 ExifTool / MediaInfo 读取元数据，不会上传文件内容、文件名、路径或扫描报告。GitHub Actions 只在构建阶段下载开源工具，不会接触用户日后扫描的媒体文件。
