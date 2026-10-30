from __future__ import annotations

import logging
import re
import subprocess
import sys
import threading
import time
from collections import deque
from pathlib import Path
from typing import Callable, Sequence

from core.errors import DependencyError, FluxoError, TaskCancelled
from core.models import MediaInfo
from utils.paths import resolve_binary

ProgressCallback = Callable[[float, str], None]
LogCallback = Callable[[str], None]


def _creation_flags() -> int:
    return subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0


class FFmpegRunner:
    """Executa FFmpeg/FFprobe fora da UI e mantém um processo cancelável."""

    def __init__(self, logger: logging.Logger) -> None:
        self.logger = logger
        self.ffmpeg = resolve_binary("ffmpeg")
        self._process: subprocess.Popen[str] | None = None
        self._lock = threading.Lock()

    @property
    def available(self) -> bool:
        return bool(self.ffmpeg)

    def cancel(self) -> None:
        with self._lock:
            process = self._process
        if process and process.poll() is None:
            self.logger.info("Cancelamento solicitado ao processo FFmpeg")
            try:
                # Não aguarda aqui: cancel() é chamado pela thread da interface.
                # O worker confirma o encerramento sem bloquear a interface.
                process.terminate()
            except OSError:
                self.logger.warning("O FFmpeg já havia encerrado ao receber o cancelamento")

    def probe(self, source: Path, cancel_event: threading.Event) -> MediaInfo:
        if not self.ffmpeg:
            raise DependencyError("FFmpeg não foi encontrado. Reinstale o aplicativo.")
        command = [
            str(self.ffmpeg), "-hide_banner", "-i", str(source),
        ]
        output = self._capture(command, cancel_event, allow_nonzero=True)
        try:
            duration_match = re.search(r"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", output)
            if not duration_match:
                raise ValueError("duration")
            hours, minutes, seconds = duration_match.groups()
            duration = int(hours) * 3600 + int(minutes) * 60 + float(seconds)
            size = source.stat().st_size
            duration_line = next((line for line in output.splitlines() if "Duration:" in line), "")
            bitrate_match = re.search(r"bitrate:\s*(\d+(?:\.\d+)?)\s*kb/s", duration_line)
            bitrate = float(bitrate_match.group(1)) if bitrate_match else 0.0
            audio_bitrate = 0.0
            audio_streams = 0
            for line in output.splitlines():
                if " Audio:" not in line:
                    continue
                audio_streams += 1
                rates = re.findall(r"(\d+(?:\.\d+)?)\s*kb/s", line)
                audio_bitrate += float(rates[-1]) if rates else 128.0
        except (TypeError, ValueError, OSError) as exc:
            raise FluxoError("Não foi possível ler as informações desse vídeo.") from exc
        if duration <= 0:
            raise FluxoError("O vídeo não possui uma duração válida.")
        if bitrate <= 0:
            bitrate = (size * 8 / 1000) / duration
        return MediaInfo(duration, size, audio_bitrate if audio_streams else 0.0, bitrate)

    def _capture(self, command: Sequence[str], cancel_event: threading.Event, allow_nonzero: bool = False) -> str:
        self.logger.info("Analisando mídia: %s", command[-1])
        process = subprocess.Popen(
            list(command), stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            stdin=subprocess.DEVNULL, text=True, encoding="utf-8", errors="replace",
            creationflags=_creation_flags(),
        )
        with self._lock:
            self._process = process
        try:
            while True:
                if cancel_event.is_set():
                    self.cancel()
                    raise TaskCancelled("Tarefa cancelada.")
                try:
                    stdout, stderr = process.communicate(timeout=0.2)
                    break
                except subprocess.TimeoutExpired:
                    continue
            if cancel_event.is_set():
                raise TaskCancelled("Tarefa cancelada.")
            if process.returncode and not allow_nonzero:
                self.logger.error("Análise falhou: %s", stderr[-2000:])
                raise FluxoError("O arquivo não pôde ser analisado. Ele pode estar corrompido ou usar um formato incompatível.")
            return stdout + "\n" + stderr
        finally:
            with self._lock:
                self._process = None

    def run(
        self,
        command: Sequence[str],
        duration: float,
        cancel_event: threading.Event,
        on_progress: ProgressCallback,
        on_log: LogCallback,
        stage: str,
    ) -> None:
        if not self.ffmpeg:
            raise DependencyError("FFmpeg não foi encontrado. Reinstale o aplicativo.")
        self.logger.info("Executando FFmpeg (%s): %s", stage, " ".join(map(str, command)))
        process = subprocess.Popen(
            list(map(str, command)), stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            stdin=subprocess.DEVNULL, text=True, encoding="utf-8", errors="replace",
            bufsize=1, creationflags=_creation_flags(),
        )
        with self._lock:
            self._process = process
        started = time.monotonic()
        last_lines: deque[str] = deque(maxlen=18)
        last_percent = -1.0
        try:
            assert process.stdout is not None
            for raw_line in iter(process.stdout.readline, ""):
                line = raw_line.strip()
                if cancel_event.is_set():
                    self.cancel()
                    raise TaskCancelled("Tarefa cancelada.")
                if not line:
                    continue
                last_lines.append(line)
                if line.startswith(("out_time_us=", "out_time_ms=")):
                    try:
                        elapsed_media = int(line.split("=", 1)[1]) / 1_000_000
                        percent = min(99.5, max(0.0, elapsed_media / max(duration, 0.01) * 100))
                        if percent - last_percent >= 0.2:
                            wall = time.monotonic() - started
                            speed = elapsed_media / wall if wall else 0
                            eta = (duration - elapsed_media) / speed if speed > 0 else 0
                            eta_text = _format_eta(max(0, eta))
                            on_progress(percent, f"{stage} • {percent:.0f}% • restante {eta_text}")
                            last_percent = percent
                    except (ValueError, IndexError):
                        pass
                elif line.startswith("progress=end"):
                    on_progress(100.0, f"{stage} • concluído")
                elif "error" in line.lower() or "invalid" in line.lower():
                    on_log(line)
            return_code = process.wait()
            if cancel_event.is_set():
                raise TaskCancelled("Tarefa cancelada.")
            if return_code:
                details = "\n".join(last_lines)
                self.logger.error("FFmpeg terminou com código %s: %s", return_code, details)
                raise FluxoError(_human_ffmpeg_error(details))
        finally:
            with self._lock:
                self._process = None


def _format_eta(seconds: float) -> str:
    seconds = int(seconds)
    if seconds < 60:
        return f"{seconds}s"
    hours, remainder = divmod(seconds, 3600)
    minutes, secs = divmod(remainder, 60)
    return f"{hours}h {minutes:02d}m" if hours else f"{minutes}m {secs:02d}s"


def _human_ffmpeg_error(details: str) -> str:
    lowered = details.lower()
    if "permission denied" in lowered:
        return "Sem permissão para gravar o arquivo de saída. Escolha outra pasta ou feche o arquivo em uso."
    if "no space left" in lowered:
        return "O disco ficou sem espaço durante o processamento."
    if "invalid data" in lowered:
        return "O vídeo contém dados inválidos ou está corrompido."
    if "unknown encoder" in lowered:
        return "Este FFmpeg não inclui o codificador necessário para a operação."
    return "O FFmpeg não conseguiu concluir o processamento. Consulte o arquivo de log para detalhes."
