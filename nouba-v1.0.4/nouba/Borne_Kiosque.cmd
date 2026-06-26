@echo off
setlocal
cd /d "%~dp0"

set "NOUBA_URLS=http://0.0.0.0:5000"
set "APP=%CD%\bin\Debug\net8.0\Nouba.exe"
set "URL=http://127.0.0.1:5000/Borne"

if exist "%APP%" (
  start "Nouba Server" "%APP%"
) else (
  start "Nouba Server" dotnet run --project "%CD%\Nouba.csproj" --urls http://0.0.0.0:5000
)

timeout /t 5 /nobreak >nul

set "BROWSER="
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" set "BROWSER=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not defined BROWSER if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" set "BROWSER=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"
if not defined BROWSER if exist "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" set "BROWSER=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"
if not defined BROWSER if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" set "BROWSER=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"

if defined BROWSER (
  start "" "%BROWSER%" --kiosk --kiosk-printing --autoplay-policy=no-user-gesture-required --disable-session-crashed-bubble --app="%URL%"
) else (
  start "" "%URL%"
)
endlocal
