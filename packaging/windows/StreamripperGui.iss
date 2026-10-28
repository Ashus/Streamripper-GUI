; Compile through package-windows.cmd with Inno Setup 6.3 or newer (including 7).
#ifndef PayloadDir
  #error PayloadDir must point to the verified installer payload
#endif
#ifndef OutputDir
  #error OutputDir must point to the package output directory
#endif
#define AppVersion Trim(FileRead(FileOpen(SourcePath + "\..\..\VERSION")))
; Refresh this URL and checksum together from Microsoft's .NET 10 release metadata.
; The runtime is downloaded only when selected in interactive Setup, never bundled.
#define RuntimeUrl "https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-win-x64.exe"
#define RuntimeSHA256 "53a67d2d68a74a892010191d70080a5b359f41ed7d0bb2f92f120c9f0e4e199d"

[Setup]
AppId={{DDF0F637-697A-4CB8-BB53-6589698B99A2}
AppName=Streamripper GUI
UninstallDisplayName=Streamripper GUI
AppVersion={#AppVersion}
AppPublisher=Ashus
AppPublisherURL=https://github.com/Ashus/Streamripper-GUI
DefaultDirName={localappdata}\Programs\Streamripper GUI
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=streamripper-gui-{#AppVersion}-win64-setup
SetupIconFile=..\..\assets\radio.ico
UninstallDisplayIcon={app}\StreamripperGui.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableStartupPrompt=yes
CloseApplications=yes
RestartApplications=no
UsePreviousTasks=no

[Tasks]
Name: "dotnetruntime"; Description: "Download and install .NET Runtime 10 x64 (requires administrator approval)"; Check: NeedsDotNet

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Streamripper GUI"; Filename: "{app}\StreamripperGui.exe"

[Run]
Filename: "{app}\StreamripperGui.exe"; Description: "Launch Streamripper GUI"; Flags: nowait postinstall skipifsilent; Check: CanLaunch

[Code]
var
  DownloadPage: TDownloadWizardPage;

function HasRuntimeInRegistry(RootKey: Integer): Boolean;
var
  Versions: TArrayOfString;
  InstallLocation: String;
  I: Integer;
begin
  Result := False;
  if not RegQueryStringValue(RootKey, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64',
    'InstallLocation', InstallLocation) then Exit;
  if RegGetValueNames(RootKey,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App', Versions) then
    for I := 0 to GetArrayLength(Versions) - 1 do
      if (Copy(Versions[I], 1, 5) = '10.0.') and (Pos('-', Versions[I]) = 0) and
        FileExists(AddBackslash(InstallLocation) + 'shared\Microsoft.NETCore.App\' +
          Versions[I] + '\coreclr.dll') then
      begin
        Result := True;
        Exit;
      end;
end;

function NeedsDotNet: Boolean;
begin
  Result := not (HasRuntimeInRegistry(HKLM32) or HasRuntimeInRegistry(HKLM64));
end;

function CanLaunch: Boolean;
begin
  Result := not NeedsDotNet;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('Downloading .NET Runtime',
    'Downloading the optional runtime from Microsoft.', nil);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // Silent installs remain offline and never trigger a dependency UAC prompt.
  if WizardSilent or not WizardIsTaskSelected('dotnetruntime') or not NeedsDotNet then Exit;
  DownloadPage.Clear;
  DownloadPage.Add('{#RuntimeUrl}', 'dotnet-runtime-x64.exe', '{#RuntimeSHA256}');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := 'Could not download .NET Runtime: ' + GetExceptionMessage;
    end;
  finally
    DownloadPage.Hide;
  end;
  if Result <> '' then Exit;
  if not ShellExec('runas', ExpandConstant('{tmp}\dotnet-runtime-x64.exe'),
    '/install /passive /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then
    Result := 'Could not start .NET Runtime installation: ' + SysErrorMessage(ResultCode)
  else if (ResultCode <> 0) and (ResultCode <> 3010) then
    Result := Format('.NET Runtime installation failed (exit code %d).', [ResultCode])
  else
  begin
    NeedsRestart := ResultCode = 3010;
    if NeedsDotNet then Result := '.NET Runtime 10 x64 was not detected after installation.';
  end;
end;
