#!/bin/bash

# 获取处理器架构
arch=$(uname -m)
ver="8.0.19"
prefix="aspnetcore-runtime-$ver-linux"
source="http://x.newlifex.com"

# 识别Alpine和musl环境
if [ -f "/proc/version" ] && cat /proc/version | grep -q -E 'musl|Alpine'; then
  # Alpine系统
  prefix="$prefix-musl"
  apk add libgcc libstdc++
elif ldd --version 2>&1 | grep -q 'musl'; then
  # 常见musl环境
  prefix="$prefix-musl"
elif ls /lib/ld-musl-* >/dev/null 2>&1; then
  # 其他musl环境
  prefix="$prefix-musl"
fi

# 根据处理器架构选择下载的文件
if [ $arch == "x86_64" ]; then
  gzfile="$prefix-x64.tar.gz"
elif [ $arch == "aarch64" ]; then
  gzfile="$prefix-arm64.tar.gz"
elif [ $arch == "armv7l" ]; then
  gzfile="$prefix-arm.tar.gz"
elif [ $arch == "riscv64" ]; then
  gzfile="dotnet-sdk-8.0.101-linux-riscv64-gcc.tar.gz"
	wget $source/dotnet/$gzfile
elif [ $arch == "loongarch64" ]; then
  gzfile="dotnet-sdk-8.0.100-linux-x64.tar.gz"
	wget $source/dotnet/$gzfile
else
  gzfile="$prefix-$arch.tar.gz"
fi

if [ ! -f "$gzfile" ]; then
	wget $source/dotnet/$ver/$gzfile
fi
if [ ! -d "/usr/share/dotnet/" ]; then
	mkdir /usr/share/dotnet
fi
tar -xzf $gzfile -C /usr/share/dotnet
if [ ! -f "/usr/bin/dotnet" ]; then
	ln /usr/share/dotnet/dotnet /usr/bin/dotnet -s
fi

# centos/neokylin/alinux需要替换libstdc++运行时库
if [ $arch == "x86_64" ] && [ -f /etc/os-release ]; then
  os_id=$(grep '^ID=' /etc/os-release | awk -F= '{print $2}' | tr -d '"')
  id_like=$(grep '^ID_LIKE=' /etc/os-release | awk -F= '{print $2}' | tr -d '"')

  echo os_id: $os_id
  echo id_like: $id_like

  # 检查是否为 centos/neokylin/alinux 或者 ID_LIKE 包含 centos
  if [ "$os_id" == "centos" ] || [ "$os_id" == "neokylin" ] || [ "$os_id" == "alinux" ] || [[ "$id_like" == *"centos"* ]]; then
    libstd=/usr/lib64/libstdc++.so.6
    libsrc=/usr/lib64/libstdc++.so.6.0.26
    if [ -f $libstd ] && [ ! -f $libsrc ]; then
      if [ ! -f libstdcpp.6.0.26.so ]; then
        wget $source"/dotnet/libstdcpp.6.0.26.so"
      fi

      cp libstdcpp.6.0.26.so $libsrc
      chmod +x $libsrc
      rm $libstd
      ln -s $libsrc $libstd
    fi

	  yum install -y libicu
  elif [ "$os_id" == "Linx" ]; then
    libstd=/usr/lib/x86_64-linux-gnu/libstdc++.so.6
    libsrc=/usr/lib/x86_64-linux-gnu/libstdc++.so.6.0.26
    if [ -f $libstd ] && [ ! -f $libsrc ]; then
      if [ ! -f libstdcpp.6.0.26.so ]; then
        wget $source"/dotnet/libstdcpp.6.0.26.so"
      fi

      cp libstdcpp.6.0.26.so $libsrc
      chmod +x $libsrc
      rm $libstd
      ln -s $libsrc $libstd
    fi

	  apt install -y libicu
  else
	  apt install -y libicu
  fi
fi

dotnet --info

rm $gzfile -f
# rm net.sh
