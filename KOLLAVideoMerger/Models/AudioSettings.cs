namespace KOLLAVideoMerger.Models;

public enum AudioSourceType
{
    None,
    LocalFile,
    YouTube
}

public enum AudioMixMode
{
    Mix,
    Replace
}

public class AudioSettings
{
    public AudioSourceType SourceType { get; set; } =
        AudioSourceType.None;

    public string SourcePath { get; set; } =
        string.Empty;

    public string YouTubeUrl { get; set; } =
        string.Empty;

    public string PreparedAudioPath { get; set; } =
        string.Empty;

    public TimeSpan StartTime { get; set; } =
        TimeSpan.Zero;

    public TimeSpan EndTime { get; set; } =
        TimeSpan.Zero;

    public TimeSpan Position { get; set; } =
        TimeSpan.Zero;

    public double MusicVolume { get; set; } =
        0.35;

    public double OriginalVolume { get; set; } =
        1.0;

    public AudioMixMode MixMode { get; set; } =
        AudioMixMode.Mix;

    public double FadeInSeconds { get; set; }

    public double FadeOutSeconds { get; set; }

    public bool HasExternalAudio =>
        SourceType != AudioSourceType.None &&
        !string.IsNullOrWhiteSpace(PreparedAudioPath);
}