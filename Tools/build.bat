set base=..\Bin\Star

del %base%\*.* /f/s/q

set out=%base%\server
rmdir /s/q %out%
dotnet publish ..\Stardust.Server\Stardust.Server.csproj --nologo -c Release -o %out%
del /f/s/q %out%\*.pdb %out%\*.xml %out%\*.deps.json

set out=%base%\web
rmdir /s/q %out%
dotnet publish ..\Stardust.Web\Stardust.Web.csproj --nologo -c Release -o %out%
del /f/s/q %out%\*.pdb %out%\*.xml %out%\*.deps.json
rmdir /s/q %out%\wwwroot

clover80.exe zip %base%\StarWeb.zip %out%\
del %out%\*.exe %out%\*.dll %out%\*.config /f/s/q
move %base%\StarWeb.zip %out%\StarWeb.zip

set out=%base%\agent
rmdir /s/q %out%
dotnet publish ..\StarAgent\StarAgent.csproj --nologo -c Release -f net8.0 -o %out%
del /f/s/q %out%\*.pdb %out%\*.xml %out%\*.deps.json

mkdir %out%\Config
xcopy Resources\backup\*.config %out%\Config\ /y/s

xcopy Resources\*.bat %base%\ /y/s
xcopy Resources\*.sh %base%\ /y/s
copy clover40.exe %base%\clover40.exe /y

mkdir %base%\Plugins

del %base%\..\star.zip /f
clover80.exe zip %base%\..\star.zip %base%\
move %base%\..\star.zip %base%\
