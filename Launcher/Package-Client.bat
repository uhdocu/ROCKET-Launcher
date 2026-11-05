@echo off
rem
rem
rem
set "YEAR=%~1"
if "%YEAR%"=="" set "YEAR=2020"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Package-Client.ps1" -Year %YEAR%
pause
