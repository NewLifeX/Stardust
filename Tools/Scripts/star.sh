
#!/bin/bash

if [ ! -d "/usr/lib/dotnet/" ] && [ ! -d "/usr/share/dotnet/" ]; then
	curl http://x.newlifex.com/dotnet/net.sh | bash
fi

gzfile="staragent90.tar.gz"
if [ ! -f "$gzfile" ]; then
	wget "http://x.newlifex.com/star/"$gzfile
fi
if [ ! -d "agent/" ]; then
	mkdir agent
fi
tar -xzf $gzfile -C agent

cd agent

dotnet StarAgent.dll -uninstall
dotnet StarAgent.dll -install -server http://s.newlifex.com:6600

cd ..
rm $gzfile -f
rm star.sh -f
