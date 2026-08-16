@echo off
set "TARGET_DIR=%AppData%\Autodesk\Revit\Addins\2024"
set "ADDIN_NAME=MEPBuriedDepthCalculator"

echo ============================================================
echo Installing Hatco MEP Buried Depth Calculator for Revit 2024
echo ============================================================
echo.

:: 1. Create target directory if it doesn't exist
echo [1/3] Preparing folders...
if not exist "%TARGET_DIR%\%ADDIN_NAME%" mkdir "%TARGET_DIR%\%ADDIN_NAME%"

:: 2. Copy the .dll file FIRST (to ensure binaries are present before Revit detects the add-in)
echo [2/3] Copying application binaries (DLL)...
if exist "%~dp0%ADDIN_NAME%\%ADDIN_NAME%.dll" (
    copy /Y "%~dp0%ADDIN_NAME%\%ADDIN_NAME%.dll" "%TARGET_DIR%\%ADDIN_NAME%\"
) else (
    echo Error: %ADDIN_NAME%.dll not found in %~dp0%ADDIN_NAME%
    pause
    exit /b
)

:: 3. Copy the .addin file LAST
echo [3/3] Registering add-in with Revit (Manifest)...
copy /Y "%~dp0%ADDIN_NAME%.addin" "%TARGET_DIR%\"

echo.
echo ============================================================
echo Installation Complete!
echo Please restart Revit 2024 to see the "Hatco" tab.
echo ============================================================
echo.
pause
