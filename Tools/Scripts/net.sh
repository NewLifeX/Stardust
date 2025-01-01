
#!/bin/bash

# 获取处理器架构
arch=$(uname -m)
ver="9.0.0"
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
elif [ $arch == "riscv64" ]; then
  gzfile="dotnet-sdk-8.0.101-linux-riscv64-gcc.tar.gz"
  wget $source/dotnet/$gzfile
elif [ $arch == "loongarch64" ]; then
  gzfile="aspnetcore-runtime-8.0.5-linux-loongarch64.tar.gz"
  wget $source/dotnet/$gzfile
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

# centos需要替换libstdc++运行时库
if [ $arch == "x86_64" ] && [ -f /etc/os-release ]; then
  os_id=$(grep '^ID=' /etc/os-release | awk -F= '{print $2}' | tr -d '"')

  echo os_id: $os_id

  if [ "$os_id" == "centos" ]; then
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
  elif [ "$os_id" == "neokylin" ]; then
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
if [ $arch == "aarch64" ] && [ -f /etc/os-release ]; then
  os_id=$(grep '^ID=' /etc/os-release | awk -F= '{print $2}' | tr -d '"')

  echo os_id: $os_id

  if [ "$os_id" == "KylinSecOS" ]; then
    libstd=/lib64/libstdc++.so.6
    libsrc=/lib64/libstdc++.so.6.0.28
    if [ -f $libstd ] && [ ! -f $libsrc ]; then
      if [ ! -f libstdcpp-arm64.6.0.28.so ]; then
        wget $source"/dotnet/libstdcpp-arm64.6.0.28.so"
      fi

      cp libstdcpp-arm64.6.0.28.so $libsrc
      chmod +x $libsrc
      rm $libstd
      ln -s $libsrc $libstd
    fi

	  yum install -y libicu
  else
	  apt install -y libicu
  fi
fi

dotnet --info

# rm $gzfile -f
# rm net.sh