# =============================================================================
# Deploy Klyon Hub Platform - Blue-Green (zero downtime)
# Uso: powershell -ExecutionPolicy Bypass -File deploy.ps1
#
# O script faz TUDO via SSH: na primeira execucao configura pastas, systemd e
# Nginx; em toda execucao faz deploy na instancia inativa e troca o trafego.
# Requer: chave SSH e (na VM) usuario ubuntu com sudo NOPASSWD para systemctl, tee, sed, nginx:
# echo "ubuntu ALL=(ALL) NOPASSWD: /usr/bin/systemctl, /usr/bin/tee, /usr/bin/sed, /usr/sbin/nginx" | sudo tee /etc/sudoers.d/klyon-deploy
# =============================================================================

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# --- Configuracao ---
$VmIp        = "168.75.93.94"
$VmUser      = "ubuntu"
$SshKey      = Join-Path $PSScriptRoot "ssh\ssh-key-2026-02-04.key"
$AppBlue     = "/home/ubuntu/app_blue"
$AppGreen    = "/home/ubuntu/app_green"
$AppNew      = "/home/ubuntu/app_new"
$NginxConfig = "/etc/nginx/sites-available/klyon-app"
$PortBlue    = 5000
$PortGreen   = 5001
# Opcional: connection string para o setup. Se vazio, o script usa KlyonHubPlatform.Api\appsettings.Production.json local.
$DeployConnectionString = ""

function Invoke-Ssh {
    param([string]$Command)
    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    try {
        $result = ssh -i $SshKey -o StrictHostKeyChecking=no -o ConnectTimeout=10 "${VmUser}@${VmIp}" $Command 2>&1
        return $result
    } finally {
        $ErrorActionPreference = $prevErrorAction
    }
}

function Test-Ssh {
    $r = Invoke-Ssh "exit 0"
    return $LASTEXITCODE -eq 0
}

# --- Validacoes ---
if (-not (Test-Path $SshKey)) {
    Write-Host "ERRO: Chave SSH nao encontrada: $SshKey" -ForegroundColor Red
    Write-Host "Coloque ssh-key-2026-02-04.key na pasta ssh\" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n[0] Ajustando permissoes da chave SSH..." -ForegroundColor Cyan
icacls $SshKey /inheritance:r | Out-Null
icacls $SshKey /grant:r "$($env:USERNAME):(R)" | Out-Null

# --- Verificar se Blue-Green ja esta configurado (evitar null se SSH falhar) ---
$blueGreenOut = Invoke-Ssh "test -f /etc/systemd/system/klyon-app-blue.service && echo 1"
$blueGreenReady = ($null -ne $blueGreenOut -and $blueGreenOut -match "1")
# Apos reboot o SSH pode falhar ou demorar; se as pastas app_blue e app_green ja tem a app, considerar configurado
if (-not $blueGreenReady) {
    $bothHaveApp = Invoke-Ssh "test -f $AppBlue/KlyonHubPlatform.Api.dll -a -f $AppGreen/KlyonHubPlatform.Api.dll && echo 1"
    if ($null -ne $bothHaveApp -and $bothHaveApp -match "1") {
        $blueGreenReady = $true
        Write-Host "`n[Setup] Blue-Green detectado pelas pastas (units podem nao ter respondido). Pulando setup." -ForegroundColor Cyan
    }
}

if (-not $blueGreenReady) {
    Write-Host "`n[Setup] Blue-Green nao configurado. Executando setup na VM..." -ForegroundColor Cyan

    # 1. Obter connection string: unit klyon-app.service -> appsettings.Production.json na VM -> variavel $DeployConnectionString
    $connStr = $null
    $connLine = Invoke-Ssh "grep 'ConnectionStrings__DefaultConnection' /etc/systemd/system/klyon-app.service 2>/dev/null || true"
    $connLine = ($connLine | Where-Object { $_ -match "ConnectionStrings" }) -join "`n"
    if ($connLine -match "Environment=ConnectionStrings__DefaultConnection=(.+)") {
        $connStr = $Matches[1].Trim()
    }
    if (-not $connStr) {
        $jsonContent = Invoke-Ssh "cat /home/ubuntu/app/appsettings.Production.json 2>/dev/null || true"
        if ($jsonContent) {
            try {
                $json = $jsonContent | ConvertFrom-Json
                if ($json.ConnectionStrings.DefaultConnection) {
                    $connStr = $json.ConnectionStrings.DefaultConnection
                }
            } catch { }
        }
    }
    # Fallback: ler de app_blue ou app_green na VM (apos deploy anterior)
    if (-not $connStr) {
        foreach ($appPath in @($AppBlue, $AppGreen)) {
            $jsonContent = Invoke-Ssh "cat ${appPath}/appsettings.Production.json 2>/dev/null || true"
            if ($jsonContent) {
                try {
                    $json = $jsonContent | ConvertFrom-Json
                    if ($json.ConnectionStrings.DefaultConnection) {
                        $connStr = $json.ConnectionStrings.DefaultConnection
                        break
                    }
                } catch { }
            }
        }
    }
    # Fallback: ler do appsettings.Production.json local (sua maquina)
    if (-not $connStr) {
        $localAppSettings = Join-Path $PSScriptRoot "KlyonHubPlatform.Api\appsettings.Production.json"
        if (Test-Path $localAppSettings) {
            try {
                $json = Get-Content $localAppSettings -Raw | ConvertFrom-Json
                if ($json.ConnectionStrings.DefaultConnection) {
                    $connStr = $json.ConnectionStrings.DefaultConnection
                }
            } catch { }
        }
    }
    if (-not $connStr -and $DeployConnectionString) {
        $connStr = $DeployConnectionString
    }
    if (-not $connStr) {
        Write-Host "ERRO: Nao foi possivel obter connection string." -ForegroundColor Red
        Write-Host "  O script ja tentou: VM (klyon-app.service, app_blue/app_green) e KlyonHubPlatform.Api\appsettings.Production.json local." -ForegroundColor Gray
        Write-Host "  Opcoes:" -ForegroundColor Yellow
        Write-Host "  1) Coloque a connection string em KlyonHubPlatform.Api\appsettings.Production.json (ConnectionStrings:DefaultConnection) na sua maquina" -ForegroundColor Yellow
        Write-Host "  2) Na VM: appsettings.Production.json em /home/ubuntu/app ou em app_blue/app_green" -ForegroundColor Yellow
        Write-Host "  3) Defina `$DeployConnectionString no inicio do deploy.ps1" -ForegroundColor Yellow
        exit 1
    }

    # 2. Criar pastas (incluindo pasta compartilhada para chaves Data Protection - mantem sessao no switch)
    Write-Host "  Criando pastas app_blue, app_green e app_shared (Data Protection)..." -ForegroundColor Gray
    Invoke-Ssh "mkdir -p $AppBlue $AppGreen $AppBlue/wwwroot/documents $AppGreen/wwwroot/documents /home/ubuntu/app_shared/dataprotection_keys" | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "ERRO: Falha ao criar pastas." -ForegroundColor Red; exit 1 }

    # 3. Se app existir, copiar para blue e green
    $appExists = Invoke-Ssh "test -d /home/ubuntu/app && test -f /home/ubuntu/app/KlyonHubPlatform.Api.dll && echo 1"
    if ($appExists -match "1") {
        Write-Host "  Copiando app existente para app_blue e app_green..." -ForegroundColor Gray
        Invoke-Ssh "cp -r /home/ubuntu/app/. $AppBlue/ 2>/dev/null; cp -r /home/ubuntu/app/. $AppGreen/ 2>/dev/null" | Out-Null
    }

    # 4. Criar units systemd (arquivos locais temporarios)
    $unitBlue = @"
[Unit]
Description=Klyon App Blue (port $PortBlue)
After=network.target mariadb.service

[Service]
WorkingDirectory=$AppBlue
ExecStart=/usr/bin/dotnet $AppBlue/KlyonHubPlatform.Api.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:$PortBlue
Environment=ConnectionStrings__DefaultConnection=$connStr
User=ubuntu

[Install]
WantedBy=multi-user.target
"@
    $unitGreen = @"
[Unit]
Description=Klyon App Green (port $PortGreen)
After=network.target mariadb.service

[Service]
WorkingDirectory=$AppGreen
ExecStart=/usr/bin/dotnet $AppGreen/KlyonHubPlatform.Api.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:$PortGreen
Environment=ConnectionStrings__DefaultConnection=$connStr
User=ubuntu

[Install]
WantedBy=multi-user.target
"@
    $unitBluePath = Join-Path $env:TEMP "klyon-app-blue.service"
    $unitGreenPath = Join-Path $env:TEMP "klyon-app-green.service"
    [System.IO.File]::WriteAllText($unitBluePath, $unitBlue)
    [System.IO.File]::WriteAllText($unitGreenPath, $unitGreen)
    scp -i $SshKey -o StrictHostKeyChecking=no $unitBluePath "${VmUser}@${VmIp}:/tmp/klyon-app-blue.service" | Out-Null
    scp -i $SshKey -o StrictHostKeyChecking=no $unitGreenPath "${VmUser}@${VmIp}:/tmp/klyon-app-green.service" | Out-Null
    Remove-Item $unitBluePath, $unitGreenPath -ErrorAction SilentlyContinue
    Invoke-Ssh "sudo mv /tmp/klyon-app-blue.service /etc/systemd/system/ && sudo mv /tmp/klyon-app-green.service /etc/systemd/system/" | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "ERRO: Falha ao criar units systemd." -ForegroundColor Red; exit 1 }
    Write-Host "  Units systemd criados." -ForegroundColor Gray

    # 5. Criar config Nginx se nao existir
    $nginxExists = Invoke-Ssh "test -f $NginxConfig && echo 1"
    if ($nginxExists -notmatch "1") {
        $nginxContent = @"
server {
    listen 80;
    server_name _;
    location / {
        proxy_pass http://127.0.0.1:$PortBlue;
        proxy_http_version 1.1;
        proxy_set_header Upgrade `$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host `$host;
        proxy_set_header X-Real-IP `$remote_addr;
        proxy_set_header X-Forwarded-For `$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto `$scheme;
        proxy_set_header X-Forwarded-Host `$host;
        proxy_cache_bypass `$http_upgrade;
    }
}
"@
        $nginxPath = Join-Path $env:TEMP "klyon-app-nginx.conf"
        [System.IO.File]::WriteAllText($nginxPath, $nginxContent)
        scp -i $SshKey -o StrictHostKeyChecking=no $nginxPath "${VmUser}@${VmIp}:/tmp/klyon-app-nginx.conf" | Out-Null
        Remove-Item $nginxPath -ErrorAction SilentlyContinue
        Invoke-Ssh "sudo mv /tmp/klyon-app-nginx.conf $NginxConfig && sudo ln -sf $NginxConfig /etc/nginx/sites-enabled/ 2>/dev/null; sudo rm -f /etc/nginx/sites-enabled/default 2>/dev/null; sudo nginx -t && sudo systemctl reload nginx" | Out-Null
        Write-Host "  Config Nginx criada e ativada." -ForegroundColor Gray
    }

    # 6. Parar klyon-app antigo e iniciar blue
    Invoke-Ssh "sudo systemctl stop klyon-app 2>/dev/null || true; sudo systemctl disable klyon-app 2>/dev/null || true" | Out-Null
    Invoke-Ssh "sudo systemctl daemon-reload && sudo systemctl enable klyon-app-blue && sudo systemctl start klyon-app-blue" | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "ERRO: Falha ao iniciar klyon-app-blue." -ForegroundColor Red; exit 1 }
    Write-Host "  klyon-app-blue iniciado." -ForegroundColor Gray
    Write-Host "Setup concluido. Iniciando deploy..." -ForegroundColor Green
}

# --- Fase Deploy ---
Write-Host "`n[1] Publicando a aplicacao (Release)..." -ForegroundColor Cyan
dotnet publish "KlyonHubPlatform.Api\KlyonHubPlatform.Api.csproj" -c Release -o ".\publish"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[2] Criando app_new e enviando arquivos para a VM..." -ForegroundColor Cyan
Invoke-Ssh "rm -rf $AppNew 2>/dev/null; mkdir -p $AppNew $AppNew/wwwroot/documents /home/ubuntu/app_shared/dataprotection_keys" | Out-Null
scp -i $SshKey -o StrictHostKeyChecking=no -r ".\publish\*" "${VmUser}@${VmIp}:${AppNew}/"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: Falha no upload." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 3. Detectar qual instancia esta ativa (Nginx aponta para qual porta? Evitar null do SSH)
$proxyLine = Invoke-Ssh "grep proxy_pass $NginxConfig 2>/dev/null || true"
if ($null -eq $proxyLine) { $proxyLine = "" }
$activeBlue = ($proxyLine -match "5000")
if ($activeBlue) {
    $activePort = $PortBlue
    $inactivePort = $PortGreen
    $inactiveApp = $AppGreen
    $inactiveService = "klyon-app-green"
    $activeService = "klyon-app-blue"
} else {
    $activePort = $PortGreen
    $inactivePort = $PortBlue
    $inactiveApp = $AppBlue
    $inactiveService = "klyon-app-blue"
    $activeService = "klyon-app-green"
}
Write-Host "`n[3] Instancia ativa: porta $activePort. Deploy na inativa (porta $inactivePort)..." -ForegroundColor Cyan

# 4. Mover app_new para pasta inativa (substituir conteudo)
Write-Host "`n[4] Copiando novos arquivos para pasta inativa..." -ForegroundColor Cyan
Invoke-Ssh "rm -rf ${inactiveApp}/* ${inactiveApp}/.[!.]* 2>/dev/null; cp -r $AppNew/. $inactiveApp/; rm -rf $AppNew" | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "ERRO: Falha ao copiar arquivos." -ForegroundColor Red; exit 1 }

$deployDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
Invoke-Ssh "echo '${deployDate} UTC' > ${inactiveApp}/deploy-date.txt" | Out-Null

# 5. Iniciar servico inativo
Write-Host "`n[5] Iniciando servico inativo ($inactiveService)..." -ForegroundColor Cyan
Invoke-Ssh "sudo systemctl start $inactiveService" | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "ERRO: Falha ao iniciar $inactiveService." -ForegroundColor Red; exit 1 }

# 6. Health check na porta inativa (tratar retorno nulo do SSH para evitar erro ao chamar .Trim())
Write-Host "`n[6] Aguardando aplicacao ficar pronta (porta $inactivePort)..." -ForegroundColor Cyan
$maxAttempts = 30
$attempt = 0
$ready = $false
do {
    Start-Sleep -Seconds 2
    $curlOut = Invoke-Ssh "curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:${inactivePort}/ 2>/dev/null"
    $httpCode = if ($null -ne $curlOut) { $curlOut.ToString().Trim() } else { "" }
    if ($httpCode -match "200|302") {
        $ready = $true
        break
    }
    $attempt++
    Write-Host "  Tentativa $attempt/$maxAttempts..." -ForegroundColor Gray
} while ($attempt -lt $maxAttempts)
if (-not $ready) {
    Write-Host "ERRO: Aplicacao nao respondeu em 60s na porta $inactivePort. Abortando switch." -ForegroundColor Red
    Invoke-Ssh "sudo systemctl stop $inactiveService 2>/dev/null" | Out-Null
    exit 1
}
Write-Host "  Aplicacao pronta!" -ForegroundColor Green

# 7. Switch Nginx para a nova porta
Write-Host "`n[7] Trocando Nginx para porta $inactivePort..." -ForegroundColor Cyan
if ($activeBlue) {
    Invoke-Ssh "sudo sed -i 's/127.0.0.1:5000/127.0.0.1:5001/g' $NginxConfig" | Out-Null
} else {
    Invoke-Ssh "sudo sed -i 's/127.0.0.1:5001/127.0.0.1:5000/g' $NginxConfig" | Out-Null
}
Invoke-Ssh "sudo nginx -t && sudo systemctl reload nginx" | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "ERRO: Falha ao recarregar Nginx." -ForegroundColor Red; exit 1 }

# 8. Parar servico antigo
Write-Host "`n[8] Parando servico antigo ($activeService)..." -ForegroundColor Cyan
Invoke-Ssh "sudo systemctl stop $activeService" | Out-Null

# 9. Excluir arquivos da pasta que ficou inativa (nao esta rodando)
$folderInactive = if ($activeBlue) { $AppBlue } else { $AppGreen }
Write-Host "`n[9] Excluindo arquivos da pasta inativa ($folderInactive)..." -ForegroundColor Cyan
Invoke-Ssh "rm -rf ${folderInactive}/* ${folderInactive}/.??* 2>/dev/null; mkdir -p ${folderInactive}/wwwroot/documents 2>/dev/null" | Out-Null

Write-Host "`n[10] Removendo pasta publish local..." -ForegroundColor Cyan
Remove-Item -Recurse -Force ".\publish" -ErrorAction SilentlyContinue

Write-Host "`n=== Deploy concluido (zero downtime) ===" -ForegroundColor Green
Write-Host "Aplicacao: http://${VmIp}/" -ForegroundColor White
