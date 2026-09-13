# Troubleshooting

## `unknown keyword 'ï»¿file'`

### Cause

The concat input file contains a UTF-8 BOM.

### Fix

The current implementation creates the concat file using UTF-8 without BOM.

---

## NVENC cannot initialize

Possible causes:

- No compatible NVIDIA GPU
- Driver/runtime issue
- FFmpeg build limitations

The application should fall back to another encoder and eventually `libx264`.

---

## QSV or AMF does not work

Hardware encoder support depends on the GPU, driver, operating system, and FFmpeg build.

The application tests the candidate encoder before selecting it.

---

## CPU usage is very high

High CPU usage is expected during software H.264 encoding.

Check:

- Whether enhancement is enabled
- Whether hardware encoding was selected
- Whether the operation requires re-encoding

---

## Merge takes much longer with enhancement

Expected behavior. Enhancement requires decoding, filtering, and encoding.

---

## Output has no audio

Check:

1. Whether the input contains an audio stream
2. Whether original audio is enabled
3. Whether external audio is configured
4. Mix vs Replace
5. Start/end values
6. FFmpeg mapping/filter errors

---

## YouTube audio fails

Check:

- URL validity
- Network access
- Availability of an audio-only stream
- YoutubeExplode errors

Use only content you have permission to use.

---

## Progress appears stuck

Progress is based on FFmpeg's `out_time_us`.

4K software encoding can advance slowly while FFmpeg remains active.

Check CPU/GPU usage and output file growth before assuming the process is stuck.

---

## Output file is not created

Check:

1. Output directory permissions
2. File locks
3. Available disk space
4. FFmpeg exit code
5. FFmpeg stderr
6. Input validity

## Reporting a bug

Include:

- Windows version
- Application version/commit
- CPU/GPU
- Input format
- Resolution
- Duration
- Enhancement setting
- Audio configuration
- Encoder
- FFmpeg error output

Do not attach private media.
