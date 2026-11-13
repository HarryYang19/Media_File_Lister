# 媒体文件清单（Windows 10 / 11）

一个完全本地运行的 Windows 桌面程序：扫描文件夹并生成 TXT、CSV 文件清单，同时为音频、照片和视频补充元数据。

**最终使用者不需要安装 Python、.NET Runtime、ExifTool、MediaInfo 或 FFmpeg。** GitHub Actions 会在构建时准备依赖，并将自包含应用与元数据工具一起打包成 ZIP。下载 ZIP、完整解压后，双击 `媒体文件清单.exe` 即可使用。ExifTool 和 MediaInfo 作为随包工具放在 `tools` 子目录中（ExifTool 还需要其配套文件夹），不是把所有第三方组件硬塞进一个 EXE。

## 功能

| 文件类型 | 清单附加内容 |
|---|---|
| MP3 / M4A / FLAC | 参与创作的艺术家（Artist，通常对应 ID3 TPE1 / Vorbis ARTIST / MP4 Artist）、专辑艺术家 / 乐队、专辑；有值时另列表演者、原始艺术家、作曲家与指挥 |
| JPG / JPEG / TIFF / PNG / HEIC 及常见 RAW | 优先读取原始拍摄时间，并搜索 EXIF、IPTC、XMP 等日期标签；同时尝试提取摄影者 / 作者 |
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

GitHub Actions 会下载并打包以下固定版本：

- ExifTool 13.63 Windows x64：官方项目页 <https://exiftool.sourceforge.net/>；官方下载目录 <https://sourceforge.net/projects/exiftool/files/>。
- MediaInfo CLI 26.10 Windows x64：MediaArea 官方 Windows 下载页 <https://mediaarea.net/en/MediaInfo/Download/Windows>；构建使用该页面对应的 CLI ZIP。

**下载可靠性与完整性校验：** ExifTool 的 SourceForge 发行文件在官方文件页公布 SHA-256：`7a4bff4b1d6ee15adef882837847f76c0ed23228d294feed285939b6c9b293bd`。工作流依次尝试多个 SourceForge 镜像和官方自动镜像地址；每次下载后先检查 ZIP 文件头、大小，再比对完整 SHA-256，只有完全匹配才会解压。MediaInfo CLI ZIP 同样固定 SHA-256：`4cc94bd272903914c04f4f9a9f53a6c94b5ea6c6454cf738d29fa182c41e8a29`（与 26.10 Windows x64 CLI 包的验证记录一致），失败时会重试，哈希不一致则立即终止。这样即使镜像返回 HTML 错误页，也不会误当作压缩包继续构建。

下载的工具放在 `src/MediaFileLister/tools/`，由 GitHub Actions 在 `dotnet publish` 完成后显式复制到发布目录。工具文件没有提交进源代码仓库，避免把体积较大的第三方二进制提交到 Git 历史中。工作流还会检查 ExifTool 配套的 `exiftool_files` 目录，以及两个命令行程序的实际版本号；另外会在含中文目录名和中文文件名的临时目录中运行 ExifTool UTF-8 参数流自检，确认 Windows 文件路径编码正常。

## 系统与限制

- 当前工作流发布的是 Windows x64 版本，目标为常见 Intel / AMD 64 位电脑；不包含 32 位或 ARM64 专用构建。
- Windows 11 x64 在支持范围内。需要特别说明：微软当前 .NET 10 支持表对 Windows 10 的正式支持仅列出 Enterprise / LTSC 版本；Windows 10 Home / Pro 即使可能在较新系统版本上运行这个自包含程序，也不属于微软列出的正式支持组合，因此不能保证所有 Win10 Home / Pro 设备均可运行。详见 <https://learn.microsoft.com/dotnet/core/install/windows>。
- 不要求目标电脑联网；程序运行期间不进行网络请求。
- 如果照片本身没有可识别的拍摄时间元数据，会显示“未记录”，不会用文件修改时间假冒拍摄时间。照片作者也只来自文件内的 Artist / Creator / By-line / Author 等标签。
- 视频码率优先读取容器总码率；若缺失，则尝试视频流码率，仍缺失时根据文件大小与时长估算并标注“估算”。
- 不支持的媒体格式仍会被列出，但不会附加对应元数据。
- 一些厂商私有 RAW 格式是否能读出具体标签，取决于 ExifTool 对该机型 MakerNotes 的支持程度。

## 隐私

所有分析都在本机完成。程序通过本地进程调用随包提供的 ExifTool / MediaInfo 读取元数据；ExifTool 文件路径通过 UTF-8 参数流传递，以兼容中文目录与文件名。不会上传文件内容、文件名、路径或扫描报告。GitHub Actions 只在构建阶段下载开源工具，不会接触用户日后扫描的媒体文件。


### 构建说明（工具文件打包）

GitHub Actions 会在下载第三方工具后执行 `dotnet publish`，随后将 `src/MediaFileLister/tools` 整个目录树显式复制到发布目录的 `tools` 子目录。这样不依赖 MSBuild 在项目加载时枚举动态下载文件，也不依赖单文件发布对外部工具的处理。打包前还会打开最终 ZIP，验证 EXE、ExifTool、ExifTool 的 `exiftool_files` 支持目录和 MediaInfo CLI 均实际存在。


### 元数据字段与诊断（本版更新）

音频字段按 Windows 的“参与创作的艺术家”语义读取 Artist（MP3 常见 ID3 TPE1、FLAC 常见 Vorbis ARTIST、M4A/MP4 常见 Artist）；专辑艺术家 / 乐队（AlbumArtist / Band）、表演者（Performer / InterpretedBy）、原始艺术家（OriginalArtist）、作曲家和指挥分别列出，不把这些不同角色混写成参与创作的艺术家。Windows 的 `System.Music.Artist` 是多值字符串属性，专辑艺术家则是独立的 `System.Music.AlbumArtist` 属性。

ExifTool 读取文件名时改用 UTF-8 参数流，并显式启用 `-charset filename=UTF8`，避免中文父目录或文件名在 Windows 命令行编码转换中损坏。日期读取从少数固定 EXIF 标签扩展为 ExifTool 的 `Time:All` 日期标签组，再按拍摄时间优先级挑选；同时将老式 EXIF `DateTime` 作为较低优先级回退，并读取常见 EXIF / IPTC / XMP 摄影者字段。扫描结束时会显示实际返回的元数据记录数和识别率；TXT 报告顶部也会附上识别统计和读取警告。若 ExifTool 没有返回 JSON、读取超时或某文件报错，会明确提示，而不是一律伪装成“未记录”。
