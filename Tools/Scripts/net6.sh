
#!/bin/bash

# 获取处理器架构
arch=$(uname -m)
ver="6.0.36"
prefix="aspnetcore-runtime-$ver-linux"
source="http://x.newlifex.com"

echo arch: $arch

# 识别Alpine
if [ -f "/proc/version" ]; then
  cat /proc/version | grep -q -E 'musl|Alpine'
  if [ $? -eq 0 ]; then
    prefix="$prefix-musl"
    apk add libgcc libstdc++
  fi
fi

# 根据处理器架构选择下载的文件
if [ $arch == "x86_64" ]; then
  gzfile="$prefix-x64.tar.gz"
elif [ $arch == "amd64" ]; then
  gzfile="$prefix-x64.tar.gz"
elif [ $arch == "aarch64" ]; then
  gzfile="$prefix-arm64.tar.gz"
elif [ $arch == "armv7l" ]; then
  gzfile="$prefix-arm.tar.gz"
else
  gzfile="$prefix-$arch.tar.gz"
fi

echo gzfile: $gzfile

if [ ! -f "$gzfile" ]; then
	wget $source/dotnet/$ver/$gzfile
fi

# Ubuntu默认安装在/usr/lib目录
target="/usr/lib/dotnet"
if [ ! -d $target ]; then
	target="/usr/share/dotnet"
fi

echo target: $target

if [ ! -d $target ]; then
	mkdir $target
fi
tar -xzf $gzfile -C $target
if [ ! -f "/usr/bin/dotnet" ]; then
	ln $target/dotnet /usr/bin/dotnet -s
fi

dotnet --info
