REM Run from dev command line

@ECHO OFF

VERIFY ON

D:
cd \ProjectsPersonal\OnlyM
rd OnlyM\bin /q /s
rd OnlyMSlideManager\bin /q /s
rd OnlyMMirror\OnlyM /q /s
rd Installer\Output /q /s
rd Installer\Staging /q /s

ECHO.
ECHO Publishing OnlyM
dotnet publish OnlyM\OnlyM.csproj -p:PublishProfile=FolderProfile -c:Release
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Publishing OnlyMSlideManager
dotnet publish OnlyMSlideManager\OnlyMSlideManager.csproj -p:PublishProfile=FolderProfile -c:Release
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Building OnlyMMirror
CALL :FindMSBuild
IF %ERRORLEVEL% NEQ 0 goto ERROR
"%MSBUILD_EXE%" OnlyMMirror\OnlyMMirror.vcxproj -t:Rebuild -p:Configuration=Release
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Copying OnlyMMirror items into delivery
copy OnlyMMirror\OnlyM\bin\x86\Release\OnlyMMirror.exe OnlyM\bin\Release\net9.0-windows\publish
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Copying OnlyMSlideManager items into delivery
xcopy OnlyMSlideManager\bin\Release\net9.0-windows\publish\*.* OnlyM\bin\Release\net9.0-windows\publish /q /s /y /d
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Copying VCRTL items into delivery
xcopy VCRTL\*.* OnlyM\bin\Release\net9.0-windows\publish /q
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Removing unwanted language files
rd OnlyM\bin\Release\net9.0-windows\publish\no-NO /q /s
rd OnlyM\bin\Release\net9.0-windows\publish\pap-PAP /q /s

ECHO.
ECHO Copying Satellite assemblies for language files
ECHO Czech
xcopy OnlyM\bin\Release\net9.0-windows\publish\cs\*.*  OnlyM\bin\Release\net9.0-windows\publish\cs-CZ /q
rd OnlyM\bin\Release\net9.0-windows\publish\cs /q /s
ECHO German
xcopy OnlyM\bin\Release\net9.0-windows\publish\de\*.*  OnlyM\bin\Release\net9.0-windows\publish\de-DE /q
rd OnlyM\bin\Release\net9.0-windows\publish\de /q /s
ECHO French
xcopy OnlyM\bin\Release\net9.0-windows\publish\fr\*.*  OnlyM\bin\Release\net9.0-windows\publish\fr-FR /q
rd OnlyM\bin\Release\net9.0-windows\publish\fr /q /s
ECHO Italian
xcopy OnlyM\bin\Release\net9.0-windows\publish\it\*.*  OnlyM\bin\Release\net9.0-windows\publish\it-IT /q
rd OnlyM\bin\Release\net9.0-windows\publish\it /q /s
ECHO Polish
xcopy OnlyM\bin\Release\net9.0-windows\publish\pl\*.*  OnlyM\bin\Release\net9.0-windows\publish\pl-PL /q
rd OnlyM\bin\Release\net9.0-windows\publish\pl /q /s
ECHO Russian
xcopy OnlyM\bin\Release\net9.0-windows\publish\ru\*.*  OnlyM\bin\Release\net9.0-windows\publish\ru-RU /q
rd OnlyM\bin\Release\net9.0-windows\publish\ru /q /s
ECHO Spanish
xcopy OnlyM\bin\Release\net9.0-windows\publish\es\*.*  OnlyM\bin\Release\net9.0-windows\publish\es-ES /q
xcopy OnlyM\bin\Release\net9.0-windows\publish\es\*.*  OnlyM\bin\Release\net9.0-windows\publish\es-MX /q
rd OnlyM\bin\Release\net9.0-windows\publish\es /q /s
ECHO Turkish
xcopy OnlyM\bin\Release\net9.0-windows\publish\tr\*.*  OnlyM\bin\Release\net9.0-windows\publish\tr-TR /q
rd OnlyM\bin\Release\net9.0-windows\publish\tr /q /s

ECHO.
ECHO Creating installer
"D:\Program Files (x86)\Inno Setup 6\iscc" Installer\onlymsetup.iss
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Creating portable zip
md Installer\Output
powershell Compress-Archive -Path OnlyM\bin\Release\net9.0-windows\publish\* -DestinationPath Installer\Output\OnlyMPortable.zip 
IF %ERRORLEVEL% NEQ 0 goto ERROR

goto SUCCESS

:FindMSBuild
SET "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
IF NOT EXIST "%VSWHERE%" (
    ECHO Could not find vswhere.exe at "%VSWHERE%"
    EXIT /B 1
)

SET "MSBUILD_EXE="
SET "MSBUILD_PATH_FILE=%TEMP%\onlym_msbuild_path.txt"

"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\Current\Bin\MSBuild.exe" > "%MSBUILD_PATH_FILE%"
IF %ERRORLEVEL% NEQ 0 (
    ECHO vswhere.exe failed while searching for MSBuild.exe.
    EXIT /B 1
)

SET /P MSBUILD_EXE=<"%MSBUILD_PATH_FILE%"
DEL "%MSBUILD_PATH_FILE%" >NUL 2>NUL

IF NOT DEFINED MSBUILD_EXE (
    ECHO Could not find MSBuild.exe. Check that the Visual Studio MSBuild component is installed.
    EXIT /B 1
)

IF NOT EXIST "%MSBUILD_EXE%" (
    ECHO MSBuild.exe was reported as "%MSBUILD_EXE%", but that file does not exist.
    EXIT /B 1
)

ECHO Using MSBuild: "%MSBUILD_EXE%"
EXIT /B 0

:ERROR
ECHO.
ECHO ******************
ECHO An ERROR occurred!
ECHO ******************

:SUCCESS

PAUSE