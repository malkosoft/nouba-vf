@echo off
setlocal
cd /d "%~dp0"

set "NOUBA_URLS=http://0.0.0.0:5000"
set "APP=%CD%\bin\Debug\net8.0\Nouba.exe"

if exist "%APP%" (
  start "Nouba Server" "%APP%"
) else (
  start "Nouba Server" dotnet run --project "%CD%\Nouba.csproj" --urls http://0.0.0.0:5000
)

timeout /t 5 /nobreak >nul
start "" "http://127.0.0.1:5000/Admin/Login"
endlocal
