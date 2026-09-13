namespace KOLLAVideoMerger.Models;

public class VideoFile
{
    public int Number { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string FileSize { get; set; } = string.Empty;

    public DateTime ModifiedDate { get; set; }

    public TimeSpan Duration { get; set; }

    public string DurationText =>
        Duration == TimeSpan.Zero
            ? "--:--"
            : Duration.ToString(@"hh\:mm\:ss");

    public string Resolution { get; set; } = string.Empty;

    public string FrameRate { get; set; } = string.Empty;

    public string VideoCodec { get; set; } = string.Empty;

    public string AudioCodec { get; set; } = string.Empty;

    public bool HasAudio { get; set; }
}