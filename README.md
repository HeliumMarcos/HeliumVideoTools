# Helium Vídeo Tools 2.6

Aplicativo desktop PySide6 para dividir, compactar e converter vídeos para WebM. Inclui FFmpeg, processamento direto de um ou vários arquivos, progresso geral com ETA, cancelamento e abertura da pasta do resultado, tema Windows 11 claro/escuro e identidade Helium própria.

## Executar em desenvolvimento

```powershell
python -m pip install -r requirements.txt
python main.py
```

Os logs JSON rotativos ficam em `%APPDATA%\HeliumVideoTools\helium_video_tools.log`.

## Testar e empacotar

```powershell
python -m unittest discover -s tests -v
.\build.ps1
```

O script gera duas distribuições:

- `C:\Users\HeliumMarcos\Downloads\Music\HeliumVideoTools.exe`: versão portátil em um único arquivo.
- `C:\Users\HeliumMarcos\Downloads\Music\HeliumVideoTools-Rapido\HeliumVideoTools.exe`: versão `onedir`, que abre mais rápido porque não precisa extrair o runtime Qt a cada execução.

O build usa Python 3.12 para estabilidade do runtime Qt. O FFprobe separado foi eliminado; a análise usa o próprio FFmpeg.

## Assinatura digital opcional

Com um certificado de code signing instalado, assine antes de distribuir:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a dist\HeliumVideoTools.exe
signtool verify /pa dist\HeliumVideoTools.exe
```

Um certificado EV reduz avisos do SmartScreen mais rapidamente; certificados OV também são válidos, mas constroem reputação ao longo do tempo.
