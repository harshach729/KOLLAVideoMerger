using KOLLAVideoMerger.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace KOLLAVideoMerger.Services;

public class VideoMergeService
{
    private readonly string _ffmpegPath;

    private string? _hardwareEncoder;
    private bool _encoderDetectionCompleted;

    private string? _workingHardwareEncoder;
    private bool _encoderChecked;

    public bool IsInstalled =>
        File.Exists(_ffmpegPath);

    public VideoMergeService()
    {
        _ffmpegPath = Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg",
            "bin",
            "ffmpeg.exe");
    }

    public async Task MergeAsync(
        List<VideoFile> videos,
        string outputPath,
        bool includeAudio,
        bool enhanceVideo,
        AudioSettings? audioSettings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!IsInstalled)
        {
            throw new FileNotFoundException(
                "FFmpeg was not found.",
                _ffmpegPath);
        }

        if (videos == null || videos.Count < 2)
        {
            throw new ArgumentException(
                "At least two videos are required.",
                nameof(videos));
        }

        foreach (VideoFile video in videos)
        {
            if (!File.Exists(video.FullPath))
            {
                throw new FileNotFoundException(
                    $"Input video was not found: {video.FullPath}");
            }
        }

        string? outputDirectory =
            Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string concatFile =
            Path.Combine(
                Path.GetTempPath(),
                $"kolla_concat_{Guid.NewGuid():N}.txt");

        try
        {
            await CreateConcatFileAsync(
                videos,
                concatFile,
                cancellationToken);

            double totalDuration =
                videos.Sum(v =>
                    v.Duration.TotalSeconds);

            if (totalDuration <= 0)
            {
                throw new InvalidOperationException(
                    "Unable to determine video duration.");
            }

            bool externalAudio =
                audioSettings?.HasExternalAudio == true;

            // ---------------------------------------------------------
            // ORIGINAL FAST PATH
            // ---------------------------------------------------------

            if (!enhanceVideo && !externalAudio)
            {
                await RunFastMergeAsync(
                    concatFile,
                    outputPath,
                    includeAudio,
                    totalDuration,
                    progress,
                    cancellationToken);

                return;
            }

            // ---------------------------------------------------------
            // AUDIO PATH
            // ---------------------------------------------------------

            if (externalAudio && audioSettings != null)
            {
                await RunAudioMergeAsync(
                    concatFile,
                    outputPath,
                    includeAudio,
                    enhanceVideo,
                    totalDuration,
                    audioSettings,
                    progress,
                    cancellationToken);

                return;
            }

            // ---------------------------------------------------------
            // ENHANCED PATH
            // ---------------------------------------------------------

            string encoder =
                await GetBestVideoEncoderAsync(
                    cancellationToken);

            try
            {
                await RunEnhancedMergeAsync(
                    concatFile,
                    outputPath,
                    includeAudio,
                    totalDuration,
                    encoder,
                    progress,
                    cancellationToken);
            }
            catch (Exception)
                when (!cancellationToken.IsCancellationRequested &&
                      !string.Equals(
                          encoder,
                          "libx264",
                          StringComparison.OrdinalIgnoreCase))
            {
                _hardwareEncoder = null;

                DeleteOutput(outputPath);

                progress?.Report(0);

                await RunEnhancedMergeAsync(
                    concatFile,
                    outputPath,
                    includeAudio,
                    totalDuration,
                    "libx264",
                    progress,
                    cancellationToken);
            }
        }
        finally
        {
            DeleteOutput(concatFile);
        }
    }

    // ================================================================
    // CONCAT
    // ================================================================

    private static async Task CreateConcatFileAsync(
        List<VideoFile> videos,
        string concatFile,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        foreach (VideoFile video in videos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string path =
                video.FullPath
                    .Replace("\\", "/")
                    .Replace("'", "'\\''");

            builder.AppendLine(
                $"file '{path}'");
        }

        var encoding =
            new UTF8Encoding(false);

        await File.WriteAllTextAsync(
            concatFile,
            builder.ToString(),
            encoding,
            cancellationToken);
    }

    // ================================================================
    // ORIGINAL FAST MERGE
    // ================================================================

    private async Task RunFastMergeAsync(
        string concatFile,
        string outputPath,
        bool includeAudio,
        double totalDuration,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = new StringBuilder();

        arguments.Append("-hide_banner ");
        arguments.Append("-loglevel error ");

        arguments.Append("-f concat ");
        arguments.Append("-safe 0 ");
        arguments.Append($"-i \"{concatFile}\" ");

        arguments.Append("-c:v copy ");

        if (includeAudio)
        {
            arguments.Append("-c:a copy ");
        }
        else
        {
            arguments.Append("-an ");
        }

        arguments.Append("-movflags +faststart ");

        arguments.Append("-progress pipe:1 ");
        arguments.Append("-stats_period 0.5 ");

        arguments.Append(
            $"-y \"{outputPath}\"");

        await RunFFmpegAsync(
            arguments.ToString(),
            outputPath,
            totalDuration,
            progress,
            cancellationToken);
    }

    // ================================================================
    // AUDIO MERGE
    // ================================================================

    private async Task RunAudioMergeAsync(
        string concatFile,
        string outputPath,
        bool includeOriginalAudio,
        bool enhanceVideo,
        double totalDuration,
        AudioSettings settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.PreparedAudioPath))
        {
            throw new FileNotFoundException(
                "Prepared music file was not found.",
                settings.PreparedAudioPath);
        }

        var arguments =
            new StringBuilder();

        arguments.Append("-hide_banner ");
        arguments.Append("-loglevel error ");

        // Video
        arguments.Append("-f concat ");
        arguments.Append("-safe 0 ");
        arguments.Append($"-i \"{concatFile}\" ");

        // Music
        arguments.Append(
            $"-stream_loop -1 ");

        arguments.Append(
            $"-i \"{settings.PreparedAudioPath}\" ");

        // ------------------------------------------------------------
        // VIDEO
        // ------------------------------------------------------------

        if (enhanceVideo)
        {
            arguments.Append(
                "-vf " +
                "\"eq=contrast=1.03:brightness=0.01:saturation=1.03," +
                "unsharp=5:5:0.35:5:5:0.0\" ");

            string encoder =
                await GetBestVideoEncoderAsync(
                    cancellationToken);

            AppendVideoEncoder(
                arguments,
                encoder);
        }
        else
        {
            // Preserve the fast video path.
            arguments.Append("-c:v copy ");
        }

        // ------------------------------------------------------------
        // AUDIO FILTER
        // ------------------------------------------------------------

        string filter =
            BuildAudioFilter(
                settings,
                includeOriginalAudio,
                totalDuration);

        arguments.Append(
            $"-filter_complex \"{filter}\" ");

        arguments.Append("-map 0:v:0 ");

        arguments.Append(
            "-map \"[aout]\" ");

        arguments.Append(
            "-c:a aac ");

        arguments.Append(
            "-b:a 192k ");

        arguments.Append(
            "-movflags +faststart ");

        arguments.Append(
            "-shortest ");

        arguments.Append(
            "-progress pipe:1 ");

        arguments.Append(
            "-stats_period 0.5 ");

        arguments.Append(
            $"-y \"{outputPath}\"");

        await RunFFmpegAsync(
            arguments.ToString(),
            outputPath,
            totalDuration,
            progress,
            cancellationToken);
    }

    private static string BuildAudioFilter(
    AudioSettings settings,
    bool includeOriginalAudio,
    double totalDuration)
{
    double sourceStart =
        Math.Max(
            0,
            settings.StartTime.TotalSeconds);

    double sourceEnd =
        settings.EndTime > settings.StartTime
            ? settings.EndTime.TotalSeconds
            : double.MaxValue;

    double position =
        Math.Max(
            0,
            settings.Position.TotalSeconds);

    double musicVolume =
        Math.Clamp(
            settings.MusicVolume,
            0,
            3);

    double originalVolume =
        Math.Clamp(
            settings.OriginalVolume,
            0,
            3);

    var music =
        new StringBuilder();

    // ------------------------------------------------------------
    // MUSIC SOURCE
    // ------------------------------------------------------------

    music.Append("[1:a]");

    // Explicitly trim using start/end.
    if (sourceEnd != double.MaxValue)
    {
        music.Append(
            $"atrim=" +
            $"start={sourceStart.ToString(CultureInfo.InvariantCulture)}:" +
            $"end={sourceEnd.ToString(CultureInfo.InvariantCulture)},");
    }
    else
    {
        music.Append(
            $"atrim=" +
            $"start={sourceStart.ToString(CultureInfo.InvariantCulture)},");
    }

    // Reset timestamps after trimming.
    music.Append("asetpts=PTS-STARTPTS,");

    // Music volume.
    music.Append(
        $"volume={musicVolume.ToString(CultureInfo.InvariantCulture)},");

    // ------------------------------------------------------------
    // FADE IN
    // ------------------------------------------------------------

    if (settings.FadeInSeconds > 0)
    {
        music.Append(
            $"afade=t=in:" +
            $"st=0:" +
            $"d={settings.FadeInSeconds.ToString(CultureInfo.InvariantCulture)},");
    }

    // ------------------------------------------------------------
    // FADE OUT
    // ------------------------------------------------------------

    if (settings.FadeOutSeconds > 0 &&
        sourceEnd != double.MaxValue)
    {
        double musicDuration =
            Math.Max(
                0.1,
                sourceEnd - sourceStart);

        double fadeStart =
            Math.Max(
                0,
                musicDuration - settings.FadeOutSeconds);

        double fadeDuration =
            Math.Min(
                settings.FadeOutSeconds,
                musicDuration);

        music.Append(
            $"afade=t=out:" +
            $"st={fadeStart.ToString(CultureInfo.InvariantCulture)}:" +
            $"d={fadeDuration.ToString(CultureInfo.InvariantCulture)},");
    }

    // ------------------------------------------------------------
    // MUSIC POSITION
    // ------------------------------------------------------------

    if (position > 0)
    {
        int delayMilliseconds =
            (int)Math.Round(
                position * 1000);

        music.Append(
            $"adelay={delayMilliseconds}:all=1,");
    }

    // Never allow the music stream to extend beyond the video.
    music.Append(
        $"atrim=duration=" +
        $"{totalDuration.ToString(CultureInfo.InvariantCulture)}");

    music.Append("[music]");

    // ------------------------------------------------------------
    // REPLACE ORIGINAL AUDIO
    // ------------------------------------------------------------

    if (settings.MixMode == AudioMixMode.Replace ||
        !includeOriginalAudio)
    {
        return
            music +
            ";" +
            $"[music]apad=whole_dur=" +
            $"{totalDuration.ToString(CultureInfo.InvariantCulture)}," +
            $"atrim=duration=" +
            $"{totalDuration.ToString(CultureInfo.InvariantCulture)}" +
            "[aout]";
    }

    // ------------------------------------------------------------
    // ORIGINAL AUDIO
    // ------------------------------------------------------------

    string original =
        "[0:a]" +
        $"volume={originalVolume.ToString(CultureInfo.InvariantCulture)}" +
        "[original]";

    // ------------------------------------------------------------
    // MIX MUSIC + ORIGINAL AUDIO
    // ------------------------------------------------------------

    string mix =
        "[original][music]" +
        "amix=" +
        "inputs=2:" +
        "duration=longest:" +
        "dropout_transition=0" +
        "[aout]";

    return
        original +
        ";" +
        music +
        ";" +
        mix;
}

    // ================================================================
    // ENHANCED MERGE
    // ================================================================

    private async Task RunEnhancedMergeAsync(
        string concatFile,
        string outputPath,
        bool includeAudio,
        double totalDuration,
        string encoder,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var arguments =
            new StringBuilder();

        arguments.Append("-hide_banner ");
        arguments.Append("-loglevel error ");

        arguments.Append("-f concat ");
        arguments.Append("-safe 0 ");
        arguments.Append($"-i \"{concatFile}\" ");

        arguments.Append(
            "-vf " +
            "\"eq=contrast=1.03:brightness=0.01:saturation=1.03," +
            "unsharp=5:5:0.35:5:5:0.0\" ");

        AppendVideoEncoder(
            arguments,
            encoder);

        if (includeAudio)
        {
            arguments.Append("-c:a aac ");
            arguments.Append("-b:a 192k ");
        }
        else
        {
            arguments.Append("-an ");
        }

        arguments.Append("-movflags +faststart ");

        arguments.Append("-progress pipe:1 ");
        arguments.Append("-stats_period 0.5 ");

        arguments.Append(
            $"-y \"{outputPath}\"");

        await RunFFmpegAsync(
            arguments.ToString(),
            outputPath,
            totalDuration,
            progress,
            cancellationToken);
    }

    // ================================================================
    // ENCODER
    // ================================================================

    private static void AppendVideoEncoder(
        StringBuilder arguments,
        string encoder)
    {
        switch (encoder.ToLowerInvariant())
        {
            case "h264_nvenc":

                arguments.Append(
                    "-c:v h264_nvenc ");

                arguments.Append(
                    "-preset p4 ");

                arguments.Append(
                    "-cq 23 ");

                arguments.Append(
                    "-b:v 0 ");

                break;

            case "h264_qsv":

                arguments.Append(
                    "-c:v h264_qsv ");

                arguments.Append(
                    "-preset medium ");

                arguments.Append(
                    "-global_quality 23 ");

                break;

            case "h264_amf":

                arguments.Append(
                    "-c:v h264_amf ");

                arguments.Append(
                    "-quality quality ");

                arguments.Append(
                    "-rc cqp ");

                arguments.Append(
                    "-qp_i 23 ");

                arguments.Append(
                    "-qp_p 23 ");

                break;

            default:

                arguments.Append(
                    "-c:v libx264 ");

                arguments.Append(
                    "-preset veryfast ");

                arguments.Append(
                    "-crf 22 ");

                break;
        }
    }

    // ================================================================
    // ENCODER DETECTION
    // ================================================================

    private async Task<string> GetBestVideoEncoderAsync(
    CancellationToken cancellationToken)
    {
        if (_encoderChecked)
        {
            return _workingHardwareEncoder ?? "libx264";
        }

        _encoderChecked = true;

        string[] candidates =
        {
        "h264_nvenc",
        "h264_qsv",
        "h264_amf"
    };

        foreach (string encoder in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await IsEncoderAvailableAsync(
                    encoder,
                    cancellationToken))
            {
                continue;
            }

            _workingHardwareEncoder = encoder;

            return encoder;
        }

        _workingHardwareEncoder = null;

        return "libx264";
    }

    private async Task<bool> IsEncoderAvailableAsync(
    string encoder,
    CancellationToken cancellationToken)
    {
        string arguments =
            "-hide_banner " +
            "-loglevel error " +
            "-f lavfi " +
            "-i color=c=black:s=128x128:r=1 " +
            "-frames:v 1 " +
            $"-c:v {encoder} " +
            "-pix_fmt yuv420p " +
            "-f null NUL";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
            {
                return false;
            }

            Task<string> errorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            string error = await errorTask;

            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetFFmpegEncodersAsync(
        CancellationToken cancellationToken)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName = _ffmpegPath,

                Arguments =
                    "-hide_banner -encoders",

                UseShellExecute = false,

                RedirectStandardOutput = true,

                RedirectStandardError = true,

                CreateNoWindow = true
            };

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Unable to start FFmpeg.");
        }

        Task<string> stdoutTask =
            process.StandardOutput.ReadToEndAsync(
                cancellationToken);

        Task<string> stderrTask =
            process.StandardError.ReadToEndAsync(
                cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        return
            await stdoutTask +
            Environment.NewLine +
            await stderrTask;
    }

    private static bool ContainsEncoder(
        string output,
        string encoder)
    {
        return output.Contains(
            encoder,
            StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // FFMPEG
    // ================================================================

    private async Task RunFFmpegAsync(
        string arguments,
        string outputPath,
        double totalDuration,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName = _ffmpegPath,

                Arguments = arguments,

                UseShellExecute = false,

                RedirectStandardOutput = true,

                RedirectStandardError = true,

                CreateNoWindow = true
            };

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Unable to start FFmpeg.");
        }

        Task stdoutTask =
            ReadProgressAsync(
                process,
                totalDuration,
                progress,
                cancellationToken);

        Task<string> errorTask =
            ReadErrorAsync(
                process,
                cancellationToken);

        try
        {
            await process.WaitForExitAsync(
                cancellationToken);

            await stdoutTask;

            string error =
                await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"FFmpeg exited with code {process.ExitCode}."
                        : error.Trim());
            }

            progress?.Report(100);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(
                        entireProcessTree: true);
                }
            }
            catch
            {
            }

            DeleteOutput(outputPath);

            throw;
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(
                        entireProcessTree: true);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    // ================================================================
    // PROGRESS
    // ================================================================

    private static async Task ReadProgressAsync(
        Process process,
        double totalDuration,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var reader =
            process.StandardOutput;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line =
                await reader.ReadLineAsync(
                    cancellationToken);

            if (line == null)
            {
                break;
            }

            if (!line.StartsWith(
                    "out_time_us=",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value =
                line["out_time_us=".Length..];

            if (!long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long microseconds))
            {
                continue;
            }

            double seconds =
                microseconds / 1_000_000.0;

            double percentage =
                seconds /
                totalDuration *
                100.0;

            percentage =
                Math.Clamp(
                    percentage,
                    0,
                    99.9);

            progress?.Report(
                percentage);
        }
    }

    // ================================================================
    // ERROR
    // ================================================================

    private static async Task<string> ReadErrorAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        var reader =
            process.StandardError;

        var builder =
            new StringBuilder();

        while (true)
        {
            string? line =
                await reader.ReadLineAsync(
                    cancellationToken);

            if (line == null)
            {
                break;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    // ================================================================
    // CLEANUP
    // ================================================================

    private static void DeleteOutput(
        string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}