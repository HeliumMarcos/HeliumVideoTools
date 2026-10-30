from __future__ import annotations

import sys
import tempfile
from pathlib import Path
from uuid import uuid4

from core.errors import FluxoError
from core.models import VideoCodec


def video_bitrate_kbps(duration: float, target_mb: float, audio_kbps: float, overhead: float = 0.96) -> int:
    available = target_mb * 8192 * overhead - audio_kbps * duration
    result = int(available / duration)
    if result < 80:
        raise FluxoError("O tamanho escolhido é pequeno demais para a duração e o áudio deste vídeo.")
    return result


def split_command(ffmpeg: Path, source: Path, output: Path, start: float, length: float) -> list[str]:
    return [
        str(ffmpeg), "-y", "-hide_banner", "-ss", f"{start:.3f}", "-i", str(source),
        "-t", f"{length:.3f}", "-map", "0", "-c", "copy",
        "-avoid_negative_ts", "make_zero", "-movflags", "+faststart",
        "-progress", "pipe:1", "-nostats", str(output),
    ]


def compress_commands(
    ffmpeg: Path,
    source: Path,
    output: Path,
    codec: VideoCodec,
    duration: float,
    target_mb: float,
    source_audio_kbps: float,
    copy_audio: bool,
    precise: bool,
) -> tuple[list[list[str]], Path | None]:
    audio_kbps = source_audio_kbps if copy_audio else 128
    bitrate = video_bitrate_kbps(duration, target_mb, audio_kbps)
    encoder = "libx264" if codec == VideoCodec.H264 else "libx265"
    profile = "high" if codec == VideoCodec.H264 else "main"
    video = [
        "-c:v", encoder, "-b:v", f"{bitrate}k", "-maxrate", f"{bitrate * 2}k",
        "-bufsize", f"{bitrate * 4}k", "-preset", "medium", "-profile:v", profile,
        "-pix_fmt", "yuv420p",
    ]
    audio = ["-c:a", "copy"] if copy_audio else ["-c:a", "aac", "-b:a", "128k"]
    prefix = [str(ffmpeg), "-y", "-hide_banner", "-i", str(source)]
    progress = ["-progress", "pipe:1", "-nostats"]
    if not precise:
        return [prefix + video + audio + ["-movflags", "+faststart"] + progress + [str(output)]], None
    passlog = Path(tempfile.gettempdir()) / f"fluxo_{uuid4().hex}"
    null_target = "NUL" if sys.platform == "win32" else "/dev/null"
    pass1 = prefix + video + ["-pass", "1", "-passlogfile", str(passlog), "-an", "-f", "null"] + progress + [null_target]
    pass2 = prefix + video + ["-pass", "2", "-passlogfile", str(passlog)] + audio + ["-movflags", "+faststart"] + progress + [str(output)]
    return [pass1, pass2], passlog


def webm_commands(
    ffmpeg: Path,
    source: Path,
    output: Path,
    codec: VideoCodec,
    duration: float,
    target_mb: float,
    source_audio_kbps: float,
    precise: bool,
) -> tuple[list[list[str]], Path | None]:
    audio_kbps = int(min(160, max(64, source_audio_kbps or 128)))
    bitrate = video_bitrate_kbps(duration, target_mb, audio_kbps, overhead=0.95)
    encoder = "libvpx" if codec == VideoCodec.VP8 else "libvpx-vp9"
    cpu_used = "4" if codec == VideoCodec.VP8 else "3"
    prefix = [str(ffmpeg), "-y", "-hide_banner", "-i", str(source)]
    video = ["-c:v", encoder, "-b:v", f"{bitrate}k", "-deadline", "good", "-cpu-used", cpu_used]
    audio = ["-c:a", "libopus", "-b:a", f"{audio_kbps}k"]
    progress = ["-progress", "pipe:1", "-nostats"]
    if not precise:
        return [prefix + video + ["-crf", "24"] + audio + progress + [str(output)]], None
    passlog = Path(tempfile.gettempdir()) / f"fluxo_{uuid4().hex}"
    null_target = "NUL" if sys.platform == "win32" else "/dev/null"
    pass1 = prefix + video + ["-pass", "1", "-passlogfile", str(passlog), "-an", "-f", "webm"] + progress + [null_target]
    pass2 = prefix + video + ["-pass", "2", "-passlogfile", str(passlog)] + audio + progress + [str(output)]
    return [pass1, pass2], passlog


def cleanup_passlog(base: Path | None) -> None:
    if not base:
        return
    for suffix in ("-0.log", "-0.log.mbtree", "-0.log.temp"):
        try:
            base.with_name(base.name + suffix).unlink(missing_ok=True)
        except OSError:
            pass
