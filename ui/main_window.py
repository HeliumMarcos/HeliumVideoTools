from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import QSettings, QSize, Qt, QThreadPool, QUrl
from PySide6.QtGui import QCloseEvent, QDesktopServices, QDragEnterEvent, QDropEvent, QIcon, QPixmap
from PySide6.QtWidgets import (
    QApplication,
    QButtonGroup,
    QComboBox,
    QFrame,
    QHBoxLayout,
    QLabel,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QScrollArea,
    QSizePolicy,
    QStackedWidget,
    QVBoxLayout,
    QWidget,
)

from config.settings import APP_CONFIG, ThemeMode
from core.models import (
    SUPPORTED_VIDEO_EXTENSIONS,
    TaskResult,
    TaskSpec,
)
from core.worker import VideoTaskWorker
from ui.pages import CompressPage, SplitPage, WebmPage
from ui.theme import apply_theme
from ui.widgets import OverallProgressPanel
from ui.windows_effects import enable_windows_11_backdrop
from utils.logging_setup import LOGGER
from utils.paths import asset_path, resolve_binary


class MainWindow(QMainWindow):
    def __init__(self) -> None:
        super().__init__()
        self.settings = QSettings(APP_CONFIG.organization, APP_CONFIG.product_name)
        self._migrate_settings()
        self.thread_pool = QThreadPool(self)
        self.thread_pool.setMaxThreadCount(APP_CONFIG.max_parallel_tasks)
        self.workers: dict[str, VideoTaskWorker] = {}
        self.batch_task_ids: set[str] = set()
        self.task_progress: dict[str, float] = {}
        self.task_states: dict[str, str] = {}
        self.task_statuses: dict[str, str] = {}
        self.task_names: dict[str, str] = {}
        self.canceling_ids: set[str] = set()
        self.last_output_dir: Path | None = None
        self._dark = False
        self.setAcceptDrops(True)
        self.setWindowTitle(APP_CONFIG.product_name)
        self.setWindowIcon(QIcon(str(asset_path("logo.ico"))))
        self.setMinimumSize(980, 700)
        self.resize(1280, 900)
        self._build_ui()
        geometry = self.settings.value("window/geometry")
        if geometry:
            self.restoreGeometry(geometry)
        self._apply_saved_theme()
        QApplication.styleHints().colorSchemeChanged.connect(self._system_theme_changed)

    def _build_ui(self) -> None:
        root = QWidget()
        root.setObjectName("appRoot")
        self.setCentralWidget(root)
        outer = QHBoxLayout(root)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(0)

        sidebar = QFrame()
        sidebar.setObjectName("sidebar")
        sidebar.setFixedWidth(276)
        side = QVBoxLayout(sidebar)
        side.setContentsMargins(14, 16, 14, 14)
        side.setSpacing(8)
        brand_card = QFrame()
        brand_card.setObjectName("brandCard")
        brand_outer = QVBoxLayout(brand_card)
        brand_outer.setContentsMargins(14, 14, 14, 12)
        brand_outer.setSpacing(10)
        brand = QHBoxLayout()
        brand.setSpacing(12)
        logo = QLabel()
        logo.setObjectName("brandLogo")
        pixmap = QPixmap(str(asset_path("logo-512.png")))
        logo.setPixmap(pixmap.scaled(58, 58, Qt.AspectRatioMode.KeepAspectRatio, Qt.TransformationMode.SmoothTransformation))
        logo.setFixedSize(58, 58)
        name_box = QVBoxLayout()
        name_box.setSpacing(0)
        name = QLabel("HELIUM")
        name.setObjectName("brandTitle")
        product = QLabel("VÍDEO TOOLS")
        product.setObjectName("brandProduct")
        version = QLabel(f"v{APP_CONFIG.version}  •  Desktop")
        version.setObjectName("brandVersion")
        name_box.addWidget(name)
        name_box.addWidget(product)
        name_box.addWidget(version)
        brand.addWidget(logo)
        brand.addLayout(name_box)
        brand.addStretch()
        tagline = QLabel("Divida  •  compacte  •  converta")
        tagline.setObjectName("brandTagline")
        brand_outer.addLayout(brand)
        brand_outer.addWidget(tagline)
        side.addWidget(brand_card)
        side.addSpacing(12)

        self.stack = QStackedWidget()
        self.pages = [SplitPage(self.settings), CompressPage(self.settings), WebmPage(self.settings)]
        nav_data = [
            ("Dividir", "split.svg"), ("Compactar", "compress.svg"), ("Converter", "convert.svg"),
        ]
        self.nav_group = QButtonGroup(self)
        self.nav_group.setExclusive(True)
        for index, ((label, icon_name), page) in enumerate(zip(nav_data, self.pages)):
            button = QPushButton(label)
            button.setObjectName("navButton")
            button.setIcon(QIcon(str(asset_path("icons", icon_name))))
            button.setIconSize(QSize(22, 22))
            button.setCheckable(True)
            button.setAccessibleName(f"Abrir ferramenta {label}")
            button.clicked.connect(lambda checked=False, i=index: self._navigate(i))
            self.nav_group.addButton(button, index)
            side.addWidget(button)
            self.stack.addWidget(page)
            page.runNowRequested.connect(self._add_tasks)
            if index == 0:
                button.setChecked(True)
        # A tela inicial é sempre Compactar; a navegação durante o uso continua normal.
        saved_page = 1
        self.stack.setCurrentIndex(saved_page)
        self.nav_group.button(saved_page).setChecked(True)
        side.addStretch()
        theme_label = QLabel("Tema")
        theme_label.setObjectName("muted")
        self.theme_combo = QComboBox()
        self.theme_combo.addItem("Automático", ThemeMode.AUTO)
        self.theme_combo.addItem("Claro", ThemeMode.LIGHT)
        self.theme_combo.addItem("Escuro", ThemeMode.DARK)
        self.theme_combo.setAccessibleName("Tema do aplicativo")
        self.theme_combo.currentIndexChanged.connect(self._theme_selected)
        side.addWidget(theme_label)
        side.addWidget(self.theme_combo)
        outer.addWidget(sidebar)

        content = QWidget()
        content_layout = QVBoxLayout(content)
        content_layout.setContentsMargins(24, 20, 24, 12)
        content_layout.setSpacing(10)
        page_scroll = QScrollArea()
        page_scroll.setObjectName("pageScroll")
        page_scroll.setFrameShape(QFrame.Shape.NoFrame)
        page_scroll.setWidgetResizable(True)
        page_scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        page_scroll.setVerticalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAsNeeded)
        page_scroll.setWidget(self.stack)
        # O scroll absorve janelas baixas/escala de texto alta; os formulários nunca
        # são comprimidos abaixo do mínimo e, portanto, não se sobrepõem.
        self.stack.setMinimumHeight(max(page.minimumSizeHint().height() for page in self.pages))
        self.stack.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.MinimumExpanding)
        content_layout.addWidget(page_scroll, 1)
        self.overall_progress = OverallProgressPanel()
        self.overall_progress.cancelAllRequested.connect(self._cancel_all_tasks)
        self.overall_progress.openOutputRequested.connect(self._open_output_folder)
        self.overall_progress.hide()
        content_layout.addWidget(self.overall_progress)
        footer = QHBoxLayout()
        self.app_status = QLabel("Pronto")
        self.app_status.setObjectName("muted")
        ffmpeg = resolve_binary("ffmpeg")
        self.ffmpeg_status = QLabel("● FFmpeg detectado" if ffmpeg else "● FFmpeg ausente")
        self.ffmpeg_status.setObjectName("successText" if ffmpeg else "errorText")
        self.ffmpeg_status.setAccessibleName(self.ffmpeg_status.text())
        footer.addWidget(self.app_status)
        footer.addStretch()
        footer.addWidget(self.ffmpeg_status)
        content_layout.addLayout(footer)
        outer.addWidget(content, 1)

    def _migrate_settings(self) -> None:
        """Aplica novos padrões uma única vez sem sobrescrever escolhas futuras."""
        revision = int(self.settings.value("defaults/revision", 0))
        if revision < 2:
            self.settings.setValue("compress/copy_audio", True)
            self.settings.setValue("webm/codec", "vp8")
            current_parts = int(float(self.settings.value("split/parts", 3)))
            self.settings.setValue("split/parts", max(2, min(10, current_parts)))
            self.settings.setValue("defaults/revision", 2)
        if revision < 3:
            self.settings.setValue("compress/preset", "custom")
            self.settings.setValue("compress/target_value", 98)
            self.settings.setValue("compress/target_unit", "MB")
            self.settings.setValue("compress/target_mb", 98)
            if not self.settings.contains("webm/target_value"):
                self.settings.setValue("webm/target_value", self.settings.value("webm/target_mb", 299))
                self.settings.setValue("webm/target_unit", "MB")
            self.settings.setValue("defaults/revision", 3)
        if revision < 4:
            # Descarta apenas uma geometria legada que podia reabrir a interface
            # pequena demais; novos redimensionamentos voltam a ser persistidos.
            self.settings.remove("window/geometry")
            self.settings.setValue("window/page", 1)
            self.settings.setValue("defaults/revision", 4)

    def _navigate(self, index: int) -> None:
        self.stack.setCurrentIndex(index)
        self.settings.setValue("window/page", index)

    @staticmethod
    def _dropped_videos(event) -> list[Path]:
        if not event.mimeData().hasUrls():
            return []
        return [
            Path(url.toLocalFile())
            for url in event.mimeData().urls()
            if url.isLocalFile() and Path(url.toLocalFile()).suffix.lower() in SUPPORTED_VIDEO_EXTENSIONS
        ]

    def dragEnterEvent(self, event: QDragEnterEvent) -> None:
        if self._dropped_videos(event):
            event.acceptProposedAction()
        else:
            event.ignore()

    def dropEvent(self, event: QDropEvent) -> None:
        paths = self._dropped_videos(event)
        if not paths:
            event.ignore()
            return
        self.pages[self.stack.currentIndex()].files.list.add_paths(paths)
        event.acceptProposedAction()

    def _add_tasks(self, tasks: list[TaskSpec]) -> None:
        if not tasks:
            return
        if not self.workers:
            self.batch_task_ids.clear()
            self.task_progress.clear()
            self.task_states.clear()
            self.task_statuses.clear()
            self.task_names.clear()
            self.canceling_ids.clear()
            self.last_output_dir = None
        for task in tasks:
            self.batch_task_ids.add(task.id)
            self.task_progress[task.id] = 0.0
            self.task_states[task.id] = "pending"
            self.task_statuses[task.id] = "Aguardando início"
            self.task_names[task.id] = task.source.name
            self._start_task(task, priority=10)
        self.app_status.setText(f"Processando {len(tasks)} arquivo(s)")
        self.overall_progress.show()
        self._update_overall_progress()

    def _start_task(self, task: TaskSpec, priority: int = 0) -> None:
        worker = VideoTaskWorker(task)
        self.workers[task.id] = worker
        worker.signals.started.connect(
            lambda task_id=task.id: self._task_started(task_id)
        )
        worker.signals.progress.connect(
            lambda percent, text, task_id=task.id: self._task_progress_changed(task_id, percent, text)
        )
        worker.signals.status.connect(
            lambda text, task_id=task.id: self._task_status_changed(task_id, text)
        )
        worker.signals.log.connect(lambda message, task_id=task.id: LOGGER.info("%s: %s", task_id, message))
        worker.signals.succeeded.connect(
            lambda result, task_id=task.id, name=task.source.name: self._task_succeeded(task_id, name, result)
        )
        worker.signals.failed.connect(
            lambda message, task_id=task.id: self._task_failed(task_id, message)
        )
        worker.signals.canceled.connect(lambda task_id=task.id: self._task_canceled(task_id))
        worker.signals.finished.connect(lambda task_id=task.id: self._worker_finished(task_id))
        self.thread_pool.start(worker, priority)

    def _cancel_task(self, task_id: str) -> None:
        worker = self.workers.get(task_id)
        if worker:
            self.canceling_ids.add(task_id)
            self.task_states[task_id] = "canceling"
            self.task_statuses[task_id] = "Cancelando e removendo…"
            worker.cancel()
            if self.thread_pool.tryTake(worker):
                self.workers.pop(task_id, None)
            self._remove_canceled_task(task_id)
            self.app_status.setText("Processamento cancelado")

    def _cancel_all_tasks(self) -> None:
        task_ids = tuple(self.workers)
        if not task_ids:
            return
        for task_id in task_ids:
            self._cancel_task(task_id)
        if self.workers:
            self.app_status.setText("Cancelando todas as tarefas em andamento…")

    def _task_started(self, task_id: str) -> None:
        if task_id not in self.batch_task_ids:
            return
        self.task_states[task_id] = "running"
        self.task_statuses[task_id] = "Iniciando processamento…"
        self._update_overall_progress()

    def _task_progress_changed(self, task_id: str, percent: float, text: str) -> None:
        if task_id not in self.batch_task_ids:
            return
        self.task_progress[task_id] = max(0.0, min(100.0, percent))
        self.task_statuses[task_id] = text
        self._update_overall_progress()

    def _task_status_changed(self, task_id: str, text: str) -> None:
        if task_id not in self.batch_task_ids:
            return
        self.task_statuses[task_id] = text
        self._update_overall_progress()

    def _task_succeeded(self, task_id: str, name: str, result: TaskResult) -> None:
        if task_id not in self.batch_task_ids:
            return
        if result.outputs:
            self.last_output_dir = result.outputs[0].parent
        self.task_states[task_id] = "success"
        self.task_progress[task_id] = 100.0
        self.task_statuses[task_id] = "Concluído"
        self.app_status.setText(f"Concluído: {name}")
        self._update_overall_progress()

    def _task_failed(self, task_id: str, message: str) -> None:
        if task_id not in self.batch_task_ids:
            return
        self.task_states[task_id] = "error"
        self.task_progress[task_id] = 100.0
        self.task_statuses[task_id] = f"Erro: {message}"
        self.app_status.setText(f"Erro: {message}")
        self._update_overall_progress()

    def _task_canceled(self, task_id: str) -> None:
        self._remove_canceled_task(task_id)
        self.app_status.setText("Processamento cancelado")

    def _remove_canceled_task(self, task_id: str) -> None:
        self.canceling_ids.discard(task_id)
        self.batch_task_ids.discard(task_id)
        self.task_progress.pop(task_id, None)
        self.task_states.pop(task_id, None)
        self.task_statuses.pop(task_id, None)
        self.task_names.pop(task_id, None)
        self._update_overall_progress()

    def _update_overall_progress(self) -> None:
        task_ids = [task_id for task_id in self.batch_task_ids if task_id in self.task_states]
        if not task_ids:
            self.overall_progress.hide()
            return

        total = len(task_ids)
        completed = sum(self.task_states[task_id] in {"success", "error"} for task_id in task_ids)
        percent = sum(self.task_progress.get(task_id, 0.0) for task_id in task_ids) / total
        busy = any(self.task_states[task_id] in {"pending", "running", "canceling"} for task_id in task_ids)
        active_id = next(
            (task_id for task_id in task_ids if self.task_states[task_id] in {"running", "canceling"}),
            None,
        )
        if active_id:
            status = f"{self.task_names.get(active_id, 'Tarefa')} • {self.task_statuses.get(active_id, '')}"
        else:
            pending_count = sum(self.task_states[task_id] == "pending" for task_id in task_ids)
            error_count = sum(self.task_states[task_id] == "error" for task_id in task_ids)
            if pending_count:
                status = f"{pending_count} tarefa(s) aguardando início"
            elif error_count:
                status = (
                    self.task_statuses[task_ids[0]]
                    if total == 1
                    else f"Processamento finalizado com {error_count} erro(s)"
                )
            else:
                status = "Todas as tarefas foram concluídas"
        self.overall_progress.show()
        self.overall_progress.update_progress(
            completed,
            total,
            percent,
            status,
            busy,
            can_open_output=self.last_output_dir is not None,
        )

    def _open_output_folder(self) -> None:
        output_dir = self.last_output_dir
        if output_dir is None or not output_dir.is_dir():
            self.app_status.setText("A pasta do resultado não está mais disponível")
            return
        if not QDesktopServices.openUrl(QUrl.fromLocalFile(str(output_dir))):
            self.app_status.setText("Não foi possível abrir a pasta do resultado")

    def _worker_finished(self, task_id: str) -> None:
        self.workers.pop(task_id, None)
        self.canceling_ids.discard(task_id)
        self._update_overall_progress()
        if not self.workers and self.batch_task_ids:
            self.app_status.setText("Todas as tarefas foram processadas")
            QApplication.beep()

    def _apply_saved_theme(self) -> None:
        raw = self.settings.value("appearance/theme", ThemeMode.AUTO.value)
        try:
            mode = ThemeMode(raw)
        except ValueError:
            mode = ThemeMode.AUTO
        index = max(0, self.theme_combo.findData(mode))
        self.theme_combo.blockSignals(True)
        self.theme_combo.setCurrentIndex(index)
        self.theme_combo.blockSignals(False)
        self._apply_theme(mode)

    def _theme_selected(self) -> None:
        try:
            mode = ThemeMode(self.theme_combo.currentData())
        except (TypeError, ValueError):
            mode = ThemeMode.AUTO
        self.settings.setValue("appearance/theme", mode.value)
        self._apply_theme(mode)

    def _apply_theme(self, mode: ThemeMode) -> None:
        self._dark = apply_theme(QApplication.instance(), mode)
        enable_windows_11_backdrop(self, self._dark)

    def _system_theme_changed(self) -> None:
        if self.theme_combo.currentData() == ThemeMode.AUTO.value:
            self._apply_theme(ThemeMode.AUTO)

    def showEvent(self, event) -> None:
        super().showEvent(event)
        enable_windows_11_backdrop(self, self._dark)

    def closeEvent(self, event: QCloseEvent) -> None:
        if self.workers:
            answer = QMessageBox.question(
                self, "Tarefas em andamento",
                "Cancelar as tarefas em andamento e fechar o Helium Vídeo Tools?",
                QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                QMessageBox.StandardButton.No,
            )
            if answer != QMessageBox.StandardButton.Yes:
                event.ignore()
                return
            for worker in tuple(self.workers.values()):
                worker.cancel()
            self.thread_pool.waitForDone(3000)
        self.settings.setValue("window/geometry", self.saveGeometry())
        event.accept()
