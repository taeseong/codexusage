; CodexUsage per-user Windows installer.
; Pass PublishDir, OutputDir, and AppVersion from scripts/package-windows.ps1.

#ifndef PublishDir
  #error PublishDir must point to the self-contained publish directory.
#endif

#ifndef OutputDir
  #error OutputDir must point to the installer output directory.
#endif

#ifndef AppVersion
  #define AppVersion "0.1.3"
#endif

[Setup]
AppId={{C12312E9-92C6-4C1E-A337-54134A9FBA72}
AppName=CodexUsage
AppVersion={#AppVersion}
AppPublisher=CodexUsage
DefaultDirName={localappdata}\Programs\CodexUsage
DefaultGroupName=CodexUsage
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=CodexUsage-Setup-{#AppVersion}-win-x64
SetupIconFile=..\src\CodexUsage.Windows\Assets\codex-usage.ico
UninstallDisplayIcon={app}\CodexUsage.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\CodexUsage"; Filename: "{app}\CodexUsage.exe"
Name: "{autodesktop}\CodexUsage"; Filename: "{app}\CodexUsage.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CodexUsage.exe"; Description: "Launch CodexUsage"; Flags: nowait postinstall skipifsilent
