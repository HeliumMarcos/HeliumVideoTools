"""Renderiza propostas vetoriais; não altera ícones ou identidade ativos."""
from pathlib import Path

from PySide6.QtCore import Qt, QRectF
from PySide6.QtGui import QColor, QFont, QFontDatabase, QGuiApplication, QImage, QPainter, QPainterPath, QPen
from PySide6.QtSvg import QSvgRenderer
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets" / "brand" / "proposals"


def render_icon(name: str, size: int) -> QImage:
    image = QImage(size, size, QImage.Format.Format_ARGB32_Premultiplied)
    image.fill(Qt.GlobalColor.transparent)
    painter = QPainter(image)
    renderer = QSvgRenderer(str(OUT / f"{name}.svg"))
    if not renderer.isValid():
        raise RuntimeError(f"SVG inválido: {name}")
    renderer.render(painter)
    painter.end()
    return image


def text(p: QPainter, x: float, y: float, value: str, size: float, color: str, weight=QFont.Weight.Normal):
    font = QFont("Segoe UI", int(size), weight)
    p.setFont(font)
    p.setPen(QColor(color))
    p.drawText(x, y, value)


def brand(p: QPainter, x: float, y: float, icon_name: str, dark=True):
    p.drawImage(QRectF(x, y, 88, 88), render_icon(icon_name, 256))
    text(p, x + 108, y + 43, "Helium", 37, "#F5F7FA" if dark else "#17212B", QFont.Weight.Bold)
    text(p, x + 109, y + 71, "VÍDEO", 15, "#70D5FF" if dark else "#0067C0", QFont.Weight.DemiBold)
    # O rótulo Tools ganha forma de uma pequena ferramenta/etiqueta da marca.
    p.setPen(Qt.PenStyle.NoPen)
    p.setBrush(QColor("#174D70" if dark else "#DAEEFA"))
    p.drawRoundedRect(QRectF(x + 190, y + 51, 73, 26), 6, 6)
    text(p, x + 201, y + 70, "TOOLS", 12, "#D8F2FF" if dark else "#005B9C", QFont.Weight.DemiBold)


def vector_text(x, y, value, size, weight, color):
    path = QPainterPath()
    font = QFont("Segoe UI", size, weight)
    path.addText(x, y, font, value)
    pieces = []
    i = 0
    while i < path.elementCount():
        e = path.elementAt(i)
        if e.isMoveTo():
            pieces.append(f"M{e.x:.3f},{e.y:.3f}")
        elif e.isLineTo():
            pieces.append(f"L{e.x:.3f},{e.y:.3f}")
        else:
            b, c = path.elementAt(i + 1), path.elementAt(i + 2)
            pieces.append(f"C{e.x:.3f},{e.y:.3f} {b.x:.3f},{b.y:.3f} {c.x:.3f},{c.y:.3f}")
            i += 2
        i += 1
    return f'<path fill="{color}" d="{" ".join(pieces)}"/>'


def export_signature(name, dark):
    icon = (OUT / f"{name}.svg").read_text(encoding="utf-8")
    content = icon.split(">", 1)[1].rsplit("</svg>", 1)[0]
    suffix = "dark" if dark else "light"
    markup = '<svg xmlns="http://www.w3.org/2000/svg" width="430" height="128" viewBox="0 0 430 128">'
    markup += '<title>Helium Vídeo Tools — assinatura proposta</title>'
    markup += '<g>' + content + '</g>'
    markup += vector_text(148, 61, "Helium", 37, QFont.Weight.Bold, "#F5F7FA" if dark else "#17212B")
    markup += vector_text(149, 89, "VÍDEO", 15, QFont.Weight.DemiBold, "#70D5FF" if dark else "#0067C0")
    markup += f'<rect x="230" y="69" width="73" height="26" rx="6" fill="{"#174D70" if dark else "#DAEEFA"}"/>'
    markup += vector_text(241, 88, "TOOLS", 12, QFont.Weight.DemiBold, "#D8F2FF" if dark else "#005B9C")
    markup += '</svg>'
    (OUT / f"{name}-signature-{suffix}.svg").write_text(markup, encoding="utf-8")


def main():
    app = QGuiApplication.instance() or QGuiApplication([])
    OUT.mkdir(parents=True, exist_ok=True)
    for name in ("video-flow", "video-cut"):
        for size in (16, 32, 48, 64, 128, 256, 512):
            render_icon(name, size).save(str(OUT / f"{name}-{size}.png"))
        with Image.open(OUT / f"{name}-512.png") as icon:
            icon.save(OUT / f"{name}.ico", sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
        export_signature(name, True)
        export_signature(name, False)
    board = QImage(1440, 1120, QImage.Format.Format_ARGB32_Premultiplied)
    board.fill(QColor("#1C2024"))
    p = QPainter(board)
    p.setRenderHint(QPainter.RenderHint.Antialiasing)
    text(p, 56, 66, "Helium Vídeo Tools", 29, "#F5F7FA", QFont.Weight.Bold)
    text(p, 56, 104, "Proposta de identidade • mesma família dos seus aplicativos", 15, "#B9C6CE")
    for i, (file, label) in enumerate((("video-flow", "A • Vídeo em movimento"), ("video-cut", "B • Vídeo em partes"))):
        x = 48 + i * 688
        p.setPen(QPen(QColor("#3B444C"), 1))
        p.setBrush(QColor("#252B30"))
        p.drawRoundedRect(QRectF(x, 142, 656, 500), 18, 18)
        text(p, x + 28, 185, label, 20, "#F5F7FA", QFont.Weight.DemiBold)
        p.drawImage(QRectF(x + 232, 217, 192, 192), render_icon(file, 512))
        brand(p, x + 124, 443, file)
        text(p, x + 28, 586, "Play + saída / transformação" if i == 0 else "Play + marcas de corte / enquadramento", 14, "#C2CDD5")
        text(p, x + 28, 616, "Recomendada para as três ferramentas" if i == 0 else "Alternativa mais geométrica e minimalista", 12, "#8BD5F5")
    text(p, 56, 700, "ASSINATURA APLICADA AO CABEÇALHO", 12, "#AFBDC7", QFont.Weight.DemiBold)
    p.setPen(Qt.PenStyle.NoPen)
    p.setBrush(QColor("#242424")); p.drawRoundedRect(QRectF(48, 724, 656, 148), 14, 14)
    brand(p, 74, 751, "video-flow")
    p.setBrush(QColor("#F3F5F7")); p.drawRoundedRect(QRectF(736, 724, 656, 148), 14, 14)
    brand(p, 762, 751, "video-flow", False)
    text(p, 56, 926, "LEITURA EM TAMANHOS REAIS", 12, "#AFBDC7", QFont.Weight.DemiBold)
    x = 62
    for size in (16, 32, 48, 64):
        p.drawImage(QRectF(x, 956 + (64-size)/2, size, size), render_icon("video-flow", size))
        text(p, x, 1052, f"{size} px", 11, "#B9C6CE")
        x += size + 70
    text(p, 620, 980, "Azul da família • borda escura • símbolo branco", 15, "#D7E1E8")
    text(p, 620, 1015, "Sem órbitas ou detalhes que se perdem no Windows.", 13, "#AFBDC7")
    p.end()
    board.save(str(OUT / "comparacao-propostas.png"))
    before = QImage(1120, 430, QImage.Format.Format_ARGB32_Premultiplied)
    before.fill(QColor("#202020"))
    p = QPainter(before); p.setRenderHint(QPainter.RenderHint.Antialiasing)
    text(p, 44, 48, "ANTES", 13, "#B9C6CE", QFont.Weight.DemiBold)
    p.drawImage(QRectF(44, 86, 72, 72), QImage(str(ROOT / "assets" / "brand" / "submark.png")))
    text(p, 132, 117, "HELIUM", 24, "#F3F3F3", QFont.Weight.Bold)
    text(p, 132, 147, "VÍDEO TOOLS", 13, "#6CCCF0")
    text(p, 574, 48, "PROPOSTA A", 13, "#8BD5F5", QFont.Weight.DemiBold)
    brand(p, 566, 82, "video-flow")
    text(p, 44, 225, "Mesma linguagem visual dos seus outros aplicativos", 22, "#F5F7FA", QFont.Weight.DemiBold)
    samples = [("DownloadRush", r"C:\Users\HeliumMarcos\AppData\Local\Programs\DownloadRush-Native\app\extensao-chrome\icons\128.png"), ("YTMusicRush", r"C:\Users\HeliumMarcos\AppData\Local\Programs\YTMusicRush\ChromeExtension\icon.png")]
    for i, (label, path) in enumerate(samples):
        p.drawImage(QRectF(48 + i*220, 256, 80, 80), QImage(path))
        text(p, 44 + i*220, 368, label, 13, "#C2CDD5")
    p.drawImage(QRectF(496, 256, 80, 80), render_icon("video-flow", 128))
    text(p, 484, 368, "Helium Vídeo Tools", 13, "#8BD5F5")
    p.end(); before.save(str(OUT / "antes-depois.png"))
    print(OUT / "comparacao-propostas.png")
    print(OUT / "antes-depois.png")


if __name__ == "__main__":
    main()
