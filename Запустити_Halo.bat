@echo off
set "LOCAL_EXE=%~dp0Halo.App\bin\Debug\net9.0-windows\Halo.App.exe"
set "D_EXE=D:\Halo\Halo.App\bin\Debug\net9.0-windows\Halo.App.exe"
set "C_EXE=C:\Users\volod\OneDrive\Desktop\Halo\Halo.App\bin\Debug\net9.0-windows\Halo.App.exe"

if exist "%LOCAL_EXE%" (
    start "" "%LOCAL_EXE%"
    exit /b 0
)

if exist "%D_EXE%" (
    start "" "%D_EXE%"
    exit /b 0
)

if exist "%C_EXE%" (
    start "" "%C_EXE%"
    exit /b 0
)

echo [Halo] Building project...
dotnet build "%~dp0Halo.sln"
if exist "%LOCAL_EXE%" (
    start "" "%LOCAL_EXE%"
    exit /b 0
)

pause
