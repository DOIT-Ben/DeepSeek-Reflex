Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!ifndef APP_VERSION
  !error "APP_VERSION is required"
!endif
!ifndef PAYLOAD_DIR
  !error "PAYLOAD_DIR is required"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE is required"
!endif

Name "DeepSeek-Reflex ${APP_VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\DeepSeekFloat"
RequestExecutionLevel user
SetCompressor /SOLID zlib
CRCCheck on
Icon "${PAYLOAD_DIR}\icon.ico"
UninstallIcon "${PAYLOAD_DIR}\icon.ico"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "DeepSeek-Reflex"
VIAddVersionKey "FileDescription" "DeepSeek-Reflex installer"
VIAddVersionKey "FileVersion" "${APP_VERSION}.0"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 DOIT-Ben and contributors"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${PAYLOAD_DIR}\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\DeepSeekFloat.exe"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetShellVarContext current
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "Requires Windows x64 / 需要 64 位 Windows。" /SD IDOK
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "Requires Windows 10 or later / 需要 Windows 10 或更新版本。" /SD IDOK
    Abort
  ${EndIf}
  SetRegView 32
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    MessageBox MB_OK|MB_ICONSTOP "Install .NET Framework 4.8 first / 请先安装 .NET Framework 4.8。" /SD IDOK
    Abort
  ${EndIf}
  ReadRegStr $0 HKLM "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ${If} $0 == ""
  ${OrIf} $0 == "0.0.0.0"
    ReadRegStr $0 HKCU "Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ${EndIf}
  ${If} $0 == ""
  ${OrIf} $0 == "0.0.0.0"
    MessageBox MB_OK|MB_ICONSTOP "Install WebView2 Runtime first / 请先安装 WebView2 Runtime： https://developer.microsoft.com/microsoft-edge/webview2/" /SD IDOK
    Abort
  ${EndIf}
  SetRegView 64
FunctionEnd

Section "DeepSeek-Reflex (必需 / required)"
  SectionIn RO
  IfFileExists "$INSTDIR\DeepSeekFloat.exe" 0 install_files
  ClearErrors
  FileOpen $0 "$INSTDIR\DeepSeekFloat.exe" a
  IfErrors install_busy
  FileClose $0
  Goto install_files
install_busy:
  MessageBox MB_OK|MB_ICONSTOP "Please exit from the tray menu before upgrading / 请在托盘菜单中退出后重试。" /SD IDOK
  SetErrorLevel 1
  Abort
install_files:
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File "${PAYLOAD_DIR}\*"
  WriteUninstaller "$INSTDIR\uninstall.exe"
  CreateDirectory "$SMPROGRAMS\DeepSeek-Reflex"
  CreateShortcut "$SMPROGRAMS\DeepSeek-Reflex\DeepSeek-Reflex.lnk" "$INSTDIR\DeepSeekFloat.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "DisplayName" "DeepSeek-Reflex"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "Publisher" "DOIT-Ben and contributors"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "UninstallString" '$\"$INSTDIR\uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "QuietUninstallString" '$\"$INSTDIR\uninstall.exe$\" /S'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "DisplayIcon" "$INSTDIR\DeepSeekFloat.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "URLInfoAbout" "https://github.com/DOIT-Ben/DeepSeek-Reflex"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex" "NoRepair" 1
SectionEnd

Section /o "桌面快捷方式 / Desktop shortcut"
  CreateShortcut "$DESKTOP\DeepSeek-Reflex.lnk" "$INSTDIR\DeepSeekFloat.exe"
SectionEnd

Section /o "登录后驻留托盘 / Start on sign-in"
  CreateShortcut "$SMSTARTUP\DeepSeek-Reflex.lnk" "$INSTDIR\DeepSeekFloat.exe" "--background"
SectionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  IfFileExists "$INSTDIR\DeepSeekFloat.exe" 0 uninstall_ready
  ClearErrors
  FileOpen $0 "$INSTDIR\DeepSeekFloat.exe" a
  IfErrors uninstall_busy
  FileClose $0
  Return
uninstall_busy:
  MessageBox MB_OK|MB_ICONSTOP "Please exit from the tray menu before uninstalling / 请在托盘菜单中退出后再卸载。" /SD IDOK
  SetErrorLevel 1
  Abort
uninstall_ready:
FunctionEnd

Section "Uninstall"
  !include "${PAYLOAD_DIR}\..\uninstall-files.nsh"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\DeepSeek-Reflex\DeepSeek-Reflex.lnk"
  RMDir "$SMPROGRAMS\DeepSeek-Reflex"
  Delete "$DESKTOP\DeepSeek-Reflex.lnk"
  Delete "$SMSTARTUP\DeepSeek-Reflex.lnk"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DeepSeek-Reflex"
  ; Never delete the APPDATA preferences / WebView2 profile, or recurse through user files.
SectionEnd
