@echo off
echo ========================================
echo Building PromptedRevive Plugin
echo ========================================

:: Set paths
set PROJECT_FILE=CheekiRebreeki.csproj
set DLL_DIR=Built_DLL

:: Clean previous builds
echo Cleaning previous builds...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

:: Also clean any .cache files
if exist *.cache del /q *.cache

:: Build the project
echo Building project...
dotnet build "%PROJECT_FILE%" --configuration Release

:: Check if build succeeded
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ========================================
    echo BUILD FAILED!
    echo ========================================
    echo Please check the errors above.
    pause
    exit /b 1
)

:: Copy to Built_DLL folder
echo.
echo Copying to Built_DLL folder...
if not exist "%DLL_DIR%" mkdir "%DLL_DIR%"
copy /Y "bin\Release\PromptedRevive.dll" "%DLL_DIR%\"
if exist "bin\Release\PromptedRevive.pdb" copy /Y "bin\Release\PromptedRevive.pdb" "%DLL_DIR%\"

:: Clean up build folders
echo.
echo Cleaning up build folders...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo ========================================
echo BUILD SUCCESSFUL!
echo ========================================
echo Plugin has been copied to:
echo %CD%\%DLL_DIR%\PromptedRevive.dll
echo.
pause