
#!/bin/bash

gzfile="aspnetcore-runtime-3.1.30-linux-x64.tar.gz"
if [ ! -f "$gzfile" ]; then
	wget "http://x.newlifex.com/dotnet/"$gzfile
fi
if [ ! -d "/usr/share/dotnet/" ]; then
	mkdir /usr/share/dotnet
fi
tar -xzf $gzfile -C /usr/share/dotnet
if [ ! -f "/usr/bin/dotnet" ]; then
	ln /usr/share/dotnet/dotnet /usr/bin/dotnet -s
fi

dotnet --info

rm $gzfile -f
rm net31.sh

