# Design Decisions

## WPF

The application targets Windows desktop users and requires local filesystem access, configuration controls, and long-running progress.

## FFmpeg

FFmpeg handles codec, muxing, filtering, and audio-processing complexity so application code can focus on orchestration and user experience.

## FFprobe

FFprobe provides structured media metadata without requiring the application to implement media-container parsing.

## External Process Model

FFmpeg is executed as a child process.

This provides a clean boundary between application logic and the multimedia engine, at the cost of needing careful process lifecycle and stdout/stderr handling.

## Async Processing

Video operations can be long-running, so asynchronous process execution keeps the WPF UI responsive.

## Hardware Fallback

Hardware encoding is an optimization rather than a dependency. The application validates candidates and falls back to software encoding.

## Fast Path

When no video enhancement or external audio is required, video stream copying avoids unnecessary re-encoding.

## Local Processing

Media remains on the user's machine instead of being uploaded to a remote processing service.

## Temporary Files

Unique temporary files are used for concat lists and downloaded audio, with cleanup after processing.

## Current Trade-offs

The project intentionally avoids unnecessary infrastructure such as a database, REST API, cloud processing, or a full MVVM framework.
