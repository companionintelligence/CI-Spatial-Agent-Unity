#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PID_FILE="$ROOT_DIR/Logs/mcp-server.pid"

if [[ -f "$PID_FILE" ]]; then
  PID="$(cat "$PID_FILE")"
  if ps -p "$PID" >/dev/null 2>&1; then
    kill "$PID"
    echo "Stopped MCP server (pid $PID)"
  fi
  rm -f "$PID_FILE"
else
  pkill -f unity-mcp-server || true
  echo "Stopped MCP server by name"
fi
