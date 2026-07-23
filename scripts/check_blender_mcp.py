#!/usr/bin/env python3
import json
import socket
import sys

host = '127.0.0.1'
port = 9876
payload = {"type": "get_scene_info", "params": {}}

data = json.dumps(payload).encode("utf-8")
try:
    with socket.create_connection((host, port), timeout=5) as sock:
        sock.sendall(data)
        sock.settimeout(5)
        buf = b""
        while True:
            try:
                chunk = sock.recv(8192)
            except socket.timeout:
                break
            if not chunk:
                break
            buf += chunk
            try:
                print(buf.decode("utf-8"))
                sys.exit(0)
            except Exception:
                pass
except Exception as exc:
    print(f"Blender MCP not reachable on {host}:{port}: {exc}")
    sys.exit(1)

print("No response from Blender MCP")
sys.exit(1)
