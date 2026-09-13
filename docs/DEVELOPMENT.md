# Development Guide

## Prerequisites

- Windows 10/11
- .NET 10 SDK
- Git
- FFmpeg/FFprobe binaries in the expected project directory

## Build

```bash
dotnet restore
dotnet build --configuration Release
```

## Run

```bash
dotnet run --project KOLLAVideoMerger/KOLLAVideoMerger.csproj
```

## Runtime FFmpeg Layout

```text
ffmpeg/
└── bin/
    ├── ffmpeg.exe
    └── ffprobe.exe
```

## Manual Test Matrix

### Video

- [ ] Two videos
- [ ] Three or more videos
- [ ] MP4
- [ ] MOV
- [ ] AVI
- [ ] MKV
- [ ] MTS
- [ ] M2TS
- [ ] 1080p
- [ ] 4K
- [ ] Video with audio
- [ ] Video without audio

### Enhancement

- [ ] Enhancement off
- [ ] Enhancement on
- [ ] Verify output quality
- [ ] Compare processing time

### Audio

- [ ] No external audio
- [ ] Local audio
- [ ] YouTube audio
- [ ] Mix
- [ ] Replace
- [ ] Start/end trimming
- [ ] Position
- [ ] Music volume
- [ ] Original volume

### Hardware

- [ ] NVENC
- [ ] QSV
- [ ] AMF
- [ ] Software fallback
- [ ] Hardware initialization failure

### Cancellation

- [ ] Cancel during merge
- [ ] Cancel during enhancement
- [ ] Cancel during audio processing
- [ ] Verify partial output is removed

## Debugging

When diagnosing a media-processing issue, capture:

- FFmpeg arguments
- FFmpeg stderr
- Exit code
- Selected encoder
- Input metadata
- Output path

Do not rely only on the WPF error dialog.

## Code Guidelines

- Keep media-processing logic inside services.
- Avoid blocking the WPF UI thread.
- Use asynchronous process APIs.
- Pass cancellation tokens through long-running operations.
- Validate files before invoking FFmpeg.
- Clean temporary files.
- Avoid machine-specific absolute paths.
