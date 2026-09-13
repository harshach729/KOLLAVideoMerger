namespace KOLLAVideoMerger.Models;

public class VideoMetadata
{
    public TimeSpan Duration { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public double FrameRate { get; set; }

    public string VideoCodec { get; set; } = string.Empty;

    public string AudioCodec { get; set; } = string.Empty;

    public bool HasAudio { get; set; }
}