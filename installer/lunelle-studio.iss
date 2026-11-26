; Lunelle Studio's Windows installer (Inno Setup 6). build/publish.ps1 compiles it after publishing the studio:
;   iscc /DAppVersion=<version> /DSource=<published folder> /DArch=<x64|arm64> /O<output folder> installer\lunelle-studio.iss
; It installs for the user alone by default (no elevation), or for every user when asked, and opens .lunelle files.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Source
  #define Source "..\artifacts\publish\win-x64"
#endif
#ifndef Arch
  #define Arch "x64"
#endif

[Setup]
AppId={{6B3C2F0E-8F7A-4C55-9D3E-1A2B4C6D8E10}
AppName=Lunelle Studio
AppVersion={#AppVersion}
AppPublisher=BinaryVibrance
AppPublisherURL=https://github.com/AtomicBlom/LunelleStudio
DefaultDirName={autopf}\Lunelle Studio
DefaultGroupName=Lunelle Studio
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed={#Arch}compatible
ArchitecturesInstallIn64BitMode={#Arch}compatible
ChangesAssociations=yes
OutputBaseFilename=LunelleStudio-{#AppVersion}-win-{#Arch}
SetupIconFile=..\BinaryVibrance.LunelleStudio.App\Assets\lunelle-studio.ico
UninstallDisplayIcon={app}\lunelle-studio.exe
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#Source}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Lunelle Studio"; Filename: "{app}\lunelle-studio.exe"
Name: "{autodesktop}\Lunelle Studio"; Filename: "{app}\lunelle-studio.exe"; Tasks: desktopicon

[Registry]
; .lunelle files open in the studio, which unpacks each into Documents\Lunelle Studio to edit
Root: HKA; Subkey: "Software\Classes\.lunelle"; ValueType: string; ValueName: ""; ValueData: "LunelleStudio.Music"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.lunelle\OpenWithProgids"; ValueType: string; ValueName: "LunelleStudio.Music"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\LunelleStudio.Music"; ValueType: string; ValueName: ""; ValueData: "Lunelle music"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\LunelleStudio.Music\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\lunelle-studio.exe,0"
Root: HKA; Subkey: "Software\Classes\LunelleStudio.Music\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\lunelle-studio.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Applications\lunelle-studio.exe\SupportedTypes"; ValueType: string; ValueName: ".lunelle"; ValueData: ""; Flags: uninsdeletekey

[Run]
Filename: "{app}\lunelle-studio.exe"; Description: "{cm:LaunchProgram,Lunelle Studio}"; Flags: nowait postinstall skipifsilent
