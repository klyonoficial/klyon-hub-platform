@echo off
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "%~dp0reboot-vm.ps1"
pause
