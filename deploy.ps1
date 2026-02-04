# =============================================================================
# Deploy Klyon Hub Platform - Publicar, enviar para a VM Oracle Cloud e limpar
# Uso: duplo clique neste script ou: powershell -ExecutionPolicy Bypass -File deploy.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# --- Configuração (ajuste o IP se a instância mudar) ---
$VmIp       = "168.75.93.94"
$VmUser     = "ubuntu"
$RemotePath = "/home/ubuntu/app"
$SshKey     = Join-Path $PSScriptRoot "ssh\ssh-key-2026-02-04.key"

if (-not (Test-Path $SshKey)) {
    Write-Host "ERRO: Chave SSH nao encontrada: $SshKey" -ForegroundColor Red
    Write-Host "Coloque ssh-key-2026-02-04.key na pasta ssh\" -ForegroundColor Yellow
    exit 1
}

# Ajusta permissoes da chave SSH (exigido pelo OpenSSH no Windows)
Write-Host "`n[0/6] Ajustando permissoes da chave SSH..." -ForegroundColor Cyan
icacls $SshKey /inheritance:r | Out-Null
icacls $SshKey /grant:r "$($env:USERNAME):(R)" | Out-Null

Write-Host "`n[1/6] Publicando a aplicacao (Release)..." -ForegroundColor Cyan
dotnet publish "KlyonHubPlatform.Api\KlyonHubPlatform.Api.csproj" -c Release -o ".\publish"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[2/6] Excluindo arquivos e pastas antigas na VM..." -ForegroundColor Cyan
ssh -i $SshKey -o StrictHostKeyChecking=no "${VmUser}@${VmIp}" "find $RemotePath -mindepth 1 -delete 2>/dev/null || true"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Aviso: Limpeza na VM falhou (pasta pode nao existir). Continuando." -ForegroundColor Yellow
}

Write-Host "`n[3/6] Garantindo pastas na VM..." -ForegroundColor Cyan
ssh -i $SshKey -o StrictHostKeyChecking=no "${VmUser}@${VmIp}" "mkdir -p $RemotePath $RemotePath/wwwroot/documents"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Aviso: SSH para criar pastas falhou. Se a pasta app ja existe, ignore." -ForegroundColor Yellow
}

Write-Host "`n[4/6] Enviando arquivos para a VM (scp)..." -ForegroundColor Cyan
scp -i $SshKey -o StrictHostKeyChecking=no -r ".\publish\*" "${VmUser}@${VmIp}:${RemotePath}/"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[5/6] Removendo pasta publish local..." -ForegroundColor Cyan
Remove-Item -Recurse -Force ".\publish" -ErrorAction SilentlyContinue

Write-Host "`n[6/6] Reiniciando o servico klyon-app na VM..." -ForegroundColor Cyan
ssh -i $SshKey -o StrictHostKeyChecking=no "${VmUser}@${VmIp}" "sudo systemctl restart klyon-app 2>/dev/null || true"

Write-Host "`n=== Deploy concluido ===" -ForegroundColor Green
Write-Host "Aplicacao: http://${VmIp}/" -ForegroundColor White
