
echo "Install Stardust as service on Windows"

clover40.exe net7-aspnet -silent

agent\StarAgent.exe -install -server http://127.0.0.1:6600
