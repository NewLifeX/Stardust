set base=..\Bin\Star

set out=%base%\server
rmdir /s/q %out%
dotnet publish ..\Stardust.Server\Stardust.Server.csproj --nologo -c Release -o %out%
del /f/s/q %out%\*.pdb %out%\*.xml %out%\*.deps.json

set out=%base%\web
rmdir /s/q %out%
dotnet publish ..\Stardust.Web\Stardust.Web.csproj --nologo -c Release -o %out%
del /f/s/q %out%\*.pdb %out%\*.xml %out%\*.deps.json
rmdir /s/q %out%\wwwroot

set out=%base%\agent
rmdir /s/q %out%
dotnet publish ..\StarAgent\StarAgent.csproj --nologo -c Release -f net6.0 -o %out%
del /f/s/q %out%\*.pdb %out%\*.deps.json

xcopy Resources\*.* %base%\ /y/s