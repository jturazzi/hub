; ============================================================
;  Hub — Script d'installation Inno Setup
;  Génère : installer\Output\Hub-Setup.exe
; ============================================================

#define AppName      "Hub"
#define AppVersion   "1.0"
#define AppPublisher "Jérémy TURAZZI"
#define AppExeName   "Hub.exe"
#define PublishDir   "..\bin\Release\net8.0-windows\win-x64"

[Setup]
AppId                    = {{6748CD16-CF12-4091-900A-6A8EA7A23219}
AppName                  = {#AppName}
AppVersion               = {#AppVersion}
AppPublisher             = {#AppPublisher}
AppPublisherURL          = https://github.com/jturazzi/hub
DefaultDirName           = {autopf}\{#AppName}
DefaultGroupName         = {#AppName}
UninstallDisplayName     = {#AppName}
UninstallDisplayIcon     = {app}\{#AppExeName}
OutputDir                = Output
OutputBaseFilename       = Hub-Setup
SetupIconFile            = ..\hub.ico
Compression              = lzma2/ultra64
SolidCompression         = yes
WizardStyle              = modern
PrivilegesRequired       = admin
ArchitecturesInstallIn64BitMode = x64compatible
MinVersion               = 10.0
DisableProgramGroupPage  = yes
; Affiche la version dans Ajout/Suppression de programmes
VersionInfoVersion       = 1.0
VersionInfoCompany       = {#AppPublisher}
VersionInfoDescription   = {#AppName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

; ── Raccourcis ───────────────────────────────────────────────
[Tasks]
Name: "desktopicon"; \
  Description: "Créer un raccourci sur le bureau (tous les utilisateurs)"; \
  GroupDescription: "Raccourcis supplémentaires :"; \
  Flags: checkedonce

; ── Fichiers à installer ─────────────────────────────────────
[Files]
; Application publiée (tous les fichiers de win-x64, sauf le sous-dossier publish et les symboles)
Source: "{#PublishDir}\*"; \
  DestDir: "{app}"; \
  Excludes: "publish,*.pdb,*.xml"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; ── Raccourcis créés ─────────────────────────────────────────
[Icons]
; Menu Démarrer
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}"

; Bureau public (tous les utilisateurs)
Name: "{commondesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  IconFilename: "{app}\{#AppExeName}"; \
  Tasks: desktopicon

; ── Lancement post-install ───────────────────────────────────
[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "Lancer {#AppName} maintenant"; \
  Flags: nowait postinstall skipifsilent

; ── Code Pascal : vérification et installation de .NET 8 ─────
[Code]

// Vérifie si .NET 8 Windows Desktop Runtime est installé
function IsDotNet8Installed: Boolean;
var
  KeyPath : String;
  Names   : TArrayOfString;
  i       : Integer;
begin
  Result  := False;
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';

  if RegGetValueNames(HKLM64, KeyPath, Names) then
  begin
    for i := 0 to GetArrayLength(Names) - 1 do
    begin
      if Copy(Names[i], 1, 2) = '8.' then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

// Avertit l'utilisateur si .NET 8 est absent, sans bloquer l'installation
procedure NotifyDotNet8Missing;
var
  ResultCode : Integer;
begin
  if MsgBox(
    'Hub nécessite .NET 8 Desktop Runtime pour fonctionner.' + #13#10 +
    'Il ne semble pas être installé sur ce poste.' + #13#10#13#10 +
    'Vous pouvez continuer l''installation et installer .NET 8 ensuite.' + #13#10 +
    'Voulez-vous ouvrir la page de téléchargement maintenant ?',
    mbInformation, MB_YESNO) = IDYES then
  begin
    ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOW, ewNoWait, ResultCode);
  end;
end;

// Point d'entrée : informe l'utilisateur si .NET 8 est absent
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not IsDotNet8Installed then
    NotifyDotNet8Missing;
end;
