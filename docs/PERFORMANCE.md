# Performance Engineering

## Objective

The application is designed to process high-resolution dash-cam footage without unnecessary re-encoding.

## Copy Path

When enhancement is disabled and no external audio is being processed:

```text
Input videos
     │
     ▼
Concat demuxer
     │
     ▼
Video stream copy
     │
     ▼
Output
```

This avoids decoding and re-encoding the video stream.

## Enhanced Path

When enhancement is enabled:

```text
Input
  │
  ▼
Decode
  │
  ▼
Filter
  │
  ├── brightness
  ├── contrast
  ├── saturation
  └── sharpening
  │
  ▼
Encode
  │
  ▼
Output
```

This is substantially more expensive, particularly for 4K video.

## Hardware Encoding

The current candidates are:

1. `h264_nvenc`
2. `h264_qsv`
3. `h264_amf`
4. `libx264`

Hardware candidates are tested before use.

## Why 4K Matters

4K increases:

- Pixels processed per frame
- Decode workload
- Filter workload
- Encode workload
- Memory bandwidth requirements
- Storage I/O

Performance should therefore be measured using representative source media.

## Key Engineering Lesson

> The fastest video-processing pipeline is often the one that avoids video processing entirely.

Hardware acceleration is valuable, but eliminating unnecessary re-encoding is the first optimization to consider.
