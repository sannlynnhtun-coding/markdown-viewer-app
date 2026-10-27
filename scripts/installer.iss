#ifndef PublishDir
  #define PublishDir "..\artifacts\installer\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#define AppVersion GetFileVersion(PublishDir + "\MarkdownViewerApp.exe")

[Setup]
AppId={{A2E866D1-D3E8-49A4-9471-4BDBB299BFB1}
AppName=Markdown Viewer
AppVersion={#AppVersion}
AppPublisher=Sann Lynn Htun
AppPublisherURL=https://github.com/sannlynnhtun-coding/markdown-viewer-app
DefaultDirName={localappdata}\Programs\Markdown Viewer
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=MarkdownViewer.Setup
SetupIconFile=..\MarkdownViewerApp\Assets\AppIcon.ico
UninstallDisplayIcon={app}\MarkdownViewerApp.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
ChangesAssociations=yes
CloseApplications=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Markdown Viewer"; Filename: "{app}\MarkdownViewerApp.exe"

[Registry]
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "MarkdownViewer.Document"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: string; ValueName: "MarkdownViewer.Document"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\MarkdownViewer.Document"; ValueType: string; ValueData: "Markdown document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MarkdownViewer.Document\DefaultIcon"; ValueType: string; ValueData: """{app}\MarkdownViewerApp.exe"",0"
Root: HKCU; Subkey: "Software\Classes\MarkdownViewer.Document\shell\open\command"; ValueType: string; ValueData: """{app}\MarkdownViewerApp.exe"" ""%1"""

[Run]
Filename: "{app}\MarkdownViewerApp.exe"; Description: "Launch Markdown Viewer"; Flags: nowait postinstall skipifsilent
