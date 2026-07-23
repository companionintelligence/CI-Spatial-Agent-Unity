#!/usr/bin/env python3
import argparse
import json
import os
import socket
import sys
from pathlib import Path

HOST = os.environ.get("BLENDER_MCP_HOST", "127.0.0.1")
PORT = int(os.environ.get("BLENDER_MCP_PORT", "9876"))
TIMEOUT = float(os.environ.get("BLENDER_MCP_TIMEOUT", "10.0"))

RESET_CMDS = ["reset_scene", "new_scene", "clear_scene", "file_new"]
IMPORT_CMDS = ["import_gltf", "import_glb", "import_gltf_file", "import_scene.gltf"]
EXPORT_CMDS = ["export_fbx", "export_fbx_file", "export_scene.fbx"]


def send_cmd(cmd_type, params):
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
    return {"status": "error", "message": "No response"}


def try_commands(cmds, params):
    for cmd in cmds:
        resp = send_cmd(cmd, params)
        if resp.get("status") in ("ok", "success", True) or resp.get("ok") is True:
            return True, cmd, resp
    return False, cmds[-1], resp


def convert_one(src: Path, out_dir: Path):
    ok, cmd, resp = try_commands(RESET_CMDS, {})
    if not ok:
        return False, f"reset failed ({cmd}): {resp}"

    ok, cmd, resp = try_commands(IMPORT_CMDS, {"path": str(src)})
    if not ok:
        return False, f"import failed ({cmd}): {resp}"

    out_path = out_dir / (src.stem + ".fbx")
    ok, cmd, resp = try_commands(EXPORT_CMDS, {"path": str(out_path)})
    if not ok:
        return False, f"export failed ({cmd}): {resp}"

    return True, str(out_path)


def main():
    parser = argparse.ArgumentParser(description="Batch convert GLB/GLTF to FBX via Blender MCP")
    parser.add_argument("--input", required=True, help="Input directory containing .glb/.gltf files")
    parser.add_argument("--output", required=True, help="Output directory for FBX")
    parser.add_argument("--pattern", default="*.glb", help="Glob pattern (default *.glb)")
    args = parser.parse_args()

    src_dir = Path(args.input)
    out_dir = Path(args.output)
    if not src_dir.exists():
        print(f"Input directory missing: {src_dir}")
        return 1

    out_dir.mkdir(parents=True, exist_ok=True)

    files = list(src_dir.glob(args.pattern))
    if args.pattern.lower().endswith(".glb") is False:
        files += list(src_dir.glob("*.glb"))
    files += list(src_dir.glob("*.gltf"))
    files = sorted(set(files))

    if not files:
        print("No files found to convert.")
        return 0

    print(f"Found {len(files)} files. Converting via MCP at {HOST}:{PORT} ...")
    success = 0
    for src in files:
        ok, info = convert_one(src, out_dir)
        if ok:
            print(f"[OK] {src.name} -> {info}")
            success += 1
        else:
            print(f"[ERR] {src.name}: {info}")

    print(f"Done. Converted {success}/{len(files)} files.")
    return 0 if success == len(files) else 2


if __name__ == "__main__":
    sys.exit(main())
