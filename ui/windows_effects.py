from __future__ import annotations

import ctypes
import sys

from PySide6.QtWidgets import QWidget


def enable_windows_11_backdrop(window: QWidget, dark: bool) -> None:
    """Ativa cantos nativos e backdrop Mica quando suportados pelo Windows."""
    if sys.platform != "win32":
        return
    try:
        hwnd = int(window.winId())
        dwm = ctypes.windll.dwmapi.DwmSetWindowAttribute
        corner = ctypes.c_int(2)  # DWMWCP_ROUND
        dwm(hwnd, 33, ctypes.byref(corner), ctypes.sizeof(corner))
        backdrop = ctypes.c_int(2)  # DWMSBT_MAINWINDOW
        dwm(hwnd, 38, ctypes.byref(backdrop), ctypes.sizeof(backdrop))
        dark_value = ctypes.c_int(1 if dark else 0)
        dwm(hwnd, 20, ctypes.byref(dark_value), ctypes.sizeof(dark_value))
    except Exception:
        pass
