
echo "WindowsÔËÐÐÐÇ³¾"

start server/StarServer.exe

ping 127.0.0.1 -n 5 > nul
start http://localhost:6600/api

start agent/StarAgent.exe -run
start web/StarWeb.exe urls=http://*:6680

ping 127.0.0.1 -n 5 > nul
start http://localhost:6680