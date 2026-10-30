from __future__ import annotations

from pathlib import Path

from PIL import Image
ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets" / "brand" / "submark.png"
PNG = ROOT / "assets" / "logo-512.png"
ICO = ROOT / "assets" / "logo.ico"


def main() -> None:
    with Image.open(SOURCE) as opened:
        source = opened.convert("RGBA")
        bounds = source.getchannel("A").getbbox()
        if bounds is None:
            raise RuntimeError(f"Logo sem conteúdo: {SOURCE}")
        source = source.crop(bounds)
        side = max(source.size)
        margin = max(8, round(side * 0.035))
        canvas = Image.new("RGBA", (side + margin * 2, side + margin * 2), (0, 0, 0, 0))
        canvas.alpha_composite(source, ((canvas.width - source.width) // 2, (canvas.height - source.height) // 2))
        resampling = getattr(Image, "Resampling", Image).LANCZOS
        icon_512 = canvas.resize((512, 512), resampling)
        icon_512.save(PNG, optimize=True)
        icon_512.save(
            ICO,
            format="ICO",
            sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)],
        )
    print(f"Gerados a partir do submark: {PNG} e {ICO}")


if __name__ == "__main__":
    main()
