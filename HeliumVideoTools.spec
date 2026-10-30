# -*- mode: python ; coding: utf-8 -*-

a = Analysis(
    ["main.py"],
    pathex=["."],
    binaries=[("ffmpeg.exe", ".")],
    datas=[
        ("assets/icons", "assets/icons"),
        ("assets/logo-512.png", "assets"),
        ("assets/logo.ico", "assets"),
    ],
    hiddenimports=["PySide6.QtSvg"],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=["tkinter", "customtkinter"],
    noarchive=False,
    optimize=1,
)

# O plugin de teclado virtual e o leitor de PDF puxam todo o runtime QML/Quick,
# embora esta aplicação use apenas widgets. Removê-los reduz extração e startup.
unused_qt_parts = (
    "qtvirtualkeyboardplugin.dll",
    "qpdf.dll",
    "qt6virtualkeyboard.dll",
    "qt6qml.dll",
    "qt6qmlmeta.dll",
    "qt6qmlmodels.dll",
    "qt6qmlworkerscript.dll",
    "qt6quick.dll",
    "qt6pdf.dll",
)
a.binaries = [entry for entry in a.binaries if not any(part in entry[0].lower() for part in unused_qt_parts)]
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name="HeliumVideoTools",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=False,
    disable_windowed_traceback=True,
    icon="assets/logo.ico",
    version="version_info.txt",
)
