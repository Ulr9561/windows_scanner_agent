#define AppName "Local Scan Agent"

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\installer\app"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer\output"
#endif

[Setup]
AppId={{8E15DDF1-7D60-4B8C-A806-8A8A6DD74670}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=LocalScanAgent
DefaultDirName={localappdata}\Programs\LocalScanAgent
DefaultGroupName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=LocalScanAgentSetup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\LocalScanAgent.Tray.exe

[Tasks]
Name: "startup"; Description: "Lancer l'agent au demarrage de la session"; GroupDescription: "Options"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Local Scan Agent"; Filename: "{app}\LocalScanAgent.Tray.exe"; WorkingDir: "{app}"
Name: "{userstartup}\Local Scan Agent"; Filename: "{app}\LocalScanAgent.Tray.exe"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\LocalScanAgent.Tray.exe"; Description: "Lancer Local Scan Agent"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM ""LocalScanAgent.Tray.exe"" /F >nul 2>nul"; Flags: runhidden
Filename: "{cmd}"; Parameters: "/C taskkill /IM ""LocalScanAgent.Host.exe"" /F >nul 2>nul"; Flags: runhidden
