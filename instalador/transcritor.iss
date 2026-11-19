; Instalador do Transcritor (Inno Setup 6.7+)
; Compile com:  ISCC.exe transcritor.iss   (o empacotar.ps1 faz isso)

#define AppName "Transcritor"
#define AppVersion "1.1.0"
#define AppPublisher "Felipe Kevin"
#define AppURL "https://github.com/felipekevinns/transcritor"
#ifndef SourceDir
  #define SourceDir "..\dist\Transcritor"
#endif

[Setup]
AppId={{7C6B2E4A-5E1F-4C8B-9B3E-2A6D8F1C4E71}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=Instalador do {#AppName}

; Instala na pasta do usuario por padrao (sem pedir administrador);
; o usuario pode escolher qualquer outra pasta.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableReadyPage=no
DisableDirPage=no
UsePreviousAppDir=yes
ExtraDiskSpaceRequired=15000000000

; Aparencia
WizardStyle=modern dynamic
WizardSizePercent=110
WizardBackColor=#F3F1EC
WizardBackColorDynamicDark=#161514
WizardImageFile=imagens\lateral-100.bmp,imagens\lateral-125.bmp,imagens\lateral-150.bmp,imagens\lateral-175.bmp,imagens\lateral-200.bmp,imagens\lateral-250.bmp
WizardSmallImageFile=imagens\pequena-100.bmp,imagens\pequena-125.bmp,imagens\pequena-150.bmp,imagens\pequena-175.bmp,imagens\pequena-200.bmp,imagens\pequena-250.bmp
SetupIconFile=..\app\transcritor.ico
UninstallDisplayIcon={app}\Transcritor.exe
UninstallDisplayName={#AppName}

; Saida
OutputDir=..\dist
OutputBaseFilename=Transcritor-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.WelcomeLabel1=Instalar o [name]
ptbr.WelcomeLabel2=Transcrição de vídeos e áudios longos, com identificação de falantes, feita inteiramente neste computador.%n%nO assistente vai instalar o [name/ver] e baixar tudo de que ele precisa: Python, ffmpeg, PyTorch, WhisperX e, se você quiser, os modelos de transcrição.%n%nTenha uma conexão com a internet e cerca de 16 GB livres.
ptbr.SelectDirDesc=Onde os arquivos do [name] devem ficar?
ptbr.SelectDirLabel3=Os componentes e os modelos também ficam nesta pasta. Prefira um caminho curto, fora do OneDrive.
ptbr.SelectDirBrowseLabel=Para continuar, clique em Avançar. Para escolher outra pasta, clique em Procurar.
ptbr.ClickNext=Clique em Avançar para continuar ou em Cancelar para sair.
ptbr.WizardReady=Pronto para instalar
ptbr.ReadyLabel1=Tudo pronto para instalar o [name] neste computador.
ptbr.ReadyLabel2a=Clique em Instalar para começar ou em Voltar para revisar as escolhas.
ptbr.ReadyLabel2b=Clique em Instalar para começar.
ptbr.WizardPreparing=Preparando a instalação
ptbr.PreparingDesc=O instalador está se preparando para instalar o [name].
ptbr.ClickFinish=Clique em Concluir para sair.
ptbr.ExitSetupMessage=A instalação não terminou. Se você sair agora, o programa não será instalado.%n%nVocê pode executar o instalador de novo depois.%n%nSair do instalador?
ptbr.SetupAppRunningError=O [name] está aberto.%n%nFeche-o e clique em OK para continuar, ou em Cancelar para sair.
ptbr.UninstallAppRunningError=O %1 está aberto.%n%nFeche-o e clique em OK para continuar, ou em Cancelar para sair.
ptbr.FinishedRestartLabel=Para concluir a instalação do [name], é preciso reiniciar o computador. Deseja reiniciar agora?
ptbr.FinishedRestartMessage=Para concluir a instalação do [name], é preciso reiniciar o computador.%n%nDeseja reiniciar agora?
ptbr.UninstalledAndNeedsRestart=Para concluir a remoção do %1, é preciso reiniciar o computador.%n%nDeseja reiniciar agora?
ptbr.WizardSelectTasks=Opções
ptbr.SelectTasksDesc=Escolha como o [name] deve ser instalado.
ptbr.SelectTasksLabel2=Marque as opções desejadas e clique em Avançar.
ptbr.FinishedHeadingLabel=O [name] está pronto
ptbr.FinishedLabel=A instalação foi concluída. Abra o [name] pelo atalho na Área de Trabalho ou no menu Iniciar.
ptbr.FinishedLabelNoIcons=A instalação foi concluída.

[CustomMessages]
ptbr.TaskDesktop=Criar atalho na Área de Trabalho
ptbr.TaskModels=Baixar os modelos de transcrição agora (cerca de 7 GB), para o primeiro uso funcionar sem internet
ptbr.GroupOptions=Opções:
ptbr.StatusPrepare=Baixando e configurando os componentes. Acompanhe na janela do Transcritor...
ptbr.LaunchApp=Abrir o Transcritor agora
ptbr.PathTooLong=Escolha um caminho mais curto (até 80 caracteres). Algumas bibliotecas têm nomes de arquivo longos e podem falhar em pastas muito profundas.
ptbr.PrepareFailed=Os componentes não foram totalmente preparados.%n%nTudo bem: ao abrir o Transcritor, a preparação continua de onde parou.

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; GroupDescription: "{cm:GroupOptions}"
Name: "modelos"; Description: "{cm:TaskModels}"; GroupDescription: "{cm:GroupOptions}"; Flags: unchecked

[Dirs]
Name: "{app}"; Permissions: users-modify

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\Transcritor.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Transcritor.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Transcritor.exe"; Description: "{cm:LaunchApp}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\runtime"
Type: filesandordirs; Name: "{app}\models"
Type: filesandordirs; Name: "{app}\app"
Type: files; Name: "{app}\hf_token.txt"
Type: files; Name: "{app}\vocabulario.txt"
Type: dirifempty; Name: "{app}"

[Code]
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and (Length(WizardDirValue) > 80) then
  begin
    MsgBox(CustomMessage('PathTooLong'), mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Params: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    Params := '--preparar --automatico';
    if WizardIsTaskSelected('modelos') then
      Params := Params + ' --modelos';
    WizardForm.StatusLabel.Caption := CustomMessage('StatusPrepare');
    if not Exec(ExpandConstant('{app}\Transcritor.exe'), Params, ExpandConstant('{app}'),
                SW_SHOW, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    begin
      if not WizardSilent then
        MsgBox(CustomMessage('PrepareFailed'), mbInformation, MB_OK);
    end;
  end;
end;
