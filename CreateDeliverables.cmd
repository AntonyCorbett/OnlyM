REM Run from dev command line
D:
cd \ProjectsPersonal\OnlyM
rd OnlyM\bin /q /s
rd OnlyMSlideManager\bin /q /s
rd OnlyMMirror\OnlyM /q /s
rd Installer\Output /q /s

REM build / publish
dotnet publish OnlyM\OnlyM.csproj -p:PublishProfile=FolderProfile -c:Release
dotnet publish OnlyMSlideManager\OnlyMSlideManager.csproj -p:PublishProfile=FolderProfile -c:Release
msbuild OnlyMMirror\OnlyMMirror.vcxproj -t:Rebuild -p:Configuration=Release

REM copy items into delivery
copy OnlyMMirror\OnlyM\bin\x86\Release\OnlyMMirror.exe OnlyM\bin\Release\net5.0-windows\publish
xcopy OnlyMSlideManager\bin\Release\net5.0-windows\publish\*.* OnlyM\bin\Release\net5.0-windows\publish /q /s /y /d
xcopy VCRTL\*.* OnlyM\bin\Release\net5.0-windows\publish /q

REM Remove unwanted language files
del OnlyM\bin\Release\net5.0-windows\publish\no-NO\*.dll
del OnlyM\bin\Release\net5.0-windows\publish\pap-PAP\*.dll

REM Create installer
"C:\Program Files (x86)\Inno Setup 6\iscc" Installer\onlymsetup.iss

REM create portable zip
powershell Compress-Archive -Path OnlyM\bin\Release\net5.0-windows\publish\* -DestinationPath Installer\Output\OnlyMPortable.zip 