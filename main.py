from __future__ import annotations

import multiprocessing
import sys

from PySide6.QtCore import qVersion
from PySide6.QtGui import QFont, QIcon
from PySide6.QtWidgets import QApplication, QMessageBox

from config.settings import APP_CONFIG
from ui.main_window import MainWindow
from utils.logging_setup import LOGGER
from utils.paths import asset_path


def _exception_hook(exc_type, exc_value, traceback) -> None:
    LOGGER.error("Erro não tratado", exc_info=(exc_type, exc_value, traceback))
    QMessageBox.critical(None, "Erro inesperado", "O Helium Vídeo Tools encontrou um problema inesperado. Os detalhes foram salvos no arquivo de log.")


def main() -> int:
    multiprocessing.freeze_support()
    app = QApplication(sys.argv)
    app.setOrganizationName(APP_CONFIG.organization)
    app.setApplicationName(APP_CONFIG.product_name)
    app.setApplicationVersion(APP_CONFIG.version)
    app.setWindowIcon(QIcon(str(asset_path("logo.ico"))))
    app.setFont(QFont("Segoe UI Variable", 10))
    sys.excepthook = _exception_hook
    window = MainWindow()
    window.show()
    LOGGER.info("Janela principal exibida")
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
