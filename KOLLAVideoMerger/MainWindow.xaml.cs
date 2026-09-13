using System.IO;
using System.Windows;
using KOLLAVideoMerger.Models;
using KOLLAVideoMerger.Services;
using MessageBox = System.Windows.MessageBox;

namespace KOLLAVideoMerger;

public partial class MainWindow : Window
{
    private readonly List<VideoFile> _videos = new();

    private CancellationTokenSource? _cancellationTokenSource;

    private readonly FFmpegService _ffmpegService;

    private readonly FFprobeService _ffprobeService;

    private readonly VideoMergeService _videoMergeService;

    private readonly AudioService _audioService;

    private string? _downloadedYouTubeAudio;

    private readonly string[] _supportedExtensions =
    {
        ".mp4",
        ".mov",
        ".avi",
        ".mkv",
        ".mts",
        ".m2ts"
    };

    private readonly string[] _audioExtensions =
    {
        ".mp3",
        ".wav",
        ".m4a",
        ".aac",
        ".flac",
        ".ogg",
        ".opus",
        ".wma"
    };

    public MainWindow()
    {
        InitializeComponent();

        _ffmpegService = new FFmpegService();

        _ffprobeService = new FFprobeService();

        _videoMergeService = new VideoMergeService();

        _audioService = new AudioService();

        CheckFFmpeg();
    }

    private void CheckFFmpeg()
    {
        if (_ffmpegService.IsInstalled)
        {
            StatusText.Text = "Ready";
        }
        else
        {
            StatusText.Text =
                "FFmpeg not installed";

            MessageBox.Show(
                "FFmpeg was not found.\n\n" +
                "Please place ffmpeg.exe and ffprobe.exe inside:\n\n" +
                "ffmpeg\\bin\\",
                "FFmpeg Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    // ================================================================
    // FOLDER
    // ================================================================

    private void BrowseInputFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        using var dialog =
            new System.Windows.Forms.FolderBrowserDialog
            {
                Description =
                    "Select DashCam Video Folder",

                UseDescriptionForTitle = true
            };

        if (dialog.ShowDialog() ==
            System.Windows.Forms.DialogResult.OK)
        {
            InputFolderTextBox.Text =
                dialog.SelectedPath;

            LoadVideos(dialog.SelectedPath);
        }
    }

    private async void LoadVideos(
        string folder)
    {
        _videos.Clear();

        try
        {
            var files =
                Directory
                    .GetFiles(folder)
                    .Where(file =>
                        _supportedExtensions.Contains(
                            Path.GetExtension(file),
                            StringComparer.OrdinalIgnoreCase))
                    .Select(file => new FileInfo(file))
                    .OrderBy(file => file.LastWriteTime)
                    .ToList();

            VideoCountText.Text =
                $"{files.Count} video(s) found";

            if (files.Count == 0)
            {
                VideoListView.ItemsSource = null;

                StatusText.Text =
                    "No supported videos found.";

                return;
            }

            int number = 1;

            foreach (FileInfo file in files)
            {
                StatusText.Text =
                    $"Analyzing {number} of {files.Count}: {file.Name}";

                var video =
                    new VideoFile
                    {
                        Number = number,

                        FileName =
                            file.Name,

                        FullPath =
                            file.FullName,

                        FileSizeBytes =
                            file.Length,

                        FileSize =
                            FormatFileSize(
                                file.Length),

                        ModifiedDate =
                            file.LastWriteTime
                    };

                try
                {
                    VideoMetadata metadata =
                        await _ffprobeService.AnalyzeAsync(
                            file.FullName);

                    video.Duration =
                        metadata.Duration;

                    video.Resolution =
                        metadata.Width > 0 &&
                        metadata.Height > 0
                            ? $"{metadata.Width}x{metadata.Height}"
                            : "Unknown";

                    video.FrameRate =
                        metadata.FrameRate > 0
                            ? $"{metadata.FrameRate:0.##}"
                            : "Unknown";

                    video.VideoCodec =
                        string.IsNullOrWhiteSpace(
                            metadata.VideoCodec)
                            ? "Unknown"
                            : metadata.VideoCodec;

                    video.AudioCodec =
                        metadata.HasAudio
                            ? metadata.AudioCodec
                            : "None";

                    video.HasAudio =
                        metadata.HasAudio;
                }
                catch (Exception ex)
                {
                    video.VideoCodec =
                        "Error";

                    video.AudioCodec =
                        ex.Message.Length > 20
                            ? "Error"
                            : ex.Message;
                }

                _videos.Add(video);

                number++;
            }

            VideoListView.ItemsSource =
                null;

            VideoListView.ItemsSource =
                _videos;

            // Select everything by default.
            VideoListView.SelectAll();

            StatusText.Text =
                $"Ready. {files.Count} video(s) analyzed.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to analyze videos.\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusText.Text =
                "Error analyzing videos.";
        }
    }

    // ================================================================
    // AUDIO SOURCE
    // ================================================================

    private void AudioSource_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (LocalAudioTextBox == null)
        {
            return;
        }

        bool local =
            LocalAudioRadioButton.IsChecked == true;

        bool youtube =
            YouTubeAudioRadioButton.IsChecked == true;

        LocalAudioTextBox.IsEnabled =
            local;

        BrowseAudioButton.IsEnabled =
            local;

        YouTubeUrlTextBox.IsEnabled =
            youtube;

        FetchYouTubeButton.IsEnabled =
            youtube;
    }

    private void BrowseAudioButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        using var dialog =
            new System.Windows.Forms.OpenFileDialog
            {
                Title = "Select Music / Audio",

                Filter =
                    "Audio Files|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma|" +
                    "All Files|*.*",

                Multiselect = false
            };

        if (dialog.ShowDialog() ==
            System.Windows.Forms.DialogResult.OK)
        {
            LocalAudioTextBox.Text =
                dialog.FileName;

            StatusText.Text =
                $"Audio selected: {Path.GetFileName(dialog.FileName)}";
        }
    }

    // ================================================================
    // YOUTUBE
    // ================================================================

    private async void FetchYouTubeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string url =
            YouTubeUrlTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(
                "Please paste a YouTube URL first.",
                "YouTube URL",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        FetchYouTubeButton.IsEnabled =
            false;

        MergeButton.IsEnabled =
            false;

        try
        {
            string directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "KOLLA_VideoMerger");

            Directory.CreateDirectory(
                directory);

            _downloadedYouTubeAudio =
                Path.Combine(
                    directory,
                    $"youtube_{Guid.NewGuid():N}.audio");

            StatusText.Text =
                "Fetching YouTube audio...";

            var progress =
                new Progress<double>(
                    value =>
                    {
                        ProgressBar.Value =
                            value;

                        StatusText.Text =
                            $"Downloading audio... {value:0}%";
                    });

            await _audioService
                .DownloadYouTubeAudioAsync(
                    url,
                    _downloadedYouTubeAudio,
                    progress,
                    CancellationToken.None);

            StatusText.Text =
                "YouTube audio ready.";

            ProgressBar.Value = 100;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(
                    _downloadedYouTubeAudio))
            {
                TryDelete(
                    _downloadedYouTubeAudio);
            }

            _downloadedYouTubeAudio =
                null;

            MessageBox.Show(
                $"Unable to fetch YouTube audio.\n\n{ex.Message}",
                "YouTube Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusText.Text =
                "YouTube audio failed.";
        }
        finally
        {
            FetchYouTubeButton.IsEnabled =
                true;

            MergeButton.IsEnabled =
                true;
        }
    }

    // ================================================================
    // MERGE
    // ================================================================

    private async void MergeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_videos.Count < 2)
        {
            MessageBox.Show(
                "Please select a folder containing at least two videos.",
                "No Videos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var selectedVideos =
            VideoListView.SelectedItems
                .Cast<VideoFile>()
                .ToList();

        if (selectedVideos.Count < 2)
        {
            MessageBox.Show(
                "Please select at least two videos to merge.",
                "Select Videos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        using var dialog =
            new System.Windows.Forms.SaveFileDialog
            {
                Title =
                    "Save Merged Video",

                Filter =
                    "MP4 Video (*.mp4)|*.mp4",

                FileName =
                    $"KOLLA_Merged_{DateTime.Now:yyyyMMdd_HHmmss}.mp4",

                AddExtension = true,

                OverwritePrompt = true
            };

        if (dialog.ShowDialog() !=
            System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        string outputPath =
            dialog.FileName;

        bool includeOriginalAudio =
            AudioCheckBox.IsChecked == true;

        bool enhanceVideo =
            EnhancementCheckBox.IsChecked == true;

        AudioSettings? audioSettings =
            BuildAudioSettings();

        MergeButton.IsEnabled =
            false;

        CancelButton.IsEnabled =
            true;

        ProgressBar.Value =
            0;

        _cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            StatusText.Text =
                "Preparing video merge...";

            var progress =
                new Progress<double>(
                    value =>
                    {
                        ProgressBar.Value =
                            value;

                        StatusText.Text =
                            $"Processing... {value:0}%";
                    });

            await _videoMergeService.MergeAsync(
                selectedVideos,
                outputPath,
                includeOriginalAudio,
                enhanceVideo,
                audioSettings,
                progress,
                _cancellationTokenSource.Token);

            ProgressBar.Value =
                100;

            StatusText.Text =
                "Merge completed successfully.";

            MessageBox.Show(
                $"Your merged video has been created successfully.\n\n" +
                outputPath,
                "Merge Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            ProgressBar.Value =
                0;

            StatusText.Text =
                "Merge cancelled.";

            MessageBox.Show(
                "The video merge was cancelled.",
                "Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Merge failed.";

            MessageBox.Show(
                $"The video merge failed.\n\n{ex.Message}",
                "Merge Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            MergeButton.IsEnabled =
                true;

            CancelButton.IsEnabled =
                false;

            _cancellationTokenSource.Dispose();

            _cancellationTokenSource =
                null;
        }
    }

    // ================================================================
    // AUDIO SETTINGS
    // ================================================================

    private AudioSettings? BuildAudioSettings()
    {
        AudioSourceType sourceType;

        if (NoAudioRadioButton.IsChecked == true)
        {
            return null;
        }

        if (LocalAudioRadioButton.IsChecked == true)
        {
            sourceType =
                AudioSourceType.LocalFile;
        }
        else
        {
            sourceType =
                AudioSourceType.YouTube;
        }

        string audioPath =
            sourceType ==
            AudioSourceType.LocalFile
                ? LocalAudioTextBox.Text.Trim()
                : _downloadedYouTubeAudio ?? string.Empty;

        if (string.IsNullOrWhiteSpace(audioPath) ||
            !File.Exists(audioPath))
        {
            MessageBox.Show(
                "Please select an audio file or fetch the YouTube audio first.",
                "Audio Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return null;
        }

        TimeSpan start =
            ParseTime(
                MusicStartTextBox.Text);

        TimeSpan end =
            ParseTime(
                MusicEndTextBox.Text);

        TimeSpan position =
            ParseTime(
                MusicPositionTextBox.Text);

        AudioMixMode mixMode =
            AudioMixModeComboBox.SelectedIndex == 1
                ? AudioMixMode.Replace
                : AudioMixMode.Mix;

        return new AudioSettings
        {
            SourceType =
                sourceType,

            SourcePath =
                audioPath,

            PreparedAudioPath =
                audioPath,

            YouTubeUrl =
                YouTubeUrlTextBox.Text.Trim(),

            StartTime =
                start,

            EndTime =
                end,

            Position =
                position,

            MusicVolume =
                MusicVolumeSlider.Value,

            OriginalVolume =
                OriginalVolumeSlider.Value,

            MixMode =
                mixMode,

            FadeInSeconds =
                0,

            FadeOutSeconds =
                0
        };
    }

    private static TimeSpan ParseTime(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TimeSpan.Zero;
        }

        if (TimeSpan.TryParse(
                text,
                out TimeSpan result))
        {
            return result;
        }

        if (double.TryParse(
                text,
                out double seconds))
        {
            return TimeSpan.FromSeconds(
                Math.Max(0, seconds));
        }

        return TimeSpan.Zero;
    }

    // ================================================================
    // CANCEL
    // ================================================================

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_cancellationTokenSource == null)
        {
            return;
        }

        StatusText.Text =
            "Cancelling FFmpeg...";

        CancelButton.IsEnabled =
            false;

        _cancellationTokenSource.Cancel();
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private static string FormatFileSize(
        long bytes)
    {
        string[] sizes =
        {
            "B",
            "KB",
            "MB",
            "GB",
            "TB"
        };

        double size =
            bytes;

        int index = 0;

        while (size >= 1024 &&
               index < sizes.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return $"{size:0.##} {sizes[index]}";
    }

    private static void TryDelete(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

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

    protected override void OnClosed(
        EventArgs e)
    {
        TryDelete(
            _downloadedYouTubeAudio);

        base.OnClosed(e);
    }
}