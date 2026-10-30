from __future__ import annotations

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "assets" / "brand" / "source"
OUTPUT_DIR = ROOT / "assets" / "brand"

ASSETS = {
    "master-horizontal-source.png": "master-horizontal.png",
    "alternativa-vertical-source.png": "alternativa-vertical.png",
    "isotipo-source.png": "isotipo.png",
    "submark-source.png": "submark.png",
}


def clean_transparency(source: Path, destination: Path) -> None:
    """Remove resíduos semitransparentes e recorta sem alterar a arte da marca."""
    with Image.open(source) as opened:
        image = opened.convert("RGBA")
        alpha = image.getchannel("A").point(lambda value: 0 if value < 96 else value)
        image.putalpha(alpha)
        bounds = alpha.getbbox()
        if bounds is None:
            raise RuntimeError(f"A imagem ficou vazia: {source}")
        left, top, right, bottom = bounds
        padding = 4
        image = image.crop(
            (
                max(0, left - padding),
                max(0, top - padding),
                min(image.width, right + padding),
                min(image.height, bottom + padding),
            )
        )
        image.save(destination, optimize=True)


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for source_name, destination_name in ASSETS.items():
        clean_transparency(SOURCE_DIR / source_name, OUTPUT_DIR / destination_name)
        print(f"Preparado: {OUTPUT_DIR / destination_name}")


if __name__ == "__main__":
    main()
