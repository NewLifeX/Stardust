
#!/bin/bash

echo "Install Stardust on Linux"

if [ ! -d "/usr/share/dotnet/" ]; then
	curl https://x.newlifex.com/dotnet/net8.sh | sudo bash
fi

sudo dotnet agent/StarAgent.dll -install -server http://127.0.0.1:6600

echo "starting ..."
sleep 5
curl -w "\n" http://localhost:6600/api

sleep 5
curl -w "\n" http://localhost:6680/cube/info

echo "Install finished!"