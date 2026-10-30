Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
Name "Helium Vídeo Tools"
OutFile "${PROJECT_DIR}\artifacts\release\HeliumVideoTools-${APP_VERSION}-win-x64-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\HeliumVideoTools"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1046 "ProductName" "Helium Vídeo Tools"
VIAddVersionKey /LANG=1046 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1046 "FileDescription" "Instalador por usuário — Helium Vídeo Tools"
!define MUI_ICON "${PROJECT_DIR}\assets\logo.ico"
!define MUI_UNICON "${PROJECT_DIR}\assets\logo.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\app\HeliumVideoTools.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir Helium Vídeo Tools"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"
Var InstallerMutex
Var RestartApp

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Este instalador requer Windows de 64 bits."
    Abort
  ${EndIf}
  SetShellVarContext current
  ; Pasta única para este usuário; não usar /D nem pasta por versão no upgrade.
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\HeliumVideoTools"
  System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Local\Helium.HeliumVideoTools.Installer") p.s'
  Pop $InstallerMutex
  System::Call 'kernel32::GetLastError() i.r0'
  StrCmp $0 183 0 +3
  MessageBox MB_ICONSTOP "Outro instalador já está aberto." /SD IDOK
  Abort
  StrCpy $RestartApp 0
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/RESTART" $1
  IfErrors +2 0
  StrCpy $RestartApp 1
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "${PROJECT_DIR}\artifacts\update-host\HeliumVideoTools.UpdateHost.exe"
  ExecWait '"$PLUGINSDIR\HeliumVideoTools.UpdateHost.exe" --close "$INSTDIR\app\HeliumVideoTools.exe"' $0
  StrCmp $0 0 close_ok
  MessageBox MB_ICONSTOP "Não foi possível encerrar esta instalação. Feche o aplicativo e tente novamente." /SD IDOK
  Abort
  close_ok:
FunctionEnd

Section "Helium Vídeo Tools"
  SectionIn RO
  ; Todos os alvos abaixo ficam na pasta fixa de instalação, nunca no perfil de dados.
  RMDir /r "$INSTDIR\app.new"
  SetOutPath "$INSTDIR\app.new"
  ClearErrors
  File /r "${PROJECT_DIR}\artifacts\publish\*.*"
  IfErrors install_failed
  ; O diretório corrente do instalador não pode ser a pasta que será renomeada.
  SetOutPath "$INSTDIR"
  IfFileExists "$INSTDIR\app\HeliumVideoTools.exe" 0 no_previous
  RMDir /r "$INSTDIR\app.previous"
  Rename "$INSTDIR\app" "$INSTDIR\app.previous"
  IfErrors install_failed
  no_previous:
  ClearErrors
  Rename "$INSTDIR\app.new" "$INSTDIR\app"
  IfErrors restore_previous
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools" "DisplayName" "Helium Vídeo Tools"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools" "Publisher" "Helium"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools" "DisplayIcon" "$INSTDIR\app\HeliumVideoTools.exe"
  CreateDirectory "$SMPROGRAMS\Helium Vídeo Tools"
  ; Não substituir os .lnk existentes: o Windows pode associar os pins a esses arquivos.
  IfFileExists "$SMPROGRAMS\Helium Vídeo Tools\Helium Vídeo Tools.lnk" start_exists
  CreateShortCut "$SMPROGRAMS\Helium Vídeo Tools\Helium Vídeo Tools.lnk" "$INSTDIR\app\HeliumVideoTools.exe" "" "$INSTDIR\app\HeliumVideoTools.exe"
  ExecWait '"$PLUGINSDIR\HeliumVideoTools.UpdateHost.exe" --stamp-shortcut "$SMPROGRAMS\Helium Vídeo Tools\Helium Vídeo Tools.lnk"' $0
  start_exists:
  IfFileExists "$DESKTOP\Helium Vídeo Tools.lnk" desktop_exists
  CreateShortCut "$DESKTOP\Helium Vídeo Tools.lnk" "$INSTDIR\app\HeliumVideoTools.exe" "" "$INSTDIR\app\HeliumVideoTools.exe"
  ExecWait '"$PLUGINSDIR\HeliumVideoTools.UpdateHost.exe" --stamp-shortcut "$DESKTOP\Helium Vídeo Tools.lnk"' $0
  desktop_exists:
  Goto done
  restore_previous:
  Rename "$INSTDIR\app.previous" "$INSTDIR\app"
  install_failed:
  MessageBox MB_ICONSTOP "Falha ao instalar. A instalação anterior foi mantida quando disponível. Seus vídeos e preferências foram preservados." /SD IDOK
  SetErrorLevel 2
  Abort
  done:
SectionEnd

Function .onInstSuccess
  ; Libera a trava antes de reabrir o app.
  System::Call 'kernel32::CloseHandle(p $InstallerMutex)'
  StrCmp $RestartApp 1 0 done_restart
  Exec '"$INSTDIR\app\HeliumVideoTools.exe"'
  done_restart:
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  StrCmp $INSTDIR "$LOCALAPPDATA\Programs\HeliumVideoTools" safe_uninstall
  MessageBox MB_ICONSTOP "Pasta inesperada. Desinstalação interrompida."
  Abort
  safe_uninstall:
  ; Arquivos do usuário e conjuntos atualizados em LOCALAPPDATA\HeliumVideoTools permanecem.
  Delete "$DESKTOP\Helium Vídeo Tools.lnk"
  Delete "$SMPROGRAMS\Helium Vídeo Tools\Helium Vídeo Tools.lnk"
  RMDir "$SMPROGRAMS\Helium Vídeo Tools"
  RMDir /r "$INSTDIR\app"
  RMDir /r "$INSTDIR\app.previous"
  RMDir /r "$INSTDIR\app.new"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools"
SectionEnd
