from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum


class ThemeMode(StrEnum):
    AUTO = "auto"
    LIGHT = "light"
    DARK = "dark"


@dataclass(frozen=True, slots=True)
class AppConfig:
    organization: str = "Helium"
    product_name: str = "Helium Vídeo Tools"
    executable_name: str = "HeliumVideoTools"
    version: str = "2.6.0"
    max_parallel_tasks: int = 1
    theme: ThemeMode = ThemeMode.AUTO


APP_CONFIG = AppConfig()
