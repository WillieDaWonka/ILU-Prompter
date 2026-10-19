@echo off
rem ILU Prompter - build (Release) and start the app from this folder.
rem Any arguments are passed on, e.g.  run_app.bat samples\sample-short.txt
rem No second display? Use run_app_single_screen.bat (output opens as a window on this screen).
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo The .NET SDK was not found. Install the .NET 8 SDK, then run this again.
    pause
    exit /b 1
)
echo Building ILU Prompter...
dotnet build src\IluPrompter.App -c Release -v q -nologo
if %ERRORLEVEL% NEQ 0 (
    echo Build failed - see the messages above.
    pause
    exit /b 1
)
start "" "%~dp0src\IluPrompter.App\bin\Release\net8.0-windows\win-x64\ILUPrompter.exe" %*
exit /b 0
