@echo off
REM ============================================================================
REM  Nouba Pro — Publication de l'executable Windows autonome (single-file)
REM  Produit : publish\NoubaPro-1.0.0\Nouba.exe  (aucun .NET a installer chez
REM  le client). Lancer ce script depuis le dossier du projet (ou nouba\).
REM ============================================================================
setlocal
set VER=1.0.0
set OUT=publish\NoubaPro-%VER%

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERREUR] .NET SDK introuvable. Installez le SDK .NET 8 puis relancez.
  exit /b 1
)

echo.
echo === Nettoyage de %OUT% ===
if exist "%OUT%" rmdir /s /q "%OUT%"

echo === Publication self-contained / single-file / win-x64 (ReadyToRun) ===
dotnet publish Nouba.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishReadyToRun=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUT%"

if errorlevel 1 (
  echo.
  echo [ECHEC] La publication a echoue. Voir les messages ci-dessus.
  exit /b 1
)

echo.
echo === Verification des modeles vocaux Piper ===
if exist "%OUT%\wwwroot\tts\piper\ar_JO-kareem-medium.onnx" (
  echo   [OK] Modeles arabes presents.
) else (
  echo   [ATTENTION] Modeles .onnx absents de wwwroot\tts\piper.
  echo   Restaurez-les avant de livrer ^(voir docs\PIPER_INSTALL.md^).
)

echo.
echo ============================================================
echo  TERMINE.  Executable : %OUT%\Nouba.exe
echo  Distribuez le DOSSIER complet %OUT% (exe + dossier wwwroot).
echo ============================================================
endlocal
