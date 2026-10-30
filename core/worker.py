from __future__ import annotations

from PySide6.QtCore import QObject, QRunnable, Signal, Slot

from core.errors import TaskCancelled
from core.ffmpeg import FFmpegRunner
from core.models import TaskResult, TaskSpec
from core.processor import VideoProcessor
from utils.logging_setup import LOGGER


class WorkerSignals(QObject):
    started = Signal()
    progress = Signal(float, str)
    status = Signal(str)
    log = Signal(str)
    succeeded = Signal(object)
    failed = Signal(str)
    canceled = Signal()
    finished = Signal()


class VideoTaskWorker(QRunnable):
    def __init__(self, task: TaskSpec) -> None:
        super().__init__()
        self.task = task
        self.signals = WorkerSignals()
        self.processor = VideoProcessor(FFmpegRunner(LOGGER))

    @Slot()
    def run(self) -> None:
        self.signals.started.emit()
        LOGGER.info("Tarefa iniciada", extra={"task_id": self.task.id, "kind": self.task.kind.value})
        try:
            result: TaskResult = self.processor.execute(
                self.task, self.signals.progress.emit, self.signals.status.emit, self.signals.log.emit,
            )
        except TaskCancelled:
            LOGGER.warning("Tarefa cancelada: %s", self.task.id)
            self.signals.canceled.emit()
        except Exception as exc:
            LOGGER.exception("Falha na tarefa %s", self.task.id)
            message = str(exc).strip() or "Não foi possível concluir a tarefa."
            self.signals.failed.emit(message)
        else:
            LOGGER.info("Tarefa concluída: %s", self.task.id)
            self.signals.succeeded.emit(result)
        finally:
            self.signals.finished.emit()

    def cancel(self) -> None:
        self.processor.cancel()
