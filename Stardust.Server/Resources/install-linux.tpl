#!/bin/bash
# 星尘 StarAgent 安装脚本。由 StarServer 安装引导页生成，请勿手工修改
set -e

SERVER="{{server}}"
AGENT_URL="{{agentUrl}}"
AGENT_FILE="{{agentFile}}"
AGENT_HASH="{{agentHash}}"
RUNTIME_URL="{{runtimeUrl}}"
RUNTIME_FILE="{{runtimeFile}}"
RUNTIME_HASH="{{runtimeHash}}"

if [ "$(id -u)" != "0" ]; then
    echo "需要 root 权限，请使用 sudo 执行" >&2
    exit 1
fi

download() {
    if command -v curl >/dev/null 2>&1; then
        curl -fL --retry 3 -o "$2" "$1"
    elif command -v wget >/dev/null 2>&1; then
        wget -O "$2" "$1"
    else
        echo "未找到 curl 或 wget" >&2
        exit 1
    fi
}

verify() {
    local file="$1" hash="$2"
    [ -z "$hash" ] && return 0
    [ ${#hash} -ne 32 ] && return 0
    command -v md5sum >/dev/null 2>&1 || return 0
    local actual
    actual=$(md5sum "$file" | awk '{print $1}')
    if [ "$actual" != "$hash" ]; then
        echo "文件校验失败：$file" >&2
        exit 1
    fi
}

echo "安装目标服务端：$SERVER"

{{runtimeBlock}}

download "$AGENT_URL" "/tmp/$AGENT_FILE"
verify "/tmp/$AGENT_FILE" "$AGENT_HASH"

mkdir -p /opt/staragent
tar -xzf "/tmp/$AGENT_FILE" -C /opt/staragent
cd /opt/staragent
dotnet StarAgent.dll -uninstall >/dev/null 2>&1 || true
dotnet StarAgent.dll -install -server "$SERVER"
rm -f "/tmp/$AGENT_FILE"

echo "StarAgent 安装完成：$SERVER"
