#ifndef MyAppVersion
#define MyAppVersion "1.1.0"
#endif

#define MyAppName "Theme Toggle"
#define MyAppPublisher "awabkb"
#define MyAppURL "https://github.com/awabkb/theme-toggle"
#define MyAppExeName "ThemeToggle.exe"

[Setup]
AppId={{E8B7C4A1-9F2D-4A6E-B3C1-7D5E2A9F8C01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\ThemeToggle
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=ThemeToggle-Setup
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\src\ThemeToggle.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autostartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Theme Toggle now"; Flags: nowait postinstall skipifsilent unchecked

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM ThemeToggle.exe /F"; Flags: runhidden; RunOnceId: "StopThemeToggle"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
