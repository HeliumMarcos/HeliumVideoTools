from __future__ import annotations

import math
import shutil
import threading
from pathlib import Path
from typing import Callable

from core.commands import cleanup_passlog, compress_commands, split_command, webm_commands
from core.errors import DependencyError, FluxoError, TaskCancelled
from core.ffmpeg import FFmpegRunner
from core.models import (
    CompressOptions,
    CompressionPreset,
    SplitMode,
    SplitOptions,
    SUPPORTED_VIDEO_EXTENSIONS,
    TaskKind,
    TaskResult,
    TaskSpec,
    WebmOptions,
)

Progress = Callable[[float, str], None]
Message = Callable[[str], None]

PRESET_RATIOS = {
    CompressionPreset.LOW: 0.75,
    CompressionPreset.MEDIUM: 0.55,
    CompressionPreset.HIGH: 0.35,
}


class VideoProcessor:
    def __init__(self, runner: FFmpegRunner) -> None:
        self.runner = runner
        self.cancel_event = threading.Event()

    def cancel(self) -> None:
        self.cancel_event.set()
        self.runner.cancel()

    def execute(self, task: TaskSpec, on_progress: Progress, on_status: Message, on_log: Message) -> TaskResult:
        self._validate_source(task.source)
        if not self.runner.available:
            raise DependencyError("FFmpeg não foi encontrado. Reinstale o aplicativo.")
        if self.cancel_event.is_set():
            raise TaskCancelled("Tarefa cancelada.")
        on_status("Analisando o vídeo…")
        info = self.runner.probe(task.source, self.cancel_event)
        if task.kind == TaskKind.SPLIT:
            assert isinstance(task.options, SplitOptions)
            return self._split(task, task.options, info.duration, info.size_bytes, on_progress, on_status, on_log)
        if task.kind == TaskKind.COMPRESS:
            assert isinstance(task.options, CompressOptions)
            return self._compress(task, task.options, info, on_progress, on_status, on_log)
        assert isinstance(task.options, WebmOptions)
        return self._webm(task, task.options, info, on_progress, on_status, on_log)

    @staticmethod
    def _validate_source(source: Path) -> None:
        if not source.is_file():
            raise FluxoError("O arquivo selecionado não existe mais.")
        if source.suffix.lower() not in SUPPORTED_VIDEO_EXTENSIONS:
            raise FluxoError("Formato não suportado. Use MP4, MKV, AVI, MOV, M4V ou WebM.")

    @staticmethod
    def _output_dir(task: TaskSpec) -> Path:
        output_dir = task.output_dir or task.source.parent
        if not output_dir.is_dir():
            raise FluxoError("A pasta de saída não existe.")
        return output_dir

    @staticmethod
    def _ensure_space(output_dir: Path, expected_bytes: int) -> None:
        try:
            free = shutil.disk_usage(output_dir).free
        except OSError as exc:
            raise FluxoError("Não foi possível verificar o espaço disponível no disco.") from exc
        required = int(expected_bytes * 1.15) + 32 * 1024 * 1024
        if free < required:
            required_mb = required / 1024 / 1024
            free_mb = free / 1024 / 1024
            raise FluxoError(f"Espaço insuficiente: são necessários cerca de {required_mb:.0f} MB e há {free_mb:.0f} MB livres.")

    @staticmethod
    def _unique(path: Path) -> Path:
        if not path.exists():
            return path
        for index in range(2, 10_000):
            candidate = path.with_name(f"{path.stem} ({index}){path.suffix}")
            if not candidate.exists():
                return candidate
        raise FluxoError("Há arquivos demais com o mesmo nome na pasta de saída.")

    def _split(self, task: TaskSpec, options: SplitOptions, duration: float, size: int,
               on_progress: Progress, on_status: Message, on_log: Message) -> TaskResult:
        if options.value <= 0:
            raise FluxoError("Informe um valor de divisão maior que zero.")
        if options.mode == SplitMode.PARTS:
            part_count = int(options.value)
            if part_count < 2:
                raise FluxoError("Escolha pelo menos duas partes.")
            segment_seconds = duration / part_count
        elif options.mode == SplitMode.DURATION:
            segment_seconds = options.value
            part_count = math.ceil(duration / segment_seconds)
        else:
            target_bytes = options.value * 1024 * 1024
            if target_bytes >= size:
                raise FluxoError("O tamanho por parte deve ser menor que o arquivo original.")
            segment_seconds = duration * target_bytes / size
            part_count = math.ceil(duration / segment_seconds)
        if segment_seconds < 1:
            raise FluxoError("Cada parte ficaria com menos de um segundo. Aumente o valor escolhido.")
        output_dir = self._output_dir(task)
        self._ensure_space(output_dir, size)
        outputs: list[Path] = []
        assert self.runner.ffmpeg
        for index in range(part_count):
            if self.cancel_event.is_set():
                raise TaskCancelled("Tarefa cancelada.")
            start = index * segment_seconds
            length = min(segment_seconds, duration - start)
            output = self._unique(output_dir / f"{task.source.stem}_parte_{index + 1:03d}.mp4")
            on_status(f"Criando parte {index + 1} de {part_count}…")
            on_log(f"Parte {index + 1}/{part_count}: {output.name}")
            try:
                self.runner.run(
                    split_command(self.runner.ffmpeg, task.source, output, start, length),
                    length, self.cancel_event,
                    lambda percent, text, i=index: on_progress((i + percent / 100) / part_count * 100, text),
                    on_log, f"Parte {index + 1}/{part_count}",
                )
            except TaskCancelled:
                output.unlink(missing_ok=True)
                raise
            outputs.append(output)
        return TaskResult(tuple(outputs), f"{len(outputs)} partes criadas")

    def _compress(self, task: TaskSpec, options: CompressOptions, info,
                  on_progress: Progress, on_status: Message, on_log: Message) -> TaskResult:
        source_mb = info.size_bytes / 1024 / 1024
        if options.preset == CompressionPreset.CUSTOM:
            if not options.target_mb or options.target_mb <= 0:
                raise FluxoError("Informe um tamanho final válido.")
            target_mb = options.target_mb
        else:
            target_mb = source_mb * PRESET_RATIOS[options.preset]
        if target_mb >= source_mb:
            raise FluxoError("O tamanho final precisa ser menor que o arquivo original.")
        output_dir = self._output_dir(task)
        self._ensure_space(output_dir, int(target_mb * 1024 * 1024))
        output = self._unique(output_dir / f"{task.source.stem}_compactado.mp4")
        assert self.runner.ffmpeg
        commands, passlog = compress_commands(
            self.runner.ffmpeg, task.source, output, options.codec, info.duration,
            target_mb, info.audio_bitrate_kbps, options.copy_audio, options.precise,
        )
        try:
            self._run_passes(commands, info.duration, on_progress, on_status, on_log, "Compactando")
        except TaskCancelled:
            output.unlink(missing_ok=True)
            raise
        finally:
            cleanup_passlog(passlog)
        return TaskResult((output,), f"Vídeo compactado para aproximadamente {target_mb:.0f} MB")

    def _webm(self, task: TaskSpec, options: WebmOptions, info,
              on_progress: Progress, on_status: Message, on_log: Message) -> TaskResult:
        source_mb = info.size_bytes / 1024 / 1024
        target_mb = min(options.target_mb, source_mb * 0.98)
        if target_mb <= 0:
            raise FluxoError("Informe um limite de tamanho válido.")
        output_dir = self._output_dir(task)
        self._ensure_space(output_dir, int(target_mb * 1024 * 1024))
        output = self._unique(output_dir / f"{task.source.stem}_{options.codec.value}.webm")
        assert self.runner.ffmpeg
        commands, passlog = webm_commands(
            self.runner.ffmpeg, task.source, output, options.codec, info.duration,
            target_mb, info.audio_bitrate_kbps, options.precise,
        )
        try:
            self._run_passes(commands, info.duration, on_progress, on_status, on_log, "Convertendo")
        except TaskCancelled:
            output.unlink(missing_ok=True)
            raise
        finally:
            cleanup_passlog(passlog)
        return TaskResult((output,), f"WebM criado com limite aproximado de {target_mb:.0f} MB")

    def _run_passes(self, commands: list[list[str]], duration: float, on_progress: Progress,
                    on_status: Message, on_log: Message, verb: str) -> None:
        total = len(commands)
        for index, command in enumerate(commands):
            if self.cancel_event.is_set():
                raise TaskCancelled("Tarefa cancelada.")
            stage = f"{verb} • etapa {index + 1}/{total}"
            on_status(stage + "…")
            self.runner.run(
                command, duration, self.cancel_event,
                lambda percent, text, i=index: on_progress((i + percent / 100) / total * 100, text),
                on_log, stage,
            )
