using System.Diagnostics;
using System.Globalization;
using System.IO;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace KOLLAVideoMerger.Services;

public class AudioService
{
    private readonly string _ffmpegPath;

    public bool IsFFmpegInstalled =>
        File.Exists(_ffmpegPath);

    public AudioService()
    {
        _ffmpegPath = Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg",
            "bin",
            "ffmpeg.exe");
    }

    public async Task<string> DownloadYouTubeAudioAsync(
        string url,
        string outputPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException(
                "YouTube URL is required.",
                nameof(url));
        }

        var youtube = new YoutubeClient();

        var video = await youtube.Videos.GetAsync(
            url,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var manifest =
            await youtube.Videos.Streams.GetManifestAsync(
                video.Id,
                cancellationToken);

        var audioStream =
            manifest
                .GetAudioOnlyStreams()
                .GetWithHighestBitrate();

        if (audioStream == null)
        {
            throw new InvalidOperationException(
                "No audio stream was available for this YouTube video.");
        }

        string directory =
            Path.GetDirectoryName(outputPath)
            ?? Path.GetTempPath();

        Directory.CreateDirectory(directory);

        await youtube.Videos.Streams.DownloadAsync(
            audioStream,
            outputPath,
            new Progress<double>(value =>
            {
                progress?.Report(value);
            }),
            cancellationToken);

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                "The YouTube audio download did not produce an output file.");
        }

        return outputPath;
    }

    public async Task<string> ConvertToAacAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException(
                "Audio file was not found.",
                inputPath);
        }

        var arguments =
            $"-hide_banner " +
            $"-loglevel error " +
            $"-i \"{inputPath}\" " +
            $"-vn " +
            $"-c:a aac " +
            $"-b:a 192k " +
            $"-y \"{outputPath}\"";

        await RunFFmpegAsync(
            arguments,
            outputPath,
            cancellationToken);

        return outputPath;
    }

    private static async Task RunFFmpegAsync(
        string arguments,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                AppContext.BaseDirectory,
                "ffmpeg",
                "bin",
                "ffmpeg.exe"),

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
            throw new InvalidOperationException(
                "Unable to start FFmpeg.");
        }

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync(
                cancellationToken);

        try
        {
            await process.WaitForExitAsync(
                cancellationToken);

            string error =
                await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"FFmpeg exited with code {process.ExitCode}."
                        : error.Trim());
            }
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

            throw;
        }
    }
}