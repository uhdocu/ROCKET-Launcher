@echo off
rem
cd /d "%~dp0"
dotnet build RocketLauncher.csproj -c Release -o bin
if errorlevel 1 pause & exit /b 1
echo.
echo Built bin\RocketLauncher.exe
pause
