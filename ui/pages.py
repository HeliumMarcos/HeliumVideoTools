from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import QSettings, Qt, Signal
from PySide6.QtWidgets import (
    QComboBox,
    QDoubleSpinBox,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QMessageBox,
    QPushButton,
    QStackedWidget,
    QVBoxLayout,
    QWidget,
)

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
from ui.widgets import Card, FileSelectionCard, SizeInput, ToggleSwitch


class BasePage(QWidget):
    runNowRequested = Signal(object)

    def __init__(self, settings: QSettings, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.settings = settings

    def _header(self, layout: QVBoxLayout, title: str, subtitle: str) -> None:
        header = QWidget()
        header_layout = QVBoxLayout(header)
        header_layout.setContentsMargins(0, 0, 0, 0)
        header_layout.setSpacing(5)
        eyebrow = QLabel("FERRAMENTA DE VÍDEO")
        eyebrow.setObjectName("pageEyebrow")
        heading = QLabel(title)
        heading.setObjectName("pageTitle")
        description = QLabel(subtitle)
        description.setObjectName("subtitle")
        description.setWordWrap(True)
        header_layout.addWidget(eyebrow)
        header_layout.addWidget(heading)
        header_layout.addWidget(description)
        layout.addWidget(header)

    def _warn(self, message: str) -> None:
        QMessageBox.warning(self, "Verifique os dados", message)

    @staticmethod
    def _valid_files(paths: list[Path]) -> bool:
        return bool(paths) and all(path.is_file() for path in paths)


class SplitPage(BasePage):
    def __init__(self, settings: QSettings, parent: QWidget | None = None) -> None:
        super().__init__(settings, parent)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(4, 4, 4, 4)
        layout.setSpacing(10)
        self._header(layout, "Dividir vídeo", "Crie partes por quantidade, duração ou tamanho aproximado sem recodificar o vídeo.")
        self.files = FileSelectionCard("Vídeo de entrada", multiple=False)
        self.files.filesChanged.connect(self._update_estimate)
        layout.addWidget(self.files)
        options = Card()
        form = QFormLayout(options)
        _configure_form(form)
        self.mode = QComboBox()
        self.mode.addItem("Número de partes", SplitMode.PARTS)
        self.mode.addItem("Duração por parte", SplitMode.DURATION)
        self.mode.addItem("Tamanho aproximado", SplitMode.SIZE)
        self.parts = QComboBox()
        for count in range(2, 11):
            self.parts.addItem(f"{count} partes", count)
        self.parts.setAccessibleName("Quantidade de partes, de 2 a 10")
        self.number_value = QDoubleSpinBox()
        self.number_value.setAccessibleName("Valor da divisão")
        self.value_stack = QStackedWidget()
        self.value_stack.addWidget(self.parts)
        self.value_stack.addWidget(self.number_value)
        for control in (self.mode, self.parts, self.number_value, self.value_stack):
            control.setMaximumWidth(720)
        self.mode.currentIndexChanged.connect(self._mode_changed)
        self.parts.currentIndexChanged.connect(self._update_estimate)
        self.number_value.valueChanged.connect(self._update_estimate)
        form.addRow("Dividir por", self.mode)
        form.addRow("Quantidade", self.value_stack)
        self.value_label = form.labelForField(self.value_stack)
        self.estimate = QLabel("Adicione um vídeo para estimar o tamanho de cada parte")
        self.estimate.setObjectName("estimateText")
        self.estimate.setWordWrap(True)
        form.addRow("Estimativa", self.estimate)
        note = QLabel("O corte rápido preserva a qualidade; o ponto pode se ajustar ao keyframe mais próximo.")
        note.setObjectName("muted")
        note.setWordWrap(True)
        form.addRow("", note)
        layout.addWidget(options)
        self.primary_button = _add_action_bar(
            layout,
            "As partes serão salvas na pasta do vídeo original.",
            "Dividir",
            self._start,
        )
        layout.addStretch()
        saved_mode = self.settings.value("split/mode", SplitMode.PARTS.value)
        try:
            mode = SplitMode(saved_mode)
        except ValueError:
            mode = SplitMode.PARTS
        index = max(0, self.mode.findData(mode))
        self.mode.setCurrentIndex(index)
        self._mode_changed()

    def _mode_changed(self) -> None:
        mode = _safe_enum(SplitMode, self.mode.currentData(), SplitMode.PARTS)
        if mode == SplitMode.PARTS:
            self.value_label.setText("Quantidade")
            self.value_stack.setCurrentWidget(self.parts)
            saved_parts = max(2, min(10, int(float(self.settings.value("split/parts", 3)))))
            self.parts.setCurrentIndex(saved_parts - 2)
        elif mode == SplitMode.DURATION:
            self.value_label.setText("Duração por parte")
            self.value_stack.setCurrentWidget(self.number_value)
            self.number_value.setDecimals(1); self.number_value.setRange(0.1, 1440); self.number_value.setSuffix(" min")
            self.number_value.setValue(float(self.settings.value("split/minutes", 5)))
        else:
            self.value_label.setText("Tamanho por parte")
            self.value_stack.setCurrentWidget(self.number_value)
            self.number_value.setDecimals(1); self.number_value.setRange(1, 100000); self.number_value.setSuffix(" MB")
            self.number_value.setValue(float(self.settings.value("split/size_mb", 100)))
        self._update_estimate()

    def _update_estimate(self, *_args) -> None:
        paths = self.files.paths()
        if not paths:
            self.estimate.setText("Adicione um vídeo para estimar o tamanho de cada parte")
            return
        try:
            source_mb = paths[0].stat().st_size / 1024 / 1024
        except OSError:
            self.estimate.setText("Não foi possível ler o tamanho do arquivo")
            return
        mode = _safe_enum(SplitMode, self.mode.currentData(), SplitMode.PARTS)
        if mode == SplitMode.PARTS:
            count = int(self.parts.currentData() or 2)
            self.estimate.setText(f"≈ {_format_size(source_mb / count)} por parte  •  {count} arquivos")
        elif mode == SplitMode.SIZE:
            target = self.number_value.value()
            count = max(1, int((source_mb + target - 0.0001) // target))
            if count * target < source_mb:
                count += 1
            self.estimate.setText(f"Até ≈ {_format_size(target)} por parte  •  cerca de {count} arquivos")
        else:
            self.estimate.setText("O tamanho por parte será calculado ao analisar a duração do vídeo")

    def _start(self) -> None:
        paths = self.files.paths()
        if not self._valid_files(paths):
            self._warn("Selecione um arquivo de vídeo existente.")
            return
        mode = _safe_enum(SplitMode, self.mode.currentData(), SplitMode.PARTS)
        raw_value = float(self.parts.currentData()) if mode == SplitMode.PARTS else self.number_value.value()
        value = raw_value * 60 if mode == SplitMode.DURATION else raw_value
        key = {SplitMode.PARTS: "split/parts", SplitMode.DURATION: "split/minutes", SplitMode.SIZE: "split/size_mb"}[mode]
        self.settings.setValue("split/mode", mode.value)
        self.settings.setValue(key, raw_value)
        self.runNowRequested.emit([TaskSpec(TaskKind.SPLIT, paths[0], SplitOptions(mode, value))])


class CompressPage(BasePage):
    def __init__(self, settings: QSettings, parent: QWidget | None = None) -> None:
        super().__init__(settings, parent)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(4, 4, 4, 4)
        layout.setSpacing(10)
        self._header(layout, "Compactar vídeos", "Reduza o tamanho com presets de qualidade ou defina um tamanho final personalizado.")
        self.files = FileSelectionCard("Vídeos para compactar", multiple=True)
        self.files.filesChanged.connect(self._update_estimate)
        layout.addWidget(self.files)
        options = Card()
        form = QFormLayout(options)
        _configure_form(form)
        self.preset = QComboBox()
        self.preset.addItem("Personalizada • tamanho definido por você", CompressionPreset.CUSTOM)
        self.preset.addItem("Baixa compressão • mais qualidade", CompressionPreset.LOW)
        self.preset.addItem("Média • equilibrada", CompressionPreset.MEDIUM)
        self.preset.addItem("Alta compressão • menor arquivo", CompressionPreset.HIGH)
        # O alvo personalizado começa sempre em 98 MB a cada abertura do app.
        # Ele pode ser alterado livremente durante a sessão, mas não é preferência.
        self.target = SizeInput(98, "MB")
        self.target.setAccessibleName("Tamanho final personalizado")
        self.codec = QComboBox()
        self.codec.addItem("H.264 • maior compatibilidade", VideoCodec.H264)
        self.codec.addItem("H.265 • arquivo menor", VideoCodec.H265)
        self.copy_audio = ToggleSwitch("Manter áudio original")
        self.precise = ToggleSwitch("Modo preciso em 2 etapas")
        self.estimate = QLabel()
        self.estimate.setObjectName("estimateText")
        self.estimate.setWordWrap(True)
        form.addRow("Preset", self.preset)
        form.addRow("Tamanho final", self.target)
        form.addRow("Codec", self.codec)
        form.addRow("Áudio", self.copy_audio)
        form.addRow("Precisão", self.precise)
        form.addRow("Estimativa", self.estimate)
        self.form = form
        for control in (self.preset, self.target, self.codec):
            control.setMaximumWidth(720)
        layout.addWidget(options)
        self.primary_button = _add_action_bar(
            layout,
            "Os arquivos compactados serão salvos ao lado dos originais.",
            "Compactar",
            self._start,
        )
        layout.addStretch()
        self.copy_audio.setChecked(settings.value("compress/copy_audio", True, type=bool))
        self.precise.setChecked(settings.value("compress/precise", False, type=bool))
        preset_value = settings.value("compress/preset", CompressionPreset.CUSTOM.value)
        codec_value = settings.value("compress/codec", VideoCodec.H264.value)
        self.preset.setCurrentIndex(max(0, self.preset.findData(_safe_enum(CompressionPreset, preset_value, CompressionPreset.CUSTOM))))
        self.codec.setCurrentIndex(max(0, self.codec.findData(_safe_enum(VideoCodec, codec_value, VideoCodec.H264))))
        self.preset.currentIndexChanged.connect(self._preset_changed)
        self.target.valueChanged.connect(self._update_estimate)
        self._preset_changed()

    def _preset_changed(self) -> None:
        custom = _safe_enum(CompressionPreset, self.preset.currentData(), CompressionPreset.CUSTOM) == CompressionPreset.CUSTOM
        self.form.setRowVisible(self.target, custom)
        self._update_estimate()

    def _update_estimate(self, *_args) -> None:
        paths = self.files.paths()
        if not paths:
            self.estimate.setText("Adicione vídeos para estimar o tamanho final")
            return
        preset = _safe_enum(CompressionPreset, self.preset.currentData(), CompressionPreset.CUSTOM)
        try:
            original = sum(path.stat().st_size for path in paths) / 1024 / 1024
        except OSError:
            return
        ratios = {CompressionPreset.LOW: .75, CompressionPreset.MEDIUM: .55, CompressionPreset.HIGH: .35}
        per_file = self.target.value_mb()
        if preset == CompressionPreset.CUSTOM:
            smallest = min(path.stat().st_size for path in paths) / 1024 / 1024
            if per_file >= smallest:
                self.estimate.setText(
                    f"Reduza o alvo: ele deve ser menor que o menor vídeo ({_format_size(smallest)})"
                )
                return
        predicted = per_file * len(paths) if preset == CompressionPreset.CUSTOM else original * ratios[preset]
        detail = f"até {_format_size(per_file)} por arquivo  •  " if preset == CompressionPreset.CUSTOM else ""
        self.estimate.setText(f"{detail}≈ {_format_size(predicted)} no total  •  valor aproximado")

    def _start(self) -> None:
        paths = self.files.paths()
        if not self._valid_files(paths):
            self._warn("Adicione pelo menos um vídeo existente.")
            return
        preset = _safe_enum(CompressionPreset, self.preset.currentData(), CompressionPreset.CUSTOM)
        codec = _safe_enum(VideoCodec, self.codec.currentData(), VideoCodec.H264)
        target = self.target.value_mb() if preset == CompressionPreset.CUSTOM else None
        if target is not None:
            try:
                invalid = [path.name for path in paths if path.stat().st_size <= target * 1024 * 1024]
            except OSError:
                invalid = []
            if invalid:
                self._warn(
                    "O tamanho final precisa ser menor que cada vídeo original. "
                    "Reduza o valor antes de compactar."
                )
                return
        options = CompressOptions(preset, target, codec, self.copy_audio.isChecked(), self.precise.isChecked())
        self.settings.setValue("compress/preset", preset.value)
        self.settings.setValue("compress/codec", codec.value)
        self.settings.setValue("compress/copy_audio", self.copy_audio.isChecked())
        self.settings.setValue("compress/precise", self.precise.isChecked())
        self.runNowRequested.emit([TaskSpec(TaskKind.COMPRESS, path, options) for path in paths])


class WebmPage(BasePage):
    def __init__(self, settings: QSettings, parent: QWidget | None = None) -> None:
        super().__init__(settings, parent)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(4, 4, 4, 4)
        layout.setSpacing(10)
        self._header(layout, "Converter para WebM", "Converta para VP8 ou VP9 com áudio Opus e limite de tamanho aproximado.")
        self.files = FileSelectionCard("Vídeos para converter", multiple=True)
        self.files.filesChanged.connect(self._update_estimate)
        layout.addWidget(self.files)
        options = Card()
        form = QFormLayout(options)
        _configure_form(form)
        target_value = float(settings.value("webm/target_value", settings.value("webm/target_mb", 299)))
        target_unit = str(settings.value("webm/target_unit", "MB"))
        self.target = SizeInput(target_value, target_unit)
        self.target.setAccessibleName("Limite de tamanho por arquivo")
        self.codec = QComboBox()
        self.codec.addItem("VP8 • conversão mais rápida", VideoCodec.VP8)
        self.codec.addItem("VP9 • melhor compressão, mais lento", VideoCodec.VP9)
        self.precise = ToggleSwitch("Modo preciso em 2 etapas")
        self.precise.setChecked(settings.value("webm/precise", False, type=bool))
        codec_value = settings.value("webm/codec", VideoCodec.VP8.value)
        self.codec.setCurrentIndex(max(0, self.codec.findData(_safe_enum(VideoCodec, codec_value, VideoCodec.VP8))))
        for control in (self.target, self.codec):
            control.setMaximumWidth(720)
        note = QLabel("VP8 prioriza velocidade. Use VP9 quando um arquivo menor for mais importante que o tempo de conversão.")
        note.setObjectName("muted")
        note.setWordWrap(True)
        self.estimate = QLabel("Adicione vídeos para estimar o tamanho final")
        self.estimate.setObjectName("estimateText")
        self.estimate.setWordWrap(True)
        form.addRow("Limite por arquivo", self.target)
        form.addRow("Codec", self.codec)
        form.addRow("Precisão", self.precise)
        form.addRow("Estimativa", self.estimate)
        form.addRow("", note)
        layout.addWidget(options)
        self.primary_button = _add_action_bar(
            layout,
            "Os arquivos WebM serão salvos ao lado dos originais.",
            "Converter",
            self._start,
        )
        layout.addStretch()
        self.target.valueChanged.connect(self._update_estimate)

    def _update_estimate(self, *_args) -> None:
        paths = self.files.paths()
        if not paths:
            self.estimate.setText("Adicione vídeos para estimar o tamanho final")
            return
        try:
            source_sizes = [path.stat().st_size / 1024 / 1024 for path in paths]
        except OSError:
            self.estimate.setText("Não foi possível ler o tamanho dos arquivos")
            return
        limit = self.target.value_mb()
        predicted = sum(min(size * 0.98, limit) for size in source_sizes)
        self.estimate.setText(
            f"Até {_format_size(limit)} por arquivo  •  ≈ {_format_size(predicted)} no total"
        )

    def _start(self) -> None:
        paths = self.files.paths()
        if not self._valid_files(paths):
            self._warn("Adicione pelo menos um vídeo existente.")
            return
        codec = _safe_enum(VideoCodec, self.codec.currentData(), VideoCodec.VP8)
        options = WebmOptions(self.target.value_mb(), codec, self.precise.isChecked())
        self.settings.setValue("webm/target_value", self.target.raw_value())
        self.settings.setValue("webm/target_unit", self.target.unit())
        self.settings.setValue("webm/target_mb", self.target.value_mb())
        self.settings.setValue("webm/codec", codec.value)
        self.settings.setValue("webm/precise", self.precise.isChecked())
        self.runNowRequested.emit([TaskSpec(TaskKind.WEBM, path, options) for path in paths])


def _safe_enum(enum_type, value, fallback):
    try:
        return enum_type(value)
    except (TypeError, ValueError):
        return fallback


def _configure_form(form: QFormLayout) -> None:
    form.setContentsMargins(20, 16, 20, 16)
    form.setHorizontalSpacing(24)
    form.setVerticalSpacing(12)
    form.setLabelAlignment(Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter)
    form.setFieldGrowthPolicy(QFormLayout.FieldGrowthPolicy.AllNonFixedFieldsGrow)


def _add_action_bar(
    layout: QVBoxLayout,
    note_text: str,
    primary_text: str,
    handler,
) -> QPushButton:
    row = QHBoxLayout()
    row.setSpacing(8)
    note = QLabel(note_text)
    note.setObjectName("muted")
    note.setWordWrap(True)
    row.addWidget(note, 1)
    primary_button = QPushButton(primary_text)
    primary_button.setObjectName("accentButton")
    primary_button.setAccessibleName(f"{primary_text} agora")
    primary_button.clicked.connect(handler)
    row.addWidget(primary_button)
    layout.addLayout(row)
    return primary_button


def _format_size(size_mb: float) -> str:
    if size_mb >= 1024:
        return f"{size_mb / 1024:.2f} GB"
    if size_mb < 1:
        return f"{size_mb * 1024:.0f} KB"
    return f"{size_mb:.1f} MB"
