from __future__ import annotations

from pathlib import Path

from PIL import Image
from PySide6.QtCore import Qt
from PySide6.QtGui import QGuiApplication, QImage, QPainter
from PySide6.QtSvg import QSvgRenderer
ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets" / "logo.svg"
PNG = ROOT / "assets" / "logo-512.png"
ICO = ROOT / "assets" / "logo.ico"


def render_svg(source: Path, output: Path, width: int, height: int) -> None:
    renderer = QSvgRenderer(str(source))
    if not renderer.isValid():
        raise RuntimeError(f"SVG inválido: {source}")
    image = QImage(width, height, QImage.Format.Format_ARGB32_Premultiplied)
    image.fill(Qt.GlobalColor.transparent)
    painter = QPainter(image)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    renderer.render(painter)
    painter.end()
    if not image.save(str(output)):
        raise RuntimeError(f"Falha ao salvar: {output}")


def main() -> None:
    app = QGuiApplication.instance() or QGuiApplication([])
    render_svg(SOURCE, PNG, 512, 512)
    with Image.open(PNG) as opened:
        opened.convert("RGBA").save(
            ICO,
            format="ICO",
            sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)],
        )
    for theme in ("dark", "light"):
        render_svg(ROOT / "assets" / "brand" / f"logo-signature-{theme}.svg",
                   ROOT / "assets" / "brand" / f"logo-signature-{theme}.png", 860, 256)
    print(f"Gerados a partir da marca vetorial aprovada: {PNG}, {ICO} e assinaturas")


if __name__ == "__main__":
    main()
