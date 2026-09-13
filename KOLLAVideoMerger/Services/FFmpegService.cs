using System.IO;

namespace KOLLAVideoMerger.Services;

public sealed class FFmpegService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FFmpegService()
    {
        string baseDirectory = AppContext.BaseDirectory;

        _ffmpegPath = Path.Combine(
            baseDirectory,
            "ffmpeg",
            "bin",
            "ffmpeg.exe");

        _ffprobePath = Path.Combine(
            baseDirectory,
            "ffmpeg",
            "bin",
            "ffprobe.exe");
    }

    public string FFmpegPath => _ffmpegPath;

    public string FFprobePath => _ffprobePath;

    public bool IsInstalled =>
        File.Exists(_ffmpegPath) &&
        File.Exists(_ffprobePath);

    public void ValidateInstallation()
    {
        if (!File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException(
                "FFmpeg was not found.",
                _ffmpegPath);
        }

        if (!File.Exists(_ffprobePath))
        {
            throw new FileNotFoundException(
                "FFprobe was not found.",
                _ffprobePath);
        }
    }
}