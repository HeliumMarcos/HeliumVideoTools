from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum
from pathlib import Path
from uuid import uuid4


SUPPORTED_VIDEO_EXTENSIONS = {".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v"}


class TaskKind(StrEnum):
    SPLIT = "split"
    COMPRESS = "compress"
    WEBM = "webm"


class SplitMode(StrEnum):
    PARTS = "parts"
    DURATION = "duration"
    SIZE = "size"


class CompressionPreset(StrEnum):
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"
    CUSTOM = "custom"


class VideoCodec(StrEnum):
    H264 = "h264"
    H265 = "h265"
    VP8 = "vp8"
    VP9 = "vp9"


@dataclass(frozen=True, slots=True)
class SplitOptions:
    mode: SplitMode = SplitMode.PARTS
    value: float = 3


@dataclass(frozen=True, slots=True)
class CompressOptions:
    preset: CompressionPreset = CompressionPreset.CUSTOM
    target_mb: float | None = 98
    codec: VideoCodec = VideoCodec.H264
    copy_audio: bool = True
    precise: bool = False


@dataclass(frozen=True, slots=True)
class WebmOptions:
    target_mb: float = 299
    codec: VideoCodec = VideoCodec.VP8
    precise: bool = False


@dataclass(frozen=True, slots=True)
class TaskSpec:
    kind: TaskKind
    source: Path
    options: SplitOptions | CompressOptions | WebmOptions
    output_dir: Path | None = None
    id: str = field(default_factory=lambda: uuid4().hex)


@dataclass(frozen=True, slots=True)
class MediaInfo:
    duration: float
    size_bytes: int
    audio_bitrate_kbps: float
    bitrate_kbps: float


@dataclass(frozen=True, slots=True)
class TaskResult:
    outputs: tuple[Path, ...]
    message: str
