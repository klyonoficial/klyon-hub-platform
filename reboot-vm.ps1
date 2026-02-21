# Reinicia a VM de deploy (use se a VM estiver lenta antes de rodar o deploy).
# Uso: powershell -ExecutionPolicy Bypass -File reboot-vm.ps1
# Apos reiniciar, aguarde 1-2 minutos e rode deploy.ps1

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$VmIp   = "168.75.93.94"
$VmUser = "ubuntu"
$SshKey = Join-Path $PSScriptRoot "ssh\ssh-key-2026-02-04.key"

if (-not (Test-Path $SshKey)) {
    Write-Host "ERRO: Chave SSH nao encontrada: $SshKey" -ForegroundColor Red
    exit 1
}

Write-Host "Reiniciando a VM ${VmUser}@${VmIp}..." -ForegroundColor Cyan
ssh -i $SshKey -o StrictHostKeyChecking=no -o ConnectTimeout=10 "${VmUser}@${VmIp}" "sudo reboot"
# SSH desconecta quando a VM reinicia; exit code pode ser 255 ou 0
Write-Host "Comando enviado. A VM esta reiniciando." -ForegroundColor Green
Write-Host "Aguarde 1 a 2 minutos e execute: .\deploy.ps1" -ForegroundColor Yellow
