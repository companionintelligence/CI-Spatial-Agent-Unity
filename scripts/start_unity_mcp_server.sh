#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="$ROOT_DIR/Library/mcp-server/osx-arm64/unity-mcp-server"
PORT=${UNITY_MCP_PORT:-51360}
TIMEOUT=${UNITY_MCP_PLUGIN_TIMEOUT:-10000}
export UNITY_MCP_CLIENT_TRANSPORT=${UNITY_MCP_CLIENT_TRANSPORT:-http}

if [[ ! -x "$SERVER" ]]; then
  echo "Unity MCP server not found at $SERVER" >&2
  exit 1
fi

exec "$SERVER" --port="$PORT" --plugin-timeout="$TIMEOUT" --client-transport="$UNITY_MCP_CLIENT_TRANSPORT"
