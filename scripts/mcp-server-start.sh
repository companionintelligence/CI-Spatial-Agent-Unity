#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN="$ROOT_DIR/Library/mcp-server/osx-arm64/unity-mcp-server"
LOG="$ROOT_DIR/Logs/mcp-server.log"
PID_FILE="$ROOT_DIR/Logs/mcp-server.pid"
PORT="${1:-${UNITY_MCP_PORT:-51360}}"
TIMEOUT="${UNITY_MCP_PLUGIN_TIMEOUT:-10000}"
CLIENT_TRANSPORT="${UNITY_MCP_CLIENT_TRANSPORT:-http}"

if lsof -nP -iTCP:"$PORT" | grep -q LISTEN; then
  echo "MCP server already listening on port $PORT"
  exit 0
fi

mkdir -p "$(dirname "$LOG")"

nohup "$BIN" --port="$PORT" --plugin-timeout="$TIMEOUT" --client-transport="$CLIENT_TRANSPORT" > "$LOG" 2>&1 &
echo $! > "$PID_FILE"
echo "MCP server started (pid $(cat "$PID_FILE")) on port $PORT"
