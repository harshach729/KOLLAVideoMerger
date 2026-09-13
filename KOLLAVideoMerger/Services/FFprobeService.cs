using KOLLAVideoMerger.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace KOLLAVideoMerger.Services;

public sealed class FFprobeService
{
    private readonly string _ffprobePath;

    public FFprobeService()
    {
        _ffprobePath = Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg",
            "bin",
            "ffprobe.exe");
    }

    public bool IsInstalled =>
        File.Exists(_ffprobePath);

    public async Task<VideoMetadata> AnalyzeAsync(
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_ffprobePath))
        {
            throw new FileNotFoundException(
                "FFprobe was not found.",
                _ffprobePath);
        }

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException(
                "Video file was not found.",
                videoPath);
        }

        var arguments =
            $"-v error " +
            $"-show_entries stream=index,codec_type,codec_name,width,height,r_frame_rate,duration " +
            $"-show_entries format=duration " +
            $"-of json " +
            $"\"{videoPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = arguments,

            RedirectStandardOutput = true,
            RedirectStandardError = true,

            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        string output =
            await process.StandardOutput.ReadToEndAsync(
                cancellationToken);

        string error =
            await process.StandardError.ReadToEndAsync(
                cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFprobe failed for:\n{videoPath}\n\n{error}");
        }

        return ParseMetadata(output);
    }


    private static VideoMetadata ParseMetadata(
        string json)
    {
        using JsonDocument document =
            JsonDocument.Parse(json);

        var metadata = new VideoMetadata();

        if (!document.RootElement.TryGetProperty(
                "streams",
                out JsonElement streams))
        {
            return metadata;
        }

        foreach (JsonElement stream in streams.EnumerateArray())
        {
            if (!stream.TryGetProperty(
                    "codec_type",
                    out JsonElement codecTypeElement))
            {
                continue;
            }

            string codecType =
                codecTypeElement.GetString() ?? string.Empty;


            if (codecType.Equals(
                    "video",
                    StringComparison.OrdinalIgnoreCase))
            {
                ParseVideoStream(
                    stream,
                    metadata);
            }


            if (codecType.Equals(
                    "audio",
                    StringComparison.OrdinalIgnoreCase))
            {
                ParseAudioStream(
                    stream,
                    metadata);
            }
        }


        if (document.RootElement.TryGetProperty(
                "format",
                out JsonElement format))
        {
            if (format.TryGetProperty(
                    "duration",
                    out JsonElement durationElement))
            {
                metadata.Duration =
                    ParseDuration(
                        durationElement.GetString());
            }
        }

        return metadata;
    }


    private static void ParseVideoStream(
        JsonElement stream,
        VideoMetadata metadata)
    {
        if (stream.TryGetProperty(
                "codec_name",
                out JsonElement codec))
        {
            metadata.VideoCodec =
                codec.GetString() ?? string.Empty;
        }


        if (stream.TryGetProperty(
                "width",
                out JsonElement width))
        {
            metadata.Width =
                width.GetInt32();
        }


        if (stream.TryGetProperty(
                "height",
                out JsonElement height))
        {
            metadata.Height =
                height.GetInt32();
        }


        if (stream.TryGetProperty(
                "r_frame_rate",
                out JsonElement frameRate))
        {
            string rate =
                frameRate.GetString() ?? string.Empty;

            metadata.FrameRate =
                ParseFrameRate(rate);
        }
    }


    private static void ParseAudioStream(
        JsonElement stream,
        VideoMetadata metadata)
    {
        metadata.HasAudio = true;

        if (stream.TryGetProperty(
                "codec_name",
                out JsonElement codec))
        {
            metadata.AudioCodec =
                codec.GetString() ?? string.Empty;
        }
    }


    private static double ParseFrameRate(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        string[] parts =
            value.Split('/');

        if (parts.Length == 2 &&
            double.TryParse(
                parts[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double numerator) &&
            double.TryParse(
                parts[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double denominator) &&
            denominator != 0)
        {
            return numerator / denominator;
        }

        return 0;
    }


    private static TimeSpan ParseDuration(
        string? value)
    {
        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.Zero;
    }
}