#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/StarAgent.iOS/StarAgent.iOS.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME="${RUNTIME:-iossimulator-arm64}"
BUNDLE_ID="com.newlife.staragent"
TIMEOUT="${TIMEOUT:-180}"
# smoke：等待登录并心跳成功，需要可用的 StarServer；start：只验证冷启动，CI 无服务端时使用
MODE="${MODE:-smoke}"
ARTIFACTS="$ROOT/Bin/Agent/mobile/ios/artifacts"

case "$MODE" in
  smoke)
    APP_ARG="--smoke"
    RESULT_NAME="agent-smoke.json"
    OK_PATTERN='"ok":true'
    PASS_LABEL="iOS smoke passed."
    ;;
  start)
    APP_ARG="--start"
    RESULT_NAME="agent-start.json"
    OK_PATTERN='"started":true'
    PASS_LABEL="iOS start smoke passed."
    ;;
  *)
    echo "Unknown MODE: $MODE (expected smoke|start)"
    exit 2
    ;;
esac

mkdir -p "$ARTIFACTS"

if [ "${SKIP_BUILD:-0}" != "1" ]; then
  dotnet build "$PROJECT" -c "$CONFIGURATION" -f net10.0-ios -p:RuntimeIdentifier="$RUNTIME" --nologo
fi

APP="$(find "$ROOT/Bin/Agent/mobile/ios" -maxdepth 5 -name "*.app" -print -quit)"
if [ -z "$APP" ]; then
  echo "iOS app bundle not found."
  exit 1
fi

UDID="$(xcrun simctl list devices available | awk -F '[()]' '/iPhone/ {print $2; exit}')"
if [ -z "$UDID" ]; then
  echo "No iPhone simulator available."
  exit 1
fi

echo "Using simulator $UDID (mode=$MODE)"
xcrun simctl boot "$UDID" 2>/dev/null || true
xcrun simctl bootstatus "$UDID" -b
xcrun simctl install "$UDID" "$APP"
xcrun simctl terminate "$UDID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl launch "$UDID" "$BUNDLE_ID" "$APP_ARG"

RESULT=""
DEADLINE=$((SECONDS + TIMEOUT))
while [ "$SECONDS" -lt "$DEADLINE" ]; do
  DATA_DIR="$(xcrun simctl get_app_container "$UDID" "$BUNDLE_ID" data 2>/dev/null || true)"
  if [ -n "$DATA_DIR" ]; then
    RESULT="$(find "$DATA_DIR" -name "$RESULT_NAME" -print -quit 2>/dev/null || true)"
    if [ -n "$RESULT" ]; then
      break
    fi
  fi
  sleep 2
done

if [ -z "$RESULT" ]; then
  echo "$RESULT_NAME not found within ${TIMEOUT}s. Recent app log:"
  xcrun simctl spawn "$UDID" log show --last 2m --style compact --predicate 'process == "StarAgent.iOS"' || true
  exit 1
fi

cp "$RESULT" "$ARTIFACTS/ios-$MODE.json"
cat "$RESULT"

if command -v jq >/dev/null 2>&1; then
  if [ "$MODE" = "start" ]; then
    jq -e '.started == true' "$RESULT" >/dev/null
  else
    jq -e '.ok == true' "$RESULT" >/dev/null
  fi
else
  grep -q "$OK_PATTERN" "$RESULT"
fi

echo "$PASS_LABEL"