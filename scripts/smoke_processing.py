from __future__ import annotations

import subprocess
import tempfile
from pathlib import Path

from core.ffmpeg import FFmpegRunner, _creation_flags
from core.models import (
    CompressOptions,
    CompressionPreset,
    SplitMode,
    SplitOptions,
    TaskKind,
    TaskSpec,
    VideoCodec,
    WebmOptions,
)
from core.processor import VideoProcessor
from utils.logging_setup import LOGGER
from utils.paths import resolve_binary


def execute(task: TaskSpec) -> None:
    result = VideoProcessor(FFmpegRunner(LOGGER)).execute(task, lambda *_: None, lambda *_: None, lambda *_: None)
    if not result.outputs or not all(path.is_file() and path.stat().st_size > 0 for path in result.outputs):
        raise RuntimeError(f"Saída inválida em {task.kind}")


def main() -> None:
    ffmpeg = resolve_binary("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("FFmpeg ausente")
    with tempfile.TemporaryDirectory(prefix="fluxo_smoke_") as directory:
        root = Path(directory)
        source = root / "amostra.mp4"
        command = [
            str(ffmpeg), "-y", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=30:duration=6",
            "-f", "lavfi", "-i", "sine=frequency=880:duration=6", "-c:v", "libx264",
            "-preset", "ultrafast", "-crf", "12", "-c:a", "aac", "-shortest", str(source),
        ]
        process = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=_creation_flags())
        if process.wait() != 0:
            raise RuntimeError("Falha ao criar vídeo de teste")
        source_mb = source.stat().st_size / 1024 / 1024
        execute(TaskSpec(TaskKind.SPLIT, source, SplitOptions(SplitMode.PARTS, 2)))
        execute(TaskSpec(TaskKind.COMPRESS, source, CompressOptions(CompressionPreset.CUSTOM, source_mb * .7, VideoCodec.H264, False, False)))
        execute(TaskSpec(TaskKind.WEBM, source, WebmOptions(source_mb * .7, VideoCodec.VP9, False)))
    print("Smoke test FFmpeg: divisão, compactação e WebM concluídos.")


if __name__ == "__main__":
    main()
