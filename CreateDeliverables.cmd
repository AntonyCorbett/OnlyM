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
"C:\Program Files\Microsoft Visual Studio\2022\Professional\Msbuild\Current\Bin\MsBuild.exe" OnlyMMirror\OnlyMMirror.vcxproj -t:Rebuild -p:Configuration=Release
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Copying OnlyMMirror items into delivery
copy OnlyMMirror\OnlyM\bin\x86\Release\OnlyMMirror.exe OnlyM\bin\Release\net7.0-windows\publish
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Copying OnlyMSlideManager items into delivery
xcopy OnlyMSlideManager\bin\Release\net7.0-windows\publish\*.* OnlyM\bin\Release\net7.0-windows\publish /q /s /y /d
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Copying VCRTL items into delivery
xcopy VCRTL\*.* OnlyM\bin\Release\net7.0-windows\publish /q
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Removing unwanted language files
rd OnlyM\bin\Release\net7.0-windows\publish\no-NO /q /s
rd OnlyM\bin\Release\net7.0-windows\publish\pap-PAP /q /s

ECHO.
ECHO Copying Satellite assemblies for language files
ECHO Czech
xcopy OnlyM\bin\Release\net7.0-windows\publish\cs\*.*  OnlyM\bin\Release\net7.0-windows\publish\cs-CZ /q
rd OnlyM\bin\Release\net7.0-windows\publish\cs /q /s
ECHO German
xcopy OnlyM\bin\Release\net7.0-windows\publish\de\*.*  OnlyM\bin\Release\net7.0-windows\publish\de-DE /q
rd OnlyM\bin\Release\net7.0-windows\publish\de /q /s
ECHO French
xcopy OnlyM\bin\Release\net7.0-windows\publish\fr\*.*  OnlyM\bin\Release\net7.0-windows\publish\fr-FR /q
rd OnlyM\bin\Release\net7.0-windows\publish\fr /q /s
ECHO Italian
xcopy OnlyM\bin\Release\net7.0-windows\publish\it\*.*  OnlyM\bin\Release\net7.0-windows\publish\it-IT /q
rd OnlyM\bin\Release\net7.0-windows\publish\it /q /s
ECHO Polish
xcopy OnlyM\bin\Release\net7.0-windows\publish\pl\*.*  OnlyM\bin\Release\net7.0-windows\publish\pl-PL /q
rd OnlyM\bin\Release\net7.0-windows\publish\pl /q /s
ECHO Russian
xcopy OnlyM\bin\Release\net7.0-windows\publish\ru\*.*  OnlyM\bin\Release\net7.0-windows\publish\ru-RU /q
rd OnlyM\bin\Release\net7.0-windows\publish\ru /q /s
ECHO Spanish
xcopy OnlyM\bin\Release\net7.0-windows\publish\es\*.*  OnlyM\bin\Release\net7.0-windows\publish\es-ES /q
xcopy OnlyM\bin\Release\net7.0-windows\publish\es\*.*  OnlyM\bin\Release\net7.0-windows\publish\es-MX /q
rd OnlyM\bin\Release\net7.0-windows\publish\es /q /s
ECHO Turkish
xcopy OnlyM\bin\Release\net7.0-windows\publish\tr\*.*  OnlyM\bin\Release\net7.0-windows\publish\tr-TR /q
rd OnlyM\bin\Release\net7.0-windows\publish\tr /q /s

ECHO.
ECHO Creating installer
"D:\Program Files (x86)\Inno Setup 6\iscc" Installer\onlymsetup.iss
IF %ERRORLEVEL% NEQ 0 goto ERROR

ECHO.
ECHO Creating portable zip
md Installer\Output
powershell Compress-Archive -Path OnlyM\bin\Release\net7.0-windows\publish\* -DestinationPath Installer\Output\OnlyMPortable.zip 
IF %ERRORLEVEL% NEQ 0 goto ERROR

goto SUCCESS

:ERROR
ECHO.
ECHO ******************
ECHO An ERROR occurred!
ECHO ******************

:SUCCESS

PAUSE