from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import Property, QEasingCurve, QPropertyAnimation, QRectF, QSize, Qt, Signal
from PySide6.QtGui import QColor, QDragEnterEvent, QDragMoveEvent, QDropEvent, QPainter, QPalette
from PySide6.QtWidgets import (
    QAbstractButton,
    QAbstractItemView,
    QAbstractSpinBox,
    QComboBox,
    QFileDialog,
    QFrame,
    QHBoxLayout,
    QLabel,
    QListWidget,
    QListWidgetItem,
    QProgressBar,
    QPushButton,
    QSizePolicy,
    QSpinBox,
    QVBoxLayout,
    QWidget,
)

from core.models import SUPPORTED_VIDEO_EXTENSIONS
from utils.paths import asset_path


class Card(QFrame):
    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("card")


class SizeInput(QWidget):
    """Campo numérico com unidade separada e conversão centralizada para MB."""

    valueChanged = Signal(float)
    _TO_MB = {"KB": 1 / 1024, "MB": 1.0, "GB": 1024.0}

    def __init__(self, value: float, unit: str = "MB", parent: QWidget | None = None) -> None:
        super().__init__(parent)
        row = QHBoxLayout(self)
        row.setContentsMargins(0, 0, 0, 0)
        row.setSpacing(10)
        self.number = QSpinBox()
        self.number.setObjectName("sizeNumber")
        self.number.setRange(1, 999_999_999)
        self.number.setSingleStep(1)
        self.number.setButtonSymbols(QAbstractSpinBox.ButtonSymbols.NoButtons)
        self.number.setKeyboardTracking(False)
        self.number.setAccessibleName("Valor do tamanho")
        self.unit_box = QComboBox()
        self.unit_box.setObjectName("sizeUnit")
        self.unit_box.addItems(tuple(self._TO_MB))
        self.unit_box.setFixedWidth(92)
        self.unit_box.setAccessibleName("Unidade do tamanho")
        row.addWidget(self.number, 1)
        row.addWidget(self.unit_box)
        self.setMinimumHeight(36)
        self.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Fixed)
        self.set_value(value, unit)
        self.number.valueChanged.connect(self._emit_value)
        self.unit_box.currentTextChanged.connect(self._emit_value)

    def value_mb(self) -> float:
        return self.number.value() * self._TO_MB[self.unit_box.currentText()]

    def raw_value(self) -> float:
        return self.number.value()

    def unit(self) -> str:
        return self.unit_box.currentText()

    def set_value(self, value: float, unit: str) -> None:
        normalized = unit.upper() if unit.upper() in self._TO_MB else "MB"
        self.number.setValue(max(1, round(float(value))))
        self.unit_box.setCurrentText(normalized)

    def _emit_value(self, *_args) -> None:
        self.valueChanged.emit(self.value_mb())


class ToggleSwitch(QAbstractButton):
    def __init__(self, text: str = "", parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setCheckable(True)
        self.setText(text)
        self.setCursor(Qt.CursorShape.PointingHandCursor)
        self.setAccessibleName(text)
        self._offset = 3.0
        self._animation = QPropertyAnimation(self, b"offset", self)
        self._animation.setDuration(140)
        self._animation.setEasingCurve(QEasingCurve.Type.OutCubic)
        self.toggled.connect(self._animate)

    def sizeHint(self) -> QSize:
        return QSize(56 + self.fontMetrics().horizontalAdvance(self.text()), 32)

    def get_offset(self) -> float:
        return self._offset

    def set_offset(self, value: float) -> None:
        self._offset = value
        self.update()

    offset = Property(float, get_offset, set_offset)

    def _animate(self, checked: bool) -> None:
        self._animation.stop()
        self._animation.setStartValue(self._offset)
        self._animation.setEndValue(23.0 if checked else 3.0)
        self._animation.start()

    def paintEvent(self, event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        track = QRectF(0, 7, 44, 20)
        painter.setPen(Qt.PenStyle.NoPen)
        painter.setBrush(QColor("#0067C0") if self.isChecked() else QColor("#777777"))
        painter.drawRoundedRect(track, 10, 10)
        painter.setBrush(QColor("#FFFFFF"))
        painter.drawEllipse(QRectF(self._offset, 10, 14, 14))
        painter.setPen(self.palette().color(self.foregroundRole()))
        painter.drawText(QRectF(54, 0, self.width() - 54, self.height()), Qt.AlignmentFlag.AlignVCenter, self.text())


class DropList(QListWidget):
    filesChanged = Signal()

    def __init__(self, max_files: int | None = None, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.max_files = max_files
        self.setObjectName("dropList")
        self.setAcceptDrops(True)
        self.viewport().setAcceptDrops(True)
        self.setDragDropMode(QAbstractItemView.DragDropMode.DropOnly)
        self.setDefaultDropAction(Qt.DropAction.CopyAction)
        self.setDragDropOverwriteMode(False)
        self.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        self.setTextElideMode(Qt.TextElideMode.ElideMiddle)
        self.setUniformItemSizes(True)
        self.setSelectionMode(QListWidget.SelectionMode.ExtendedSelection)
        self.setAccessibleName("Arquivos de vídeo selecionados")
        self.setToolTip("Arraste vídeos aqui")
        # 126 px deixam exatamente três linhas completas no viewport com o tema
        # atual; nunca mostramos uma linha pela metade na borda inferior.
        self.setFixedHeight(126)
        self.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Fixed)

    def paintEvent(self, event) -> None:
        super().paintEvent(event)
        if self.count() == 0:
            painter = QPainter(self.viewport())
            painter.setPen(self.palette().color(QPalette.ColorRole.PlaceholderText))
            painter.drawText(
                self.viewport().rect(),
                Qt.AlignmentFlag.AlignCenter,
                "Solte seus vídeos aqui",
            )

    def paths(self) -> list[Path]:
        return [Path(self.item(i).data(Qt.ItemDataRole.UserRole)) for i in range(self.count())]

    def add_paths(self, paths: list[Path]) -> None:
        existing = {str(path).lower() for path in self.paths()}
        for path in paths:
            if not path.is_file() or path.suffix.lower() not in SUPPORTED_VIDEO_EXTENSIONS:
                continue
            if str(path).lower() in existing:
                continue
            if self.max_files == 1:
                self.clear()
                existing.clear()
            try:
                size_mb = path.stat().st_size / 1024 / 1024
                label = f"{path.name}   •   {size_mb:.1f} MB"
            except OSError:
                label = path.name
            item = QListWidgetItem(label)
            item.setToolTip(str(path))
            item.setData(Qt.ItemDataRole.UserRole, str(path))
            self.addItem(item)
            existing.add(str(path).lower())
            if self.max_files and self.count() >= self.max_files:
                break
        self.filesChanged.emit()

    def dragEnterEvent(self, event: QDragEnterEvent) -> None:
        if _has_supported_video(event):
            event.acceptProposedAction()
            self.setProperty("dragActive", True)
            self.style().unpolish(self)
            self.style().polish(self)
        else:
            event.ignore()

    def dragMoveEvent(self, event: QDragMoveEvent) -> None:
        if _has_supported_video(event):
            event.acceptProposedAction()
        else:
            event.ignore()

    def dragLeaveEvent(self, event) -> None:
        self._clear_drag_state()
        super().dragLeaveEvent(event)

    def dropEvent(self, event: QDropEvent) -> None:
        self._clear_drag_state()
        paths = [Path(url.toLocalFile()) for url in event.mimeData().urls() if url.isLocalFile()]
        self.add_paths(paths)
        event.acceptProposedAction()

    def _clear_drag_state(self) -> None:
        self.setProperty("dragActive", False)
        self.style().unpolish(self)
        self.style().polish(self)


class FileSelectionCard(Card):
    filesChanged = Signal()

    def __init__(self, title: str, multiple: bool = True, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setAcceptDrops(True)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(16, 12, 16, 12)
        layout.setSpacing(7)
        title_label = QLabel(title)
        title_label.setObjectName("sectionTitle")
        hint = QLabel("Arraste e solte vídeos aqui ou escolha pelo Explorador de Arquivos")
        hint.setObjectName("muted")
        hint.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents)
        self.list = DropList(None if multiple else 1)
        self.list.filesChanged.connect(self.filesChanged)
        buttons = QHBoxLayout()
        add = QPushButton("Adicionar vídeos" if multiple else "Escolher vídeo")
        add.setIcon(_icon("add.svg"))
        add.clicked.connect(self._browse)
        add.setAccessibleName(add.text())
        remove = QPushButton("Remover")
        remove.clicked.connect(self._remove_selected)
        clear = QPushButton("Limpar")
        clear.clicked.connect(self._clear)
        buttons.addWidget(add)
        buttons.addWidget(remove)
        buttons.addWidget(clear)
        buttons.addStretch()
        layout.addWidget(title_label)
        layout.addWidget(hint)
        layout.addLayout(buttons)
        layout.addWidget(self.list)
        self.multiple = multiple

    def dragEnterEvent(self, event: QDragEnterEvent) -> None:
        if _has_supported_video(event):
            event.acceptProposedAction()
            self.list.setProperty("dragActive", True)
            self.list.style().unpolish(self.list)
            self.list.style().polish(self.list)
        else:
            event.ignore()

    def dragMoveEvent(self, event: QDragMoveEvent) -> None:
        if _has_supported_video(event):
            event.acceptProposedAction()
        else:
            event.ignore()

    def dragLeaveEvent(self, event) -> None:
        self.list._clear_drag_state()
        event.accept()

    def dropEvent(self, event: QDropEvent) -> None:
        self.list._clear_drag_state()
        self.list.add_paths(_video_paths(event))
        event.acceptProposedAction()

    def paths(self) -> list[Path]:
        return self.list.paths()

    def _browse(self) -> None:
        filter_text = "Vídeos (*.mp4 *.mkv *.avi *.mov *.m4v *.webm);;Todos os arquivos (*.*)"
        if self.multiple:
            names, _ = QFileDialog.getOpenFileNames(self, "Selecionar vídeos", "", filter_text)
        else:
            name, _ = QFileDialog.getOpenFileName(self, "Selecionar vídeo", "", filter_text)
            names = [name] if name else []
        self.list.add_paths([Path(name) for name in names])

    def _remove_selected(self) -> None:
        for item in self.list.selectedItems():
            self.list.takeItem(self.list.row(item))
        self.filesChanged.emit()

    def _clear(self) -> None:
        self.list.clear()
        self.filesChanged.emit()


class OverallProgressPanel(Card):
    cancelAllRequested = Signal()
    openOutputRequested = Signal()

    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("overallProgress")
        self.setAccessibleName("Progresso geral das tarefas")
        layout = QVBoxLayout(self)
        layout.setContentsMargins(14, 10, 14, 11)
        layout.setSpacing(6)
        top = QHBoxLayout()
        title = QLabel("Progresso geral")
        title.setObjectName("sectionTitle")
        self.summary = QLabel("Preparando…")
        self.summary.setObjectName("progressSummary")
        self.action_button = QPushButton("Cancelar tudo")
        self.action_button.setObjectName("dangerButton")
        self.action_button.setIcon(_icon("cancel.svg"))
        self.action_button.setAccessibleName("Cancelar todos os processamentos em andamento")
        self.action_button.clicked.connect(self._trigger_action)
        self._action_mode = "cancel"
        top.addWidget(title)
        top.addWidget(self.summary)
        top.addStretch()
        top.addWidget(self.action_button)
        self.status = QLabel("Preparando tarefas…")
        self.status.setObjectName("muted")
        self.status.setTextInteractionFlags(Qt.TextInteractionFlag.TextSelectableByMouse)
        self.progress = QProgressBar()
        self.progress.setObjectName("overallBar")
        self.progress.setRange(0, 1000)
        self.progress.setValue(0)
        layout.addLayout(top)
        layout.addWidget(self.status)
        layout.addWidget(self.progress)

    @property
    def cancel_all(self) -> QPushButton:
        """Alias preservado para integrações e testes de versões anteriores."""
        return self.action_button

    def _trigger_action(self) -> None:
        if self._action_mode == "open":
            self.openOutputRequested.emit()
        else:
            self.cancelAllRequested.emit()

    def update_progress(
        self,
        completed: int,
        total: int,
        percent: float,
        status: str,
        busy: bool,
        can_open_output: bool = False,
    ) -> None:
        self.summary.setText(f"{completed} de {total} concluída(s)  •  {percent:.0f}%")
        self.status.setText(status)
        self.progress.setValue(max(0, min(1000, round(percent * 10))))
        self._action_mode = "cancel" if busy else "open"
        if busy:
            self.action_button.setText("Cancelar tudo")
            self.action_button.setIcon(_icon("cancel.svg"))
            self.action_button.setObjectName("dangerButton")
            self.action_button.setAccessibleName("Cancelar todos os processamentos em andamento")
            self.action_button.setToolTip("Interromper todo o processamento")
            self.action_button.setEnabled(True)
        else:
            self.action_button.setText("Abrir pasta")
            self.action_button.setIcon(_icon("folder.svg"))
            self.action_button.setObjectName("accentButton")
            self.action_button.setAccessibleName("Abrir a pasta do resultado")
            self.action_button.setToolTip("Abrir a pasta onde o resultado foi salvo")
            self.action_button.setEnabled(can_open_output)
        self.action_button.style().unpolish(self.action_button)
        self.action_button.style().polish(self.action_button)


def _icon(name: str):
    from PySide6.QtGui import QIcon
    return QIcon(str(asset_path("icons", name)))


def _video_paths(event) -> list[Path]:
    if not event.mimeData().hasUrls():
        return []
    return [
        Path(url.toLocalFile())
        for url in event.mimeData().urls()
        if url.isLocalFile() and Path(url.toLocalFile()).suffix.lower() in SUPPORTED_VIDEO_EXTENSIONS
    ]


def _has_supported_video(event) -> bool:
    return bool(_video_paths(event))
