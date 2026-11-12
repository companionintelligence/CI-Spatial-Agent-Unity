#!/usr/bin/env python3
"""Check a built APK for content it must not ship.

Scans every file inside the APK -- managed-code metadata, native libraries, Dex, the Android
manifest and Unity's serialized data files (scenes, Resources, settings) -- for names that only
the excluded vendor SDKs and tools contain:

* PICO OpenXR SDK: assemblies and namespaces, its controller / hand models, textures, shaders and
  animator states, its Java packages and native libraries. Forbidden in Quest builds (the PICO SDK
  licence forbids reproducing its material without PICO's written permission); expected in Pico
  builds, where it is only reported.
* MCP for Unity (edit-time tooling), Ready Player Me (removed), Unity Visual Scripting (removed):
  forbidden in every build.

Usage:  python tools/apk/scan_apk.py <apk> --target quest|pico [--verbose]

Exit status: 0 = clean, 1 = forbidden content found, 2 = the APK packs Unity data in an
LZ4-compressed data.unity3d this script cannot look into (turn off build compression to scan).
Standard library only.
"""

import argparse
import sys
import zipfile

# Case-sensitive byte markers per group. Chosen so that none of them occurs in Unity, Meta or
# project code: e.g. a bare "pico" also matches the project's own XRVendor.Pico enum, and "pvr"
# matches PVRTC texture-format names, so neither is used.
MARKERS = {
    "PICO SDK": [
        b"com.unity.xr.openxr.picoxr", b"Unity.XR.OpenXRPico", b"PICOSupport", b"Unity.XR.PICO",
        b"PICO.Platform", b"Pico.Platform", b"PICO.TobSupport", b"PXR_",
        b"PICO4", b"PICO 4", b"pico 4", b"pico4", b"PICO3_", b"PICONeo3", b"PICOG3",
        b"OpenXR_PICO_", b"SkinLPico", b"SkinRPico", b"PicoDown", b"PicoUp",
        b"com/pvr/", b"com.pvr.", b"com/picoxr/", b"com.picoxr", b"tobactivate",
        b"libopenxr_pico", b"libPICO_TOBAPI", b"libpvrcapturelib", b"libpxrplatformloader",
        b"libCameraRenderingPlugin",
    ],
    "MCP for Unity": [b"MCPForUnity", b"com.coplaydev"],
    "Ready Player Me": [b"ReadyPlayerMe", b"Ready Player Me", b"readyplayerme", b"readyplayer.me"],
    "Visual Scripting": [b"Unity.VisualScripting", b"com.unity.visualscripting"],
}

ALLOWED = {"quest": set(), "pico": {"PICO SDK"}}


def find(data, marker):
    """Count occurrences of marker in data as UTF-8/ASCII and as UTF-16LE."""
    count = data.count(marker)
    wide = marker.decode("ascii").encode("utf-16-le")
    return count + data.count(wide)


def context(data, marker, width=48):
    i = data.find(marker)
    if i < 0:
        i = data.find(marker.decode("ascii").encode("utf-16-le"))
    if i < 0:
        return ""
    chunk = data[max(0, i - width // 2): i + len(marker) + width // 2]
    return "".join(chr(b) if 32 <= b < 127 else "." for b in chunk)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("apk")
    parser.add_argument("--target", required=True, choices=sorted(ALLOWED))
    parser.add_argument("--verbose", action="store_true", help="list every matching file")
    args = parser.parse_args()

    hits = {group: {} for group in MARKERS}  # group -> {entry: {marker: count}}
    unscannable = []
    with zipfile.ZipFile(args.apk) as apk:
        entries = apk.infolist()
        for info in entries:
            if info.is_dir():
                continue
            data = apk.read(info)
            if info.filename.endswith("data.unity3d") and data[:8] == b"UnityFS\x00":
                unscannable.append(info.filename)
            for group, markers in MARKERS.items():
                for marker in markers:
                    n = find(data, marker)
                    if n:
                        hits[group].setdefault(info.filename, {})[marker] = (n, context(data, marker))

    print(f"{args.apk}: {len(entries)} entries scanned for a {args.target} build")
    failed = False
    for group, by_entry in hits.items():
        allowed = group in ALLOWED[args.target]
        total = sum(n for markers in by_entry.values() for n, _ in markers.values())
        if not by_entry:
            print(f"  {group}: none")
            continue
        verdict = "present (expected)" if allowed else "FORBIDDEN"
        failed |= not allowed
        print(f"  {group}: {verdict} - {total} matches in {len(by_entry)} files")
        shown = list(by_entry.items()) if args.verbose else list(by_entry.items())[:10 if not allowed else 5]
        for entry, markers in shown:
            first = next(iter(markers.items()))
            names = ", ".join(m.decode() for m in markers)
            print(f"    {entry}: {names}  e.g. \"{first[1][1]}\"")
        if len(shown) < len(by_entry):
            print(f"    ... {len(by_entry) - len(shown)} more (--verbose lists them)")

    if unscannable:
        print("  Cannot scan inside LZ4-compressed " + ", ".join(unscannable) +
              ": build without compression (Build Profiles > Compression Method: Default) to check it.")
        return 2
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
