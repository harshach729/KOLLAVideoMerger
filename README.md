# KOLLA Video Merger

**A Windows desktop video-processing application built with C#/.NET 10, WPF, and FFmpeg.**

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/UI-WPF-512BD4)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![FFmpeg](https://img.shields.io/badge/Powered%20by-FFmpeg-007808)](https://ffmpeg.org/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)

> Merge, enhance, and process high-resolution video locally with a .NET desktop application designed around performance, reliability, and graceful hardware/software fallback.

## Overview

KOLLA Video Merger was built around a practical need: combine multiple high-resolution dash-cam recordings into a single MP4 without requiring a full-featured video editor.

The application provides a WPF interface over FFmpeg and FFprobe while handling media discovery, metadata inspection, video concatenation, optional enhancement, external audio, asynchronous processing, progress reporting, cancellation, and encoder fallback.

The project evolved beyond a simple FFmpeg wrapper as real-world testing exposed problems such as high CPU usage during 4K re-encoding, hardware encoder availability differences, FFmpeg concat-file encoding issues, and audio-processing edge cases.

## Highlights

- 🎬 Merge multiple supported video files into one MP4
- 🔎 Inspect duration, resolution, FPS, video codec, and audio codec with FFprobe
- ⚡ Use a fast video-copy path when enhancement and external audio are not required
- 🚀 Detect and validate hardware H.264 encoders
- 🛡️ Fall back to `libx264` when hardware encoding is unavailable or fails
- 🎨 Apply brightness, contrast, saturation, and sharpening filters
- 🔊 Preserve original audio, replace it, or mix it with external audio
- 🎵 Use local audio files or fetch audio-only streams from a YouTube URL
- ✂️ Configure external-audio start/end trimming and placement
- 🔉 Control music and original-audio volume
- 📊 Display FFmpeg progress in the WPF UI
- ⛔ Cancel long-running processing and terminate the FFmpeg process tree
- 🧹 Clean up temporary files and partial output on cancellation

## Screenshots
- `main-window.png`
- `video-selection.png`
- `audio-settings.png`
- `merge-progress.png`

## Features

### Video Merging

The application reads supported files from an input folder and analyzes them with FFprobe.

Supported input extensions currently include:

- `.mp4`
- `.mov`
- `.avi`
- `.mkv`
- `.mts`
- `.m2ts`

The video list displays:

| Metadata | Example |
|---|---|
| File name | `LVQK0279.MOV` |
| File size | `39.73 MB` |
| Duration | `00:00:11` |
| Resolution | `3840x2160` |
| FPS | `25` |
| Video codec | `h264` |
| Audio codec | `aac` |

At least two videos must be selected before merging.

### Fast Processing Path

When enhancement is disabled and external audio is not being processed, the merge service uses FFmpeg's video-copy path.

```text
Input videos
     │
     ▼
FFmpeg concat demuxer
     │
     ├── Video: copy
     └── Audio: copy / remove
     │
     ▼
Final MP4
```

This avoids unnecessary video re-encoding when the requested operation permits it.

### Video Enhancement

The current enhanced pipeline applies:

```text
eq
 ├── contrast
 ├── brightness
 └── saturation

unsharp
 └── sharpening
```

The current implementation uses a fixed enhancement profile rather than exposing individual enhancement sliders.

### Hardware Encoder Detection

The application tests H.264 hardware encoders in this order:

```text
h264_nvenc
     ↓
h264_qsv
     ↓
h264_amf
     ↓
libx264
```

The application performs a small FFmpeg test encode rather than assuming an encoder works simply because its name is available.

If a hardware encoder fails during the actual enhanced merge, the service removes the partial output and retries with `libx264`.

### Audio Processing

External audio can be:

- A local audio file
- Audio retrieved from a YouTube URL

The application supports:

- Start time
- End time
- Position in the final video
- Music volume
- Original-audio volume
- Mix mode
- Replace mode

The FFmpeg audio pipeline uses filters including `atrim`, `asetpts`, `volume`, `adelay`, `afade`, `apad`, and `amix` where applicable.

### YouTube Audio

The application uses `YoutubeExplode` to retrieve an audio-only stream from a supplied YouTube URL.

Downloaded audio is stored temporarily under the user's temporary directory.

> Use YouTube content only when you have the necessary rights or permission and in accordance with applicable service terms and laws.

### Progress Reporting

FFmpeg is launched with:

```text
-progress pipe:1
-stats_period 0.5
```

The service reads `out_time_us` and converts processed media time into an approximate percentage.

```text
FFmpeg
   │
   │ out_time_us
   ▼
Progress parser
   │
   ▼
IProgress<double>
   │
   ▼
WPF ProgressBar
```

### Cancellation

The merge operation accepts a `CancellationToken`.

On cancellation, the application attempts to terminate the FFmpeg process tree and removes partial output.

## Architecture

```text
┌─────────────────────────────────────────┐
│                  WPF UI                 │
│                                         │
│ Folder selection / media list           │
│ Audio settings / output options         │
│ Progress / status / cancellation        │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│            Application Services         │
│                                         │
│ VideoMergeService                       │
│ AudioService                             │
│ FFprobeService                           │
│ FFmpegService                            │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│                  FFmpeg                 │
│                                         │
│ Concat / decode / filter / encode       │
│ Audio processing / progress             │
└─────────────────────────────────────────┘
```

### Core Services

**`FFmpegService`**

- Resolves bundled FFmpeg/FFprobe paths
- Verifies required binaries exist

**`FFprobeService`**

- Executes FFprobe
- Requests JSON metadata
- Parses video/audio stream information
- Calculates frame rate and duration

**`VideoMergeService`**

- Validates input files
- Creates temporary concat files
- Selects processing strategy
- Detects hardware encoders
- Builds FFmpeg arguments
- Runs FFmpeg
- Parses progress
- Handles cancellation and cleanup
- Retries with software encoding when necessary

**`AudioService`**

- Retrieves YouTube audio-only streams
- Downloads temporary audio
- Contains FFmpeg-based audio conversion support

## Project Structure

```text
KOLLAVideoMerger/
│
├── KOLLAVideoMerger/
│   ├── Models/
│   │   ├── AudioSettings.cs
│   │   ├── VideoFile.cs
│   │   └── VideoMetadata.cs
│   │
│   ├── Services/
│   │   ├── AudioService.cs
│   │   ├── FFmpegService.cs
│   │   ├── FFprobeService.cs
│   │   └── VideoMergeService.cs
│   │
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── KOLLAVideoMerger.csproj
│   └── ffmpeg/
│       └── bin/
│           ├── ffmpeg.exe
│           └── ffprobe.exe
│
├── docs/
├── screenshots/
├── .github/
├── .gitignore
├── .gitattributes
├── Directory.Build.props
├── LICENSE
└── KOLLAVideoMerger.slnx
```

## Technology Stack

| Technology | Purpose |
|---|---|
| C# | Application development |
| .NET 10 | Runtime and application framework |
| WPF | Windows desktop UI |
| XAML | UI definition |
| Windows Forms | Native file/folder dialogs |
| FFmpeg | Video/audio processing |
| FFprobe | Media metadata inspection |
| YoutubeExplode 6.6.2 | YouTube stream discovery/download |
| H.264 | Output video encoding |
| NVENC / QSV / AMF | Optional hardware encoding |
| libx264 | Software H.264 fallback |
| async/await | Non-blocking processing |
| GitHub Actions | CI |

## Installation

### Requirements

- Windows 10 or Windows 11
- .NET 10 SDK for source builds
- FFmpeg and FFprobe in the expected runtime layout
- Supported GPU and drivers for optional hardware encoding

### Clone

```bash
git clone https://github.com/harshach729/KOLLAVideoMerger.git
cd KOLLAVideoMerger
```

### Restore

```bash
dotnet restore
```

### Build

```bash
dotnet build --configuration Release
```

### Run

```bash
dotnet run --project KOLLAVideoMerger/KOLLAVideoMerger.csproj
```

## FFmpeg Packaging

The application resolves FFmpeg relative to `AppContext.BaseDirectory`:

```text
ffmpeg/
└── bin/
    ├── ffmpeg.exe
    └── ffprobe.exe
```

The project file is configured to copy both binaries to build and publish output.

FFmpeg is a separate third-party project. Review the license and redistribution requirements for the exact FFmpeg build included in any public release.

## Performance

The main optimization is choosing whether video needs to be re-encoded.

```text
No enhancement
+ No external audio
        │
        ▼
Video copy path
        │
        ▼
Lower processing cost

Enhancement enabled
        │
        ▼
Decode → Filter → Encode
        │
        ▼
Higher processing cost
```

For 4K media, software encoding can consume substantial CPU resources. Hardware encoders can reduce CPU workload on supported systems.

See [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md).

## Reliability

The project includes:

- FFmpeg/FFprobe validation
- Input-file validation
- Unique temporary concat files
- UTF-8-without-BOM concat files
- Hardware encoder validation
- `libx264` fallback
- FFmpeg stderr capture
- Cancellation support
- Partial-output cleanup
- Temporary-file cleanup

See [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md).

## Known Limitations

- The enhancement pipeline currently uses a fixed filter profile.
- Hardware encoding is currently H.264-focused.
- The `FastModeCheckBox` exists in the current UI but is not wired into `MainWindow.xaml.cs`; the service automatically chooses the fast path based on processing requirements. This should be fixed or removed before presenting it as a user-controlled setting.
- Fade-in/fade-out fields exist in the model/service pipeline but are not exposed as UI controls.
- Media compatibility can affect whether FFmpeg's concat/copy path is appropriate.
- The application is Windows-focused.
- Automated unit/integration test coverage is not currently included.

## Roadmap

### Near Term

- [ ] Add automated tests
- [ ] Fix or remove the unused fast-processing checkbox
- [ ] Improve audio-processing validation
- [ ] Improve structured error reporting
- [ ] Add structured logging
- [ ] Add GitHub Actions build/test validation
- [ ] Create a packaged Windows release

### Future

- [ ] Drag-and-drop video selection
- [ ] Thumbnail previews
- [ ] User-configurable enhancement controls
- [ ] Audio fade controls in the UI
- [ ] Encoding presets
- [ ] Batch processing
- [ ] Additional hardware-encoding options
- [ ] Windows installer
- [ ] Automated GitHub Releases

## Security & Privacy

Video and audio are processed locally through FFmpeg.

Do not commit:

- Personal media
- Credentials
- API keys
- Access tokens
- Private configuration
- Sensitive logs

The YouTube feature uses a third-party service. Users are responsible for complying with applicable terms and copyright requirements.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Development Guide](docs/DEVELOPMENT.md)
- [Performance Engineering](docs/PERFORMANCE.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Design Decisions](docs/DESIGN-DECISIONS.md)

## License

The application source code is released under the MIT License unless otherwise stated.

FFmpeg and other third-party components retain their respective licenses.

See [`LICENSE`](LICENSE).

## Author

**Harsha Kolla**

Senior Software Engineer | C#/.NET | Enterprise Applications | AI-Assisted Engineering

- GitHub: https://github.com/harshach729
- LinkedIn: https://www.linkedin.com/in/harshakolla729
