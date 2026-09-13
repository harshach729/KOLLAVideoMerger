# Architecture

## Overview

KOLLA Video Merger uses WPF for presentation and focused services for media processing.

The UI owns user interaction. Services own FFmpeg/FFprobe orchestration and media-processing responsibilities.

## Components

### Presentation

`MainWindow.xaml` and `MainWindow.xaml.cs` provide:

- Input-folder selection
- Video selection
- Media metadata display
- Audio configuration
- Output options
- Progress
- Cancellation
- User-facing errors

### Models

`VideoFile` stores file and analyzed media information.

`VideoMetadata` represents parsed FFprobe information.

`AudioSettings` represents external-audio configuration.

## Services

### FFmpegService

Resolves and validates the bundled FFmpeg and FFprobe executables.

### FFprobeService

Runs FFprobe with JSON output and maps the result into `VideoMetadata`.

### AudioService

Uses YoutubeExplode for audio-only YouTube stream retrieval and contains FFmpeg-based audio conversion support.

### VideoMergeService

The main media-processing orchestrator.

Responsibilities:

1. Validate inputs
2. Create a temporary concat file
3. Calculate total duration
4. Select a processing path
5. Select/validate an encoder
6. Build FFmpeg arguments
7. Execute FFmpeg
8. Parse progress
9. Handle cancellation
10. Retry with software encoding when needed
11. Clean temporary and partial files

## Processing Decision

```text
Start
  │
  ▼
Validate inputs
  │
  ▼
External audio?
 ┌┴─────────────┐
No             Yes
│               │
▼               ▼
Enhancement?   Audio pipeline
│
├── No → Video copy path
│
└── Yes → Enhanced encode path
```

## Encoder Strategy

```text
h264_nvenc
     │
     ├── test succeeds → use
     │
     ▼
h264_qsv
     │
     ├── test succeeds → use
     │
     ▼
h264_amf
     │
     ├── test succeeds → use
     │
     ▼
libx264
```

The actual implementation validates a candidate by running a minimal FFmpeg encoding operation.

## Progress

FFmpeg outputs machine-readable progress to stdout.

The service reads `out_time_us`, calculates the percentage from total input duration, and reports it through `IProgress<double>`.

## Cancellation

`CancellationToken` is propagated through long-running operations.

When cancellation occurs, FFmpeg is terminated and partial output is deleted.

## Temporary Files

The merge service creates a uniquely named temporary concat file.

YouTube audio is stored in a temporary application directory.

Cleanup is performed after processing or when the application closes.

## Architectural Trade-offs

The project intentionally remains lightweight. It currently does not require a database, REST API, cloud processing, dependency-injection container, or full MVVM framework.
