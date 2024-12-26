
echo "Run Stardust on Windows"

clover40.exe net8-host -silent

start agent\StarAgent.exe -run -server http://127.0.0.1:6600

ping 127.0.0.1 -n 5 > nul
start http://localhost:6600/api
ping 127.0.0.1 -n 5 > nul
start http://localhost:6680
