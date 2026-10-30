from __future__ import annotations

import ctypes
import sys

from PySide6.QtCore import Qt
from PySide6.QtGui import QColor, QPalette
from PySide6.QtWidgets import QApplication

from config.settings import ThemeMode
from utils.paths import asset_path


LIGHT = {
    "window": "#F3F3F3", "surface": "#FFFFFF", "surface2": "#F9F9F9",
    "border": "#D8D8D8", "text": "#1A1A1A", "muted": "#616161",
    "hover": "#EAEAEA", "accent": "#0067C0", "accent_hover": "#005A9E",
    "success": "#0F7B0F", "danger": "#C42B1C", "warning": "#9D5D00",
}
DARK = {
    "window": "#202020", "surface": "#2B2B2B", "surface2": "#262626",
    "border": "#454545", "text": "#FFFFFF", "muted": "#B3B3B3",
    "hover": "#383838", "accent": "#60CDFF", "accent_hover": "#4CC2FF",
    "success": "#6CCB5F", "danger": "#FF99A4", "warning": "#FCE100",
}


def system_high_contrast() -> bool:
    if sys.platform != "win32":
        return False
    try:
        class HIGHCONTRAST(ctypes.Structure):
            _fields_ = [("cbSize", ctypes.c_uint), ("dwFlags", ctypes.c_uint), ("lpszDefaultScheme", ctypes.c_void_p)]
        info = HIGHCONTRAST(ctypes.sizeof(HIGHCONTRAST), 0, None)
        ctypes.windll.user32.SystemParametersInfoW(0x0042, info.cbSize, ctypes.byref(info), 0)
        return bool(info.dwFlags & 0x1)
    except Exception:
        return False


def resolve_dark(mode: ThemeMode) -> bool:
    if mode == ThemeMode.DARK:
        return True
    if mode == ThemeMode.LIGHT:
        return False
    return QApplication.styleHints().colorScheme() == Qt.ColorScheme.Dark


def apply_theme(app: QApplication, mode: ThemeMode) -> bool:
    dark = resolve_dark(mode)
    colors = DARK if dark else LIGHT
    if system_high_contrast():
        app.setStyleSheet("")
        app.setPalette(QApplication.style().standardPalette())
        return dark
    palette = QPalette()
    palette.setColor(QPalette.ColorRole.Window, QColor(colors["window"]))
    palette.setColor(QPalette.ColorRole.WindowText, QColor(colors["text"]))
    palette.setColor(QPalette.ColorRole.Base, QColor(colors["surface"]))
    palette.setColor(QPalette.ColorRole.Text, QColor(colors["text"]))
    palette.setColor(QPalette.ColorRole.Highlight, QColor(colors["accent"]))
    palette.setColor(QPalette.ColorRole.HighlightedText, QColor("#FFFFFF" if not dark else "#101010"))
    app.setPalette(palette)
    app.setStyleSheet(build_qss(colors))
    return dark


def build_qss(c: dict[str, str]) -> str:
    arrow = asset_path("icons", "chevron-down.svg").as_posix()
    return f"""
    * {{ font-family: "Segoe UI Variable", "Segoe UI"; font-size: 10pt; color: {c['text']}; }}
    QMainWindow, QWidget#appRoot {{ background: {c['window']}; }}
    QFrame#sidebar, QFrame#card {{
        background: {c['surface']}; border: 1px solid {c['border']}; border-radius: 8px;
    }}
    QFrame#overallProgress {{
        background: {c['surface']}; border: 1px solid {c['accent']}; border-radius: 10px;
    }}
    QFrame#sidebar {{ border-radius: 0px; border-top: 0; border-bottom: 0; border-left: 0; }}
    QFrame#brandCard {{
        background: qlineargradient(x1:0, y1:0, x2:1, y2:1,
            stop:0 {c['surface2']}, stop:1 {c['hover']});
        border: 1px solid {c['border']}; border-radius: 12px;
    }}
    QLabel#brandLogo {{ background: transparent; border: 0; }}
    QLabel#brandTitle {{ color: {c['text']}; font-size: 15pt; font-weight: 700; letter-spacing: 1px; }}
    QLabel#brandProduct {{ color: {c['accent']}; font-size: 8pt; font-weight: 700; letter-spacing: 2px; }}
    QLabel#brandVersion {{ color: {c['muted']}; font-size: 8pt; }}
    QLabel#brandTagline {{ color: {c['muted']}; font-size: 8pt; padding-top: 3px; }}
    QLabel#pageEyebrow {{ color: {c['accent']}; font-size: 8pt; font-weight: 700; letter-spacing: 2px; }}
    QLabel#pageTitle {{ font-size: 24pt; font-weight: 650; }}
    QLabel#sectionTitle {{ font-size: 12pt; font-weight: 600; }}
    QLabel#subtitle, QLabel#muted {{ color: {c['muted']}; }}
    QLabel#estimateText {{
        color: {c['text']}; background: {c['surface2']}; border: 1px solid {c['border']};
        border-radius: 6px; padding: 8px 10px;
    }}
    QPushButton {{
        min-height: 34px; padding: 0 14px; background: {c['surface2']};
        border: 1px solid {c['border']}; border-radius: 8px;
    }}
    QPushButton:hover {{ background: {c['hover']}; }}
    QPushButton:pressed {{ padding-top: 2px; background: {c['border']}; }}
    QPushButton:focus {{ border: 2px solid {c['accent']}; }}
    QPushButton:disabled {{ color: {c['muted']}; background: {c['surface2']}; }}
    QPushButton#accentButton {{ color: white; background: #0067C0; border-color: #0067C0; font-weight: 600; }}
    QPushButton#accentButton:hover {{ background: #005A9E; }}
    QPushButton#dangerButton {{ color: {c['danger']}; }}
    QPushButton#navButton {{ text-align: left; padding-left: 14px; border: 0; background: transparent; min-height: 42px; }}
    QPushButton#navButton:hover {{ background: {c['hover']}; }}
    QPushButton#navButton:checked {{ background: {c['hover']}; border-left: 3px solid {c['accent']}; padding-left: 11px; font-weight: 600; }}
    QLineEdit, QSpinBox, QDoubleSpinBox, QComboBox {{
        min-height: 34px; padding: 0 10px; background: {c['surface2']};
        border: 1px solid {c['border']}; border-radius: 6px; selection-background-color: {c['accent']};
    }}
    QLineEdit:focus, QSpinBox:focus, QDoubleSpinBox:focus, QComboBox:focus {{ border-bottom: 2px solid {c['accent']}; }}
    QComboBox::drop-down {{ border: 0; width: 32px; }}
    QComboBox::down-arrow {{ image: url("{arrow}"); width: 12px; height: 12px; }}
    QSpinBox#sizeNumber {{ padding-left: 12px; }}
    QComboBox#sizeUnit {{ font-weight: 600; }}
    QComboBox QAbstractItemView {{ background: {c['surface']}; border: 1px solid {c['border']}; selection-background-color: {c['hover']}; }}
    QListWidget#dropList {{
        background: {c['surface2']}; border: 1px dashed {c['border']}; border-radius: 8px;
        padding: 8px; outline: 0;
    }}
    QListWidget#dropList[dragActive="true"] {{ border: 2px solid {c['accent']}; background: {c['hover']}; }}
    QListWidget::item {{ min-height: 30px; border-radius: 5px; padding: 3px 6px; }}
    QListWidget::item:selected {{ background: {c['hover']}; color: {c['text']}; }}
    QProgressBar {{
        min-height: 8px; max-height: 8px; border: 0; border-radius: 4px;
        background: {c['border']}; text-align: center; color: transparent;
    }}
    QProgressBar::chunk {{ background: {c['accent']}; border-radius: 4px; }}
    QScrollArea {{ border: 0; background: transparent; }}
    QScrollArea > QWidget > QWidget {{ background: transparent; }}
    QScrollBar:vertical {{ background: transparent; width: 10px; margin: 2px; }}
    QScrollBar::handle:vertical {{ background: {c['border']}; border-radius: 4px; min-height: 28px; }}
    QScrollBar::handle:vertical:hover {{ background: {c['muted']}; }}
    QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0; }}
    QToolTip {{ background: {c['surface']}; color: {c['text']}; border: 1px solid {c['border']}; padding: 5px; }}
    QLabel#successText {{ color: {c['success']}; }}
    QLabel#errorText {{ color: {c['danger']}; }}
    QLabel#progressSummary {{ color: {c['accent']}; font-weight: 600; }}
    QProgressBar#overallBar {{ min-height: 10px; max-height: 10px; border-radius: 5px; }}
    QProgressBar#overallBar::chunk {{ border-radius: 5px; }}
    """
