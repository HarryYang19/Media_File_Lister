using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MediaFileLister;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox _folderBox = new();
    private readonly CheckBox _recursiveBox = new();
    private readonly Button _browseButton = new();
    private readonly Button _startButton = new();
    private readonly Button _openTxtButton = new();
    private readonly Button _openCsvButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly ListBox _logBox = new();
    private string? _txtPath;
    private string? _csvPath;

    public MainForm()
    {
        Text = "媒体文件清单";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 620);
        Size = new Size(900, 650);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(248, 249, 251);

        var title = new Label
        {
            Text = "媒体文件清单",
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };
        var subtitle = new Label
        {
            Text = "读取当前文件夹的本地媒体元数据，生成 TXT 和 CSV 清单；不会上传或修改原文件。",
            AutoSize = true,
            ForeColor = Color.FromArgb(90, 96, 105),
            Location = new Point(27, 58)
        };
        Controls.Add(title);
        Controls.Add(subtitle);

        var folderLabel = new Label { Text = "扫描文件夹", AutoSize = true, Location = new Point(27, 104) };
        _folderBox.Location = new Point(27, 130);
        _folderBox.Size = new Size(710, 30);
        _folderBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _folderBox.Text = Environment.CurrentDirectory;
        _browseButton.Text = "选择文件夹…";
        _browseButton.Location = new Point(748, 128);
        _browseButton.Size = new Size(120, 32);
        _browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _browseButton.Click += BrowseFolder;
        Controls.Add(folderLabel);
        Controls.Add(_folderBox);
        Controls.Add(_browseButton);

        _recursiveBox.Text = "同时扫描所有子文件夹（递归）";
        _recursiveBox.AutoSize = true;
        _recursiveBox.Location = new Point(29, 178);
        Controls.Add(_recursiveBox);

        _startButton.Text = "开始扫描并生成清单";
        _startButton.Location = new Point(27, 215);
        _startButton.Size = new Size(190, 40);
        _startButton.BackColor = Color.FromArgb(35, 100, 180);
        _startButton.ForeColor = Color.White;
        _startButton.FlatStyle = FlatStyle.Flat;
        _startButton.FlatAppearance.BorderSize = 0;
        _startButton.Click += async (_, _) => await StartScanAsync();
        Controls.Add(_startButton);

        _openTxtButton.Text = "打开 TXT";
        _openTxtButton.Location = new Point(232, 215);
        _openTxtButton.Size = new Size(105, 40);
        _openTxtButton.Enabled = false;
        _openTxtButton.Click += (_, _) => OpenReport(_txtPath);
        Controls.Add(_openTxtButton);

        _openCsvButton.Text = "打开 CSV";
        _openCsvButton.Location = new Point(348, 215);
        _openCsvButton.Size = new Size(105, 40);
        _openCsvButton.Enabled = false;
        _openCsvButton.Click += (_, _) => OpenReport(_csvPath);
        Controls.Add(_openCsvButton);

        _progressBar.Location = new Point(27, 278);
        _progressBar.Size = new Size(841, 16);
        _progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_progressBar);

        _statusLabel.Text = "请选择要扫描的文件夹，然后开始。";
        _statusLabel.AutoSize = false;
        _statusLabel.Location = new Point(28, 302);
        _statusLabel.Size = new Size(840, 24);
        _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_statusLabel);

        var logLabel = new Label { Text = "运行记录", AutoSize = true, Location = new Point(28, 340) };
        Controls.Add(logLabel);
        _logBox.Location = new Point(27, 365);
        _logBox.Size = new Size(841, 190);
        _logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _logBox.HorizontalScrollbar = true;
        Controls.Add(_logBox);

        var privacy = new Label
        {
            Text = "完全本地处理 · 无需安装 Python / .NET / ExifTool / MediaInfo · 不联网",
            AutoSize = true,
            ForeColor = Color.FromArgb(105, 112, 122),
            Location = new Point(28, 565),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        Controls.Add(privacy);
    }

    private void BrowseFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择需要生成文件清单的文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = Directory.Exists(_folderBox.Text) ? _folderBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _folderBox.Text = dialog.SelectedPath;
    }

    private async Task StartScanAsync()
    {
        var folder = _folderBox.Text.Trim().Trim('"');
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "文件夹不存在，请重新选择。", "路径错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _progressBar.Value = 0;
        _logBox.Items.Clear();
        _txtPath = null;
        _csvPath = null;
        _openTxtButton.Enabled = false;
        _openCsvButton.Enabled = false;
        AddLog($"开始扫描：{folder}");
        AddLog(_recursiveBox.Checked ? "扫描模式：包含子文件夹" : "扫描模式：仅当前文件夹");

        var progress = new Progress<ScanProgress>(p =>
        {
            _statusLabel.Text = p.Message;
            _progressBar.Maximum = Math.Max(1, p.Total);
            _progressBar.Value = Math.Clamp(p.Completed, 0, _progressBar.Maximum);
            if (p.LogLine is not null) AddLog(p.LogLine);
        });

        try
        {
            var result = await Task.Run(() => ScanService.Run(folder, _recursiveBox.Checked, progress));
            _txtPath = result.TxtPath;
            _csvPath = result.CsvPath;
            _openTxtButton.Enabled = File.Exists(_txtPath);
            _openCsvButton.Enabled = File.Exists(_csvPath);
            _progressBar.Value = _progressBar.Maximum;
            _statusLabel.Text = $"完成：共列出 {result.Files.Count:N0} 个文件。报告已保存到所选文件夹。";
            AddLog($"音频：{result.AudioCount}；照片：{result.PhotoCount}；视频：{result.VideoCount}；其他：{result.OtherCount}");
            AddLog($"TXT：{result.TxtPath}");
            AddLog($"CSV：{result.CsvPath}");
            MessageBox.Show(this,
                $"清单已生成。\n\n文件数：{result.Files.Count:N0}\n\nTXT：{result.TxtPath}\nCSV：{result.CsvPath}",
                "扫描完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "扫描未能完成。";
            AddLog("错误：" + ex.Message);
            MessageBox.Show(this, ex.Message, "扫描失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _startButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _folderBox.Enabled = !busy;
        _recursiveBox.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void AddLog(string line)
    {
        _logBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
        if (_logBox.Items.Count > 0)
            _logBox.TopIndex = _logBox.Items.Count - 1;
    }

    private void OpenReport(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}

internal sealed record ScanProgress(int Completed, int Total, string Message, string? LogLine = null);
internal sealed record FileEntry(string FileName, string RelativePath, string Category, string Detail, string Extension, long SizeBytes);
internal sealed record ScanResult(List<FileEntry> Files, string TxtPath, string CsvPath, int AudioCount, int PhotoCount, int VideoCount, int OtherCount);
internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);

internal static class ScanService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".m4a", ".flac" };

    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".tif", ".tiff", ".png", ".bmp", ".webp", ".heic", ".heif", ".jxl",
        ".arw", ".srf", ".sr2", ".crw", ".cr2", ".cr3", ".nef", ".nrw", ".orf", ".rw2", ".raw",
        ".pef", ".ptx", ".raf", ".rwl", ".dng", ".3fr", ".ari", ".bay", ".cap", ".data",
        ".dcs", ".dcr", ".drf", ".eip", ".erf", ".fff", ".gpr", ".iiq", ".k25", ".kdc",
        ".mdc", ".mef", ".mos", ".mrw", ".obm", ".qtk", ".rwz", ".srw", ".x3f", ".pxn"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".mkv", ".avi", ".wmv", ".flv", ".webm", ".3gp", ".3g2",
        ".mts", ".m2ts", ".ts", ".mpg", ".mpeg", ".vob", ".ogv", ".asf", ".divx", ".mxf",
        ".m2v", ".rm", ".rmvb", ".y4m", ".dv", ".wtv", ".f4v", ".mpe", ".mpv", ".roq"
    };

    private static readonly string[] ExifTags =
    {
        "-Artist", "-Album", "-AlbumArtist", "-Performer", "-Composer", "-Conductor",
        "-DateTimeOriginal", "-SubSecDateTimeOriginal", "-CreateDate", "-DateTimeDigitized",
        "-MediaCreateDate", "-DateCreated", "-OffsetTimeOriginal"
    };

    public static ScanResult Run(string folder, bool recursive, IProgress<ScanProgress> progress)
    {
        folder = Path.GetFullPath(folder);
        var exifTool = FindTool("exiftool.exe");
        var mediaInfo = FindTool("MediaInfo.exe") ?? FindTool("mediainfo.exe");
        if (exifTool is null)
            throw new FileNotFoundException("缺少随程序打包的 ExifTool。请从 GitHub Actions 下载完整的 Windows ZIP，不要只复制主 EXE。", "tools\\ExifTool\\exiftool.exe");
        if (mediaInfo is null)
            throw new FileNotFoundException("缺少随程序打包的 MediaInfo CLI。请从 GitHub Actions 下载完整的 Windows ZIP。", "tools\\MediaInfo\\MediaInfo.exe");

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };
        var files = Directory.EnumerateFiles(folder, "*", options)
            .Where(path => !IsReportFile(folder, path))
            .OrderBy(path => Path.GetRelativePath(folder, path), NaturalStringComparer.Instance)
            .ToList();

        var audioFiles = files.Where(p => AudioExtensions.Contains(Path.GetExtension(p))).ToList();
        var photoFiles = files.Where(p => PhotoExtensions.Contains(Path.GetExtension(p))).ToList();
        var videoFiles = files.Where(p => VideoExtensions.Contains(Path.GetExtension(p))).ToList();
        var exifFiles = audioFiles.Concat(photoFiles).ToList();

        progress.Report(new ScanProgress(0, Math.Max(files.Count, 1), $"已找到 {files.Count:N0} 个文件，正在读取音频与照片元数据…"));
        var exifData = ReadExifMetadata(exifFiles, exifTool, progress, files.Count);

        var entries = new List<FileEntry>(files.Count);
        int completed = 0;
        foreach (var path in files)
        {
            var ext = Path.GetExtension(path);
            var category = Classify(ext);
            var info = "";
            if (AudioExtensions.Contains(ext))
                info = BuildAudioDetail(exifData, path);
            else if (PhotoExtensions.Contains(ext))
                info = BuildPhotoDetail(exifData, path);
            else if (VideoExtensions.Contains(ext))
            {
                info = BuildVideoDetail(path, mediaInfo);
            }

            long size = 0;
            try { size = new FileInfo(path).Length; } catch { /* file may have disappeared */ }
            entries.Add(new FileEntry(Path.GetFileName(path), Path.GetRelativePath(folder, path), category,
                info, ext.TrimStart('.').ToUpperInvariant(), size));
            completed++;
            if (completed % 5 == 0 || completed == files.Count)
                progress.Report(new ScanProgress(completed, Math.Max(files.Count, 1), $"正在整理文件信息：{completed:N0}/{files.Count:N0}"));
        }

        var txtPath = Path.Combine(folder, "文件清单.txt");
        var csvPath = Path.Combine(folder, "文件清单.csv");
        WriteTextReport(txtPath, folder, entries);
        WriteCsvReport(csvPath, entries);
        progress.Report(new ScanProgress(Math.Max(files.Count, 1), Math.Max(files.Count, 1), "报告写入完成。", "已生成 TXT 与 CSV 文件。"));

        return new ScanResult(entries, txtPath, csvPath, audioFiles.Count, photoFiles.Count, videoFiles.Count,
            files.Count - audioFiles.Count - photoFiles.Count - videoFiles.Count);
    }

    private static bool IsReportFile(string root, string file)
    {
        if (!string.Equals(Path.GetDirectoryName(file), root, StringComparison.OrdinalIgnoreCase)) return false;
        var name = Path.GetFileName(file);
        return name.Equals("文件清单.txt", StringComparison.OrdinalIgnoreCase)
            || name.Equals("文件清单.csv", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindTool(string fileName)
    {
        var toolsDir = Path.Combine(AppContext.BaseDirectory, "tools");
        if (!Directory.Exists(toolsDir)) return null;
        try
        {
            return Directory.EnumerateFiles(toolsDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch { return null; }
    }

    private static Dictionary<string, Dictionary<string, JsonElement>> ReadExifMetadata(
        List<string> paths, string exifTool, IProgress<ScanProgress> progress, int totalFiles)
    {
        var result = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        for (var start = 0; start < paths.Count; start += 25)
        {
            var batch = paths.Skip(start).Take(25).ToList();
            var args = new List<string>
            {
                "-json", "-G1", "-s", "-api", "LargeFileSupport=1",
                "-charset", "filename=UTF8", "-charset", "exiftool=UTF8"
            };
            args.AddRange(ExifTags);
            args.Add("--");
            args.AddRange(batch);
            var process = RunProcess(exifTool, args, 120_000);
            if (!string.IsNullOrWhiteSpace(process.StdOut))
            {
                try
                {
                    using var doc = JsonDocument.Parse(process.StdOut);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            if (!TryGetProperty(item, "SourceFile", out var source)) continue;
                            var sourcePath = source.GetString();
                            if (string.IsNullOrWhiteSpace(sourcePath)) continue;
                            var record = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                            foreach (var prop in item.EnumerateObject()) record[prop.Name] = prop.Value.Clone();
                            result[NormalPath(sourcePath)] = record;
                        }
                    }
                }
                catch (JsonException)
                {
                    progress.Report(new ScanProgress(0, Math.Max(totalFiles, 1), "ExifTool 返回了无法解析的数据，部分文件可能缺少元数据。"));
                }
            }
            var done = Math.Min(start + batch.Count, paths.Count);
            progress.Report(new ScanProgress(0, Math.Max(totalFiles, 1), $"正在读取媒体标签：{done:N0}/{paths.Count:N0}"));
        }
        return result;
    }

    private static string BuildAudioDetail(Dictionary<string, Dictionary<string, JsonElement>> data, string path)
    {
        if (!data.TryGetValue(NormalPath(path), out var record))
            return "艺术家：未记录 | 专辑：未记录";
        var artist = FindTag(record, "Artist", "Performer", "AlbumArtist", "Composer", "Conductor");
        var album = FindTag(record, "Album");
        return $"艺术家：{ValueOrMissing(artist)} | 专辑：{ValueOrMissing(album)}";
    }

    private static string BuildPhotoDetail(Dictionary<string, Dictionary<string, JsonElement>> data, string path)
    {
        if (!data.TryGetValue(NormalPath(path), out var record))
            return "拍摄时间：未记录";
        var capture = FindTag(record, "SubSecDateTimeOriginal", "DateTimeOriginal", "CreateDate",
            "DateTimeDigitized", "DateCreated", "MediaCreateDate");
        return $"拍摄时间：{(string.IsNullOrWhiteSpace(capture) ? "未记录" : NormalizeDate(capture))}";
    }

    private static string BuildVideoDetail(string path, string mediaInfo)
    {
        var process = RunProcess(mediaInfo, new[] { "--Output=JSON", path }, 120_000);
        if (process.TimedOut)
            return $"类型：{Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} | 编码：读取超时 | 码率：未知";
        if (string.IsNullOrWhiteSpace(process.StdOut))
            return $"类型：{Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} | 编码：读取失败 | 码率：未知";

        try
        {
            using var doc = JsonDocument.Parse(process.StdOut);
            if (!TryGetProperty(doc.RootElement, "media", out var media)
                || !TryGetProperty(media, "track", out var tracks)
                || tracks.ValueKind != JsonValueKind.Array)
                throw new JsonException("MediaInfo JSON 格式不包含 track。");

            JsonElement general = default, video = default;
            foreach (var track in tracks.EnumerateArray())
            {
                var type = GetString(track, "@type");
                if (type.Equals("General", StringComparison.OrdinalIgnoreCase)) general = track;
                else if (type.Equals("Video", StringComparison.OrdinalIgnoreCase) && video.ValueKind == JsonValueKind.Undefined) video = track;
            }

            var container = GetString(general, "Format");
            if (string.IsNullOrWhiteSpace(container)) container = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            var codec = GetString(video, "Format");
            if (string.IsNullOrWhiteSpace(codec)) codec = GetString(video, "CodecID");
            if (string.IsNullOrWhiteSpace(codec)) codec = video.ValueKind == JsonValueKind.Undefined ? "未找到视频流" : "未知";
            codec = FriendlyCodec(codec);

            var bitrate = FormatBitrate(GetString(general, "OverallBitRate"));
            if (string.IsNullOrWhiteSpace(bitrate))
            {
                bitrate = FormatBitrate(GetString(video, "BitRate"));
                if (!string.IsNullOrWhiteSpace(bitrate)) bitrate += "（视频流）";
            }
            if (string.IsNullOrWhiteSpace(bitrate))
            {
                var durationMs = ParseNumber(GetString(general, "Duration"));
                if (durationMs > 0)
                {
                    try
                    {
                        var estimated = new FileInfo(path).Length * 8d / (durationMs / 1000d);
                        bitrate = FormatBitrate(estimated.ToString(CultureInfo.InvariantCulture)) + "（估算）";
                    }
                    catch { /* file access can fail */ }
                }
            }
            if (string.IsNullOrWhiteSpace(bitrate)) bitrate = "未知";
            return $"类型：{container} | 编码：{codec} | 码率：{bitrate}";
        }
        catch (JsonException)
        {
            return $"类型：{Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} | 编码：无法解析 | 码率：未知";
        }
    }

    private static string FindTag(Dictionary<string, JsonElement> record, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var pair in record)
            {
                var normalized = pair.Key.Split(':').Last().Replace("_", "", StringComparison.Ordinal).Trim();
                if (!normalized.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                var value = JsonValueText(pair.Value);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return "";
    }

    private static string JsonValueText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Array => string.Join("; ", value.EnumerateArray().Select(JsonValueText).Where(s => !string.IsNullOrWhiteSpace(s))),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "是",
            JsonValueKind.False => "否",
            _ => ""
        };
    }

    private static string ValueOrMissing(string value) => string.IsNullOrWhiteSpace(value) ? "未记录" : Regex.Replace(value, @"\s+", " ").Trim();

    private static string NormalizeDate(string value)
    {
        var text = Regex.Replace(value.Trim(), @"^(\d{4}):(\d{2}):(\d{2})", "$1-$2-$3");
        return text.Replace('T', ' ');
    }

    private static string FormatBitrate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var match = Regex.Match(raw, @"[-+]?[0-9]*\.?[0-9]+");
        if (!match.Success || !double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return "";
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("gb/s") || lower.Contains("gbps") || lower.Contains("gbit/s")) value *= 1_000_000_000;
        else if (lower.Contains("mb/s") || lower.Contains("mbps") || lower.Contains("mbit/s")) value *= 1_000_000;
        else if (lower.Contains("kb/s") || lower.Contains("kbps") || lower.Contains("kbit/s")) value *= 1_000;
        if (value >= 1_000_000) return $"{value / 1_000_000:0.0} Mbps";
        if (value >= 1_000) return $"{value / 1_000:0} kbps";
        return $"{value:0} bps";
    }

    private static double ParseNumber(string raw)
    {
        var match = Regex.Match(raw ?? "", @"[-+]?[0-9]*\.?[0-9]+");
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string FriendlyCodec(string codec)
    {
        var normalized = codec.Trim();
        var key = normalized.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        return key switch
        {
            "AVC" or "H264" or "AVC1" => "H.264/AVC",
            "HEVC" or "H265" or "HVC1" or "HEV1" => "H.265/HEVC",
            "AV1" => "AV1",
            "VP9" => "VP9",
            "VP8" => "VP8",
            "MPEGVIDEO" or "MPEG2VIDEO" => "MPEG Video",
            "MPEG4VISUAL" or "MP4V" => "MPEG-4 Part 2",
            "PRORES" or "APCH" or "APCN" or "AP4H" or "AP4X" => "Apple ProRes",
            "DNXHD" or "DNXHR" => "Avid DNxHD/DNxHR",
            "MOTIONJPEG" or "MJPEG" => "Motion JPEG",
            "THEORA" => "Theora",
            "VC1" => "VC-1",
            "WMV3" => "WMV3",
            "FFV1" => "FFV1",
            "RAW" or "RAWVIDEO" => "未压缩视频",
            _ => normalized
        };
    }

    private static string Classify(string extension)
    {
        if (AudioExtensions.Contains(extension)) return "音频";
        if (PhotoExtensions.Contains(extension)) return "照片";
        if (VideoExtensions.Contains(extension)) return "视频";
        return "其他";
    }

    private static string NormalPath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant(); }
        catch { return path.ToUpperInvariant(); }
    }

    private static bool TryGetProperty(JsonElement element, string property, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in element.EnumerateObject())
            {
                if (p.Name.Equals(property, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string GetString(JsonElement element, string property)
    {
        if (!TryGetProperty(element, property, out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    private static ProcessResult RunProcess(string executable, IEnumerable<string> arguments, int timeoutMs)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) return new ProcessResult(-1, "", "无法启动外部工具。", false);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(true); } catch { }
                return new ProcessResult(124, "", "处理超时。", true);
            }
            Task.WaitAll(stdoutTask, stderrTask);
            return new ProcessResult(process.ExitCode, stdoutTask.Result, stderrTask.Result, false);
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, "", ex.Message, false);
        }
    }

    private static void WriteTextReport(string path, string folder, List<FileEntry> entries)
    {
        var lines = new List<string>
        {
            "媒体文件清单",
            $"扫描目录：{folder}",
            $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"文件数量：{entries.Count}",
            new string('=', 72),
            ""
        };
        foreach (var entry in entries)
        {
            var line = entry.RelativePath;
            if (!string.IsNullOrWhiteSpace(entry.Detail)) line += $"（{entry.Detail}）";
            lines.Add(line);
        }
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WriteCsvReport(string path, List<FileEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("文件名,相对路径,类别,附加信息,扩展名,大小(字节)");
        foreach (var item in entries)
        {
            sb.Append(Csv(item.FileName)).Append(',')
                .Append(Csv(item.RelativePath)).Append(',')
                .Append(Csv(item.Category)).Append(',')
                .Append(Csv(item.Detail)).Append(',')
                .Append(Csv(item.Extension)).Append(',')
                .Append(item.SizeBytes.ToString(CultureInfo.InvariantCulture)).AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

internal sealed class NaturalStringComparer : IComparer<string>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var left = Regex.Split(x, "(\\d+)");
        var right = Regex.Split(y, "(\\d+)");
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var aIsNumber = long.TryParse(left[i], out var aNum);
            var bIsNumber = long.TryParse(right[i], out var bNum);
            int compare = aIsNumber && bIsNumber ? aNum.CompareTo(bNum) :
                string.Compare(left[i], right[i], StringComparison.CurrentCultureIgnoreCase);
            if (compare != 0) return compare;
        }
        return left.Length.CompareTo(right.Length);
    }
}
