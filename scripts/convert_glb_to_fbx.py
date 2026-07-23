#!/usr/bin/env python3
import argparse
import json
import os
import socket
import sys

HOST = os.environ.get('BLENDER_MCP_HOST', '127.0.0.1')
PORT = int(os.environ.get('BLENDER_MCP_PORT', '9876'))
TIMEOUT = float(os.environ.get('BLENDER_MCP_TIMEOUT', '10'))


def send_command(cmd_type, params):
    payload = {"type": cmd_type, "params": params or {}}
    data = json.dumps(payload).encode("utf-8")
    with socket.create_connection((HOST, PORT), timeout=TIMEOUT) as sock:
        sock.sendall(data)
        sock.settimeout(TIMEOUT)
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
                return json.loads(buf.decode("utf-8"))
            except json.JSONDecodeError:
                continue
    if not buf:
        return {"status": "error", "message": "no response"}
    try:
        return json.loads(buf.decode("utf-8"))
    except Exception:
        return {"status": "error", "message": "invalid json response"}


def build_code(src, dst):
    src = src.replace('\\', '\\\\')
    dst = dst.replace('\\', '\\\\')
    return f"""
import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r'{src}')
# Apply transforms
for obj in bpy.context.scene.objects:
    obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.export_scene.fbx(filepath=r'{dst}', use_selection=False)
"""


def main():
    parser = argparse.ArgumentParser(description="Convert GLB avatars to FBX via Blender MCP")
    parser.add_argument("input", help="GLB file or folder")
    parser.add_argument("--out", default="Assets/Avatars/Converted", help="Output folder")
    args = parser.parse_args()

    src = args.input
    out_dir = args.out
    os.makedirs(out_dir, exist_ok=True)

    files = []
    if os.path.isdir(src):
        for name in os.listdir(src):
            if name.lower().endswith('.glb'):
                files.append(os.path.join(src, name))
    else:
        files = [src]

    if not files:
        print("No GLB files found")
        return 1

    for glb in files:
        base = os.path.splitext(os.path.basename(glb))[0]
        fbx = os.path.join(out_dir, base + '.fbx')
        code = build_code(glb, fbx)
        resp = send_command('execute_code', {"code": code})
        print(glb, '->', fbx, resp)

    return 0


if __name__ == '__main__':
    raise SystemExit(main())
