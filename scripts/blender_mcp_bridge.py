#!/usr/bin/env python3
import json
import os
import socket
import sys
from typing import Any, Dict

HOST = os.environ.get("BLENDER_MCP_HOST", "127.0.0.1")
PORT = int(os.environ.get("BLENDER_MCP_PORT", "9876"))
SOCK_TIMEOUT = float(os.environ.get("BLENDER_MCP_TIMEOUT", "5.0"))

SERVER_INFO = {
    "name": "blender-mcp-bridge",
    "version": "0.1.0",
}

TOOLS = [
    {
        "name": "blender.command",
        "description": "Send a command to the Blender MCP socket server.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "type": {"type": "string", "description": "Blender MCP command type"},
                "params": {"type": "object", "description": "Command parameters"},
            },
            "required": ["type"],
            "additionalProperties": False,
        },
    }
]


def send_blender_command(cmd_type: str, params: Dict[str, Any]) -> Dict[str, Any]:
    payload = {"type": cmd_type, "params": params or {}}
    data = json.dumps(payload).encode("utf-8")

    with socket.create_connection((HOST, PORT), timeout=SOCK_TIMEOUT) as sock:
        sock.sendall(data)
        sock.settimeout(SOCK_TIMEOUT)
        buffer = b""
        while True:
            try:
                chunk = sock.recv(8192)
            except socket.timeout:
                break
            if not chunk:
                break
            buffer += chunk
            try:
                return json.loads(buffer.decode("utf-8"))
            except json.JSONDecodeError:
                continue

    if buffer:
        try:
            return json.loads(buffer.decode("utf-8"))
        except json.JSONDecodeError:
            return {"status": "error", "message": "Invalid JSON response from Blender MCP"}
    return {"status": "error", "message": "No response from Blender MCP"}


def respond(msg_id, result=None, error=None):
    response = {"jsonrpc": "2.0", "id": msg_id}
    if error is not None:
        response["error"] = error
    else:
        response["result"] = result
    sys.stdout.write(json.dumps(response) + "\n")
    sys.stdout.flush()


def main():
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
        except json.JSONDecodeError:
            continue

        msg_id = req.get("id")
        method = req.get("method")
        params = req.get("params") or {}

        if method == "initialize":
            result = {
                "protocolVersion": "2024-11-05",
                "capabilities": {"tools": {}},
                "serverInfo": SERVER_INFO,
            }
            respond(msg_id, result=result)
        elif method == "tools/list":
            respond(msg_id, result={"tools": TOOLS})
        elif method == "tools/call":
            tool_name = params.get("name")
            args = params.get("arguments") or {}
            if tool_name != "blender.command":
                respond(msg_id, error={"code": -32602, "message": f"Unknown tool: {tool_name}"})
                continue
            cmd_type = args.get("type")
            cmd_params = args.get("params") or {}
            if not cmd_type:
                respond(msg_id, error={"code": -32602, "message": "Missing 'type' argument"})
                continue
            try:
                result = send_blender_command(cmd_type, cmd_params)
                respond(msg_id, result=result)
            except Exception as e:
                respond(msg_id, error={"code": -32000, "message": str(e)})
        elif method == "ping":
            respond(msg_id, result={"status": "ok"})
        else:
            respond(msg_id, error={"code": -32601, "message": f"Method not found: {method}"})


if __name__ == "__main__":
    main()
