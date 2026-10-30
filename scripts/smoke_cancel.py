from __future__ import annotations

import os
import subprocess
import tempfile
import time
from pathlib import Path

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtWidgets import QApplication

from core.ffmpeg import _creation_flags
from core.models import CompressOptions, CompressionPreset, TaskKind, TaskSpec, VideoCodec
from ui.main_window import MainWindow
from utils.paths import resolve_binary


def main() -> None:
    ffmpeg = resolve_binary("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("FFmpeg ausente")
    with tempfile.TemporaryDirectory(prefix="helium_cancel_") as directory:
        root = Path(directory)
        source = root / "cancelamento.mp4"
        command = [
            str(ffmpeg), "-y", "-f", "lavfi", "-i",
            "testsrc2=size=1280x720:rate=30:duration=45", "-c:v", "libx264",
            "-preset", "ultrafast", "-crf", "12", "-an", str(source),
        ]
        process = subprocess.Popen(
            command,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=_creation_flags(),
        )
        if process.wait() != 0:
            raise RuntimeError("Falha ao criar o vídeo de cancelamento")

        app = QApplication.instance() or QApplication([])
        window = MainWindow()
        target_mb = max(1.0, source.stat().st_size / 1024 / 1024 * 0.2)
        task = TaskSpec(
            TaskKind.COMPRESS,
            source,
            CompressOptions(CompressionPreset.CUSTOM, target_mb, VideoCodec.H264, False, False),
        )
        window._add_tasks([task])
        deadline = time.monotonic() + 5
        while task.id in window.workers and window.task_states.get(task.id) != "running":
            app.processEvents()
            if time.monotonic() > deadline:
                raise RuntimeError("A tarefa não iniciou a tempo")
            time.sleep(0.01)

        started = time.monotonic()
        window._cancel_task(task.id)
        cancel_call_seconds = time.monotonic() - started
        if task.id in window.batch_task_ids:
            raise RuntimeError("A tarefa cancelada permaneceu no progresso geral")
        deadline = time.monotonic() + 8
        while task.id in window.workers:
            app.processEvents()
            if time.monotonic() > deadline:
                raise RuntimeError("A tarefa cancelada não encerrou")
            time.sleep(0.01)
        window.close()
        if cancel_call_seconds > 0.25:
            raise RuntimeError(f"Cancelamento bloqueou a interface por {cancel_call_seconds:.2f}s")
        print(f"Smoke cancelamento: removido do progresso; resposta da UI em {cancel_call_seconds:.3f}s.")


if __name__ == "__main__":
    main()
