#!/usr/bin/env python3
"""Check that THIRD_PARTY_NOTICES.md names every library a built APK or AAB packages.

Unity's Android build adds libraries that no Unity package lists: with the GameActivity entry
point, the generated Gradle project pulls AndroidX, the Android Games SDK, Kotlin and
kotlinx.coroutines from Maven, and packs the NDK's libc++_shared.so. The EditMode test
ThirdPartyNoticesTests only compares the notices with Packages/packages-lock.json, so it cannot
see them. This script reads what was actually packaged and fails when THIRD_PARTY_NOTICES.md
does not name:

* each Maven artifact that left a META-INF/<group>_<artifact>.version file (AndroidX) or a
  META-INF/<module>.version file (kotlinx), as `group:artifact` with that version on the same
  line, so an upgrade fails until the notices are updated too;
* each native library lib/<abi>/<name>.so, as `<name>.so`;
* each Java package in classes*.dex, through a `package.*` pattern in backticks (the notices
  give one for every library, including those, like plain JARs, that leave no version file).

Usage:  python tools/apk/check_notices.py <apk|aab> [--notices THIRD_PARTY_NOTICES.md] [--verbose]

Exit status: 0 = everything packaged is named, 1 = something is missing (listed), 2 = the file
is not an APK or AAB this script can read. Standard library only.
"""

import argparse
import collections
import os
import re
import struct
import sys
import zipfile

DEFAULT_NOTICES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "THIRD_PARTY_NOTICES.md")


def normalized(name, bundle):
    """Maps an AAB module entry (base/root/..., base/dex/..., base/lib/...) to its APK path."""
    parts = name.split("/", 2)
    if bundle and len(parts) == 3 and parts[1] in ("root", "dex", "lib"):
        return "lib/" + parts[2] if parts[1] == "lib" else parts[2]
    return name


def uleb128(data, offset):
    value = shift = 0
    while True:
        byte = data[offset]
        offset += 1
        value |= (byte & 0x7F) << shift
        if byte < 0x80:
            return value, offset
        shift += 7


def dex_packages(data):
    """Counts the classes defined in a .dex file per Java package (dotted)."""
    if data[:4] != b"dex\n":
        raise ValueError("not a dex file")
    string_ids_off, = struct.unpack_from("<I", data, 0x3C)
    type_ids_off, = struct.unpack_from("<I", data, 0x44)
    class_defs_size, class_defs_off = struct.unpack_from("<2I", data, 0x60)
    packages = collections.Counter()
    for i in range(class_defs_size):
        class_idx, = struct.unpack_from("<I", data, class_defs_off + 32 * i)
        descriptor_idx, = struct.unpack_from("<I", data, type_ids_off + 4 * class_idx)
        string_off, = struct.unpack_from("<I", data, string_ids_off + 4 * descriptor_idx)
        _, start = uleb128(data, string_off)
        descriptor = data[start:data.index(b"\x00", start)].decode("utf-8", "replace")  # Lpkg/Name;
        packages[descriptor[1:-1].rpartition("/")[0].replace("/", ".")] += 1
    return packages


def coordinate_of(version_file):
    """androidx.core_core -> androidx.core:core; kotlinx_coroutines_core -> :kotlinx-coroutines-core."""
    group, sep, artifact = version_file.partition("_")
    if sep and "." in group:
        return f"{group}:{artifact}"
    return ":" + version_file.replace("_", "-")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("package", help="APK or AAB")
    parser.add_argument("--notices", default=DEFAULT_NOTICES, help="default: THIRD_PARTY_NOTICES.md of this repository")
    parser.add_argument("--verbose", action="store_true", help="also list what each notices entry covers")
    args = parser.parse_args()

    notices = open(args.notices, encoding="utf-8").read()
    lines = notices.splitlines()
    patterns = sorted(set(re.findall(r"`([A-Za-z_]\w*(?:\.\w+)*)\.\*`", notices)))

    versions, libs, packages = {}, set(), collections.Counter()
    try:
        with zipfile.ZipFile(args.package) as archive:
            bundle = "BundleConfig.pb" in archive.namelist()
            for info in archive.infolist():
                name = normalized(info.filename, bundle)
                match = re.fullmatch(r"META-INF/([^/]+)\.version", name)
                if match:
                    versions[match.group(1)] = archive.read(info).decode("utf-8", "replace").strip()
                match = re.fullmatch(r"lib/[^/]+/([^/]+\.so)", name)
                if match:
                    libs.add(match.group(1))
                if re.fullmatch(r"classes\d*\.dex", name):
                    packages.update(dex_packages(archive.read(info)))
    except (OSError, zipfile.BadZipFile, ValueError, IndexError, struct.error) as error:
        print(f"{args.package}: cannot read it as an APK or AAB ({error})")
        return 2
    if not packages:
        print(f"{args.package}: no classes*.dex found; not an APK or AAB")
        return 2

    problems = []

    for version_file, version in sorted(versions.items()):
        coordinate = coordinate_of(version_file)
        named = re.compile(re.escape(coordinate) + r"(?![\w.-])")
        with_version = re.compile(r"(?<![\w.])" + re.escape(version) + r"(?![\w.])")
        rows = [line for line in lines if named.search(line)]
        if not rows:
            problems.append(f"Maven artifact {coordinate.lstrip(':')} {version} (META-INF/{version_file}.version) is not named")
        elif not any(with_version.search(line) for line in rows):
            problems.append(f"Maven artifact {coordinate.lstrip(':')} is named, but not with version {version} on the same line")

    for lib in sorted(libs):
        if f"`{lib}`" not in notices:
            problems.append(f"native library {lib} is not named (as `{lib}`)")

    covered = collections.Counter()
    for package, count in sorted(packages.items()):
        prefix = next((p for p in sorted(patterns, key=len, reverse=True) if package == p or package.startswith(p + ".")), None)
        if prefix is None:
            problems.append(f"Java package {package or '(default package)'} ({count} classes) matches no `<package>.*` pattern")
        else:
            covered[prefix] += count

    print(f"{args.package}: {len(versions)} META-INF version files, {len(libs)} native libraries, "
          f"{sum(packages.values())} dex classes in {len(packages)} packages, checked against {os.path.basename(args.notices)} "
          f"({len(patterns)} package patterns)")
    if args.verbose:
        for prefix in patterns:
            print(f"  `{prefix}.*`: {covered[prefix]} classes")
        for lib in sorted(libs):
            print(f"  {lib}")
        for version_file, version in sorted(versions.items()):
            print(f"  {coordinate_of(version_file).lstrip(':')} {version}")
    if problems:
        print(f"  {len(problems)} things the APK packages are not named in the notices:")
        for problem in problems:
            print(f"    {problem}")
        return 1
    print("  every packaged library is named in the notices")
    return 0


if __name__ == "__main__":
    sys.exit(main())
