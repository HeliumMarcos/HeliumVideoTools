from __future__ import annotations

import os
import shutil
import sys
from pathlib import Path

from config.settings import APP_CONFIG


def resource_root() -> Path:
    bundle = getattr(sys, "_MEIPASS", None)
    return Path(bundle) if bundle else Path(__file__).resolve().parents[1]


def asset_path(*parts: str) -> Path:
    return resource_root().joinpath("assets", *parts)


def app_data_dir() -> Path:
    roaming = Path(os.environ.get("APPDATA", Path.home() / "AppData" / "Roaming"))
    path = roaming / APP_CONFIG.executable_name
    path.mkdir(parents=True, exist_ok=True)
    return path


def resolve_binary(name: str) -> Path | None:
    executable_name = f"{name}.exe" if sys.platform == "win32" else name
    candidates = [
        resource_root() / executable_name,
        Path(sys.executable).resolve().parent / executable_name,
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    located = shutil.which(executable_name)
    return Path(located) if located else None
