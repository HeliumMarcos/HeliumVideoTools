#define MyAppName "Helium Vídeo Tools"
#define MyAppVersion "2.6.0"
#define MyAppPublisher "Helium"
#define MyAppExeName "HeliumVideoTools.exe"

[Setup]
AppId={{A838695D-F4C0-4745-B9DC-42DC95346E48}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\HeliumVideoTools
DefaultGroupName=Helium Vídeo Tools
OutputDir=output
OutputBaseFilename=HeliumVideoTools-Setup-2.6.0
SetupIconFile=..\assets\logo.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\dist\HeliumVideoTools.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Helium Vídeo Tools"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Helium Vídeo Tools"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos adicionais:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Helium Vídeo Tools"; Flags: nowait postinstall skipifsilent
