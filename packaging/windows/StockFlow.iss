#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
#ifndef ClientDir
  #error ClientDir must point to the self-contained client publish folder.
#endif
#ifndef OutputDir
  #error OutputDir must point to the release output folder.
#endif

[Setup]
AppId={{CB89A35B-DDA1-4D19-95EE-84612E379F6C}
AppName=StockFlow
AppVersion={#AppVersion}
AppVerName=StockFlow {#AppVersion}
AppPublisher=TikTak-creator
AppPublisherURL=https://github.com/TikTak-creator/InventorySystem
AppSupportURL=https://github.com/TikTak-creator/InventorySystem/issues
AppUpdatesURL=https://github.com/TikTak-creator/InventorySystem/releases
DefaultDirName={localappdata}\Programs\StockFlow
DefaultGroupName=StockFlow
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
WizardStyle=modern
DisableProgramGroupPage=yes
DisableWelcomePage=no
OutputDir={#OutputDir}
OutputBaseFilename=StockFlow-{#AppVersion}-Setup-win-x64
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\StockFlow.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}
VersionInfoDescription=StockFlow Windows client installer

[Languages]
Name: english; MessagesFile: compiler:Default.isl
Name: russian; MessagesFile: compiler:Languages\Russian.isl

[CustomMessages]
english.StartGuide=Read the getting started guide
russian.StartGuide=Открыть краткое руководство
english.ClientWelcome=This installs the StockFlow Windows client with its .NET runtime.%n%nYou will need a working StockFlow server address and an account from your administrator. MySQL and the API server are distributed separately.
russian.ClientWelcome=Будет установлен Windows-клиент StockFlow со встроенной средой .NET.%n%nДля входа нужны адрес работающего сервера StockFlow и учётная запись от администратора. MySQL и сервер API поставляются отдельно.

[Messages]
WelcomeLabel2={cm:ClientWelcome}

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#ClientDir}\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ClientDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\StockFlow"; Filename: "{app}\StockFlow.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:StartGuide}"; Filename: "{app}\START-HERE.txt"
Name: "{autodesktop}\StockFlow"; Filename: "{app}\StockFlow.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\StockFlow.exe"; Description: "{cm:LaunchProgram,StockFlow}"; Flags: nowait postinstall skipifsilent
