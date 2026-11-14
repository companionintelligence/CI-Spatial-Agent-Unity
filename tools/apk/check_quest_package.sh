#!/usr/bin/env bash
# Checks a Quest APK or app bundle built by XRScanBuilder.BuildQuest: what it packages, its manifest and how it is
# signed. The Quest APK workflow (debug builds) and the Release workflow (release builds) both run it, so every
# build CI makes is held to the same expectations, which mirror docs/BUILD.md ("Verifying a build").
#
# Usage: tools/apk/check_quest_package.sh <package.apk|package.aab> <debug|release>
#
#   debug    signed with any key (Unity's debug key in CI).
#   release  signed by exactly one signer that is not Unity's debug key (CN=Android Debug).
#
# Environment:
#   UNITY_VERSION              the editor version. aapt2, apksigner, bundletool and the JDK are run from the
#                              unityci/editor:ubuntu-<version>-android-* image, which the build step has pulled.
#   VERSION_NAME, VERSION_CODE the versionName and versionCode the package must have (Player Settings > Version).
#   GITHUB_STEP_SUMMARY        optional: the package's details are appended there.
#
# Exit status: 0 when every check passes, 1 otherwise; each failed check is reported as an ::error annotation.
set -uo pipefail

usage="usage: $0 <package.apk|package.aab> <debug|release>"
package="${1:?$usage}"
signing="${2:?$usage}"
: "${UNITY_VERSION:?UNITY_VERSION is not set}" "${VERSION_NAME:?VERSION_NAME is not set}" "${VERSION_CODE:?VERSION_CODE is not set}"
summary="${GITHUB_STEP_SUMMARY:-/dev/null}"
title="Quest package check failed"

case "$signing" in
  debug | release) ;;
  *) echo "$usage" >&2; exit 2 ;;
esac
case "$package" in
  *.apk) kind=apk; libs_prefix=lib ;;
  # An app bundle keeps the base module's files under base/.
  *.aab) kind=aab; libs_prefix=base/lib ;;
  *) echo "$usage" >&2; exit 2 ;;
esac

if [[ ! -s "$package" ]]; then
  echo "::error title=Quest package missing::${package} was not produced. Search the build log above for '[XRScanBuilder]'."
  exit 1
fi
errors=()

# Native libraries: ARM64 only, and none of the PICO / MockHMD libraries that XRScanNativeLibExcluder strips from
# Quest builds.
mapfile -t libs < <(unzip -Z1 "$package" | grep "^${libs_prefix}/" || true)
has_lib() {
  local lib
  for lib in "${libs[@]}"; do
    if [[ "$lib" == "$1" ]]; then return 0; fi
  done
  return 1
}
for lib in "${libs[@]}"; do
  [[ "$lib" == "${libs_prefix}/arm64-v8a/"* ]] || errors+=("Non-ARM64 native library packaged: ${lib}")
done
for name in libil2cpp.so libunity.so libopenxr_loader.so; do
  has_lib "${libs_prefix}/arm64-v8a/${name}" || errors+=("Missing native library ${libs_prefix}/arm64-v8a/${name}")
done
for name in libPICO_TOBAPI.so libpvrcapturelib.so libpxrplatformloader.so libCameraRenderingPlugin.so libopenxr_pico.so libUnityMockHMD.so; do
  if has_lib "${libs_prefix}/arm64-v8a/${name}"; then errors+=("PICO/MockHMD library packaged in the Quest build: ${name}"); fi
done

# Every file in the package, serialized assets included: no PICO SDK, MCP for Unity, Ready Player Me or Visual
# Scripting content (standard-library Python).
if ! python3 tools/apk/scan_apk.py "$package" --target quest; then
  errors+=("tools/apk/scan_apk.py found content the Quest build must not ship, or could not scan it (see its output above)")
fi

# THIRD_PARTY_NOTICES.md names every library the package holds: each Maven artifact that left a META-INF/*.version
# file (with that version), each native library and each Java package in classes.dex. Unity's Gradle build adds
# libraries (AndroidX, Kotlin, kotlinx.coroutines, GameActivity, the NDK's libc++_shared.so) that
# packages-lock.json, and so the EditMode test ThirdPartyNoticesTests, never sees.
if ! python3 tools/apk/check_notices.py "$package"; then
  errors+=("tools/apk/check_notices.py found libraries in the package that THIRD_PARTY_NOTICES.md does not name, or could not read it (see its output above)")
fi

# Manifest and signature: aapt2, apksigner, bundletool and the JDK of the Unity Android image the build just used.
inspector="$(mktemp)"
trap 'rm -f "$inspector"' EXIT
cat > "$inspector" <<'SH'
set -u
newest() { find /opt -type f -path "$1" 2>/dev/null | sort -V | tail -n 1; }
bt="$(newest '*/build-tools/*/aapt2')"
if [ -z "$bt" ]; then bt="$(find / -xdev -type f -path '*/build-tools/*/aapt2' 2>/dev/null | sort -V | tail -n 1)"; fi
if [ -z "$bt" ]; then echo "aapt2 not found in the Unity image" >&2; exit 1; fi
bt="$(dirname "$bt")"
# The editor's own JDK.
java="${JAVA_HOME:+$JAVA_HOME/bin/java}"
if [ -z "$java" ] || [ ! -x "$java" ]; then java="$(newest '*/AndroidPlayer/OpenJDK/bin/java')"; fi
if [ -z "$java" ]; then java="$(command -v java || true)"; fi
if [ -z "$java" ]; then echo "java not found in the Unity image" >&2; exit 1; fi
jdk="$(dirname "$java")"
case "$1" in
  apk-manifest)
    "$bt/aapt2" dump badging "$2" || true
    "$bt/aapt2" dump permissions "$2" || true
    "$bt/aapt2" dump xmltree --file AndroidManifest.xml "$2" || true ;;
  apk-signature)
    "$java" -jar "$bt/lib/apksigner.jar" verify --verbose --print-certs "$2" ;;
  aab-manifest)
    bundletool="$(newest '*/AndroidPlayer/Tools/bundletool-all-*.jar')"
    if [ -z "$bundletool" ]; then echo "bundletool not found in the Unity image" >&2; exit 1; fi
    "$java" -jar "$bundletool" dump manifest --bundle "$2" ;;
  aab-signature)
    # An app bundle carries a JAR signature. jarsigner prints "jar verified." only for a valid one; keytool names
    # each signer's certificate.
    "$jdk/jarsigner" -verify "$2" && "$jdk/keytool" -printcert -jarfile "$2" ;;
esac
SH
image=""
while read -r ref; do
  if [[ "$ref" == "unityci/editor:ubuntu-${UNITY_VERSION}-android-"* ]]; then image="$ref"; break; fi
done < <(docker image ls --format '{{.Repository}}:{{.Tag}}')
inspect() {
  docker run --rm --entrypoint /bin/bash \
    -v "$inspector:/inspect.sh:ro" -v "$(cd "$(dirname "$package")" && pwd):/package:ro" \
    "$image" /inspect.sh "$1" "/package/$(basename "$package")"
}
manifest=""
signature=""
if [[ -z "$image" ]]; then
  errors+=("No unityci/editor Android image for ${UNITY_VERSION} found locally to run aapt2, apksigner and bundletool")
else
  manifest="$(inspect "${kind}-manifest")" || errors+=("Could not read the manifest of ${package}")
  signature="$(inspect "${kind}-signature")" || errors+=("Could not verify the signature of ${package}")
fi

if [[ -n "$manifest" ]]; then
  expect() { grep -Eq -- "$1" <<< "$manifest" || errors+=("$2"); }
  reject() { if grep -Eq -- "$1" <<< "$manifest"; then errors+=("$2"); fi; }
  if [[ "$kind" == apk ]]; then
    # aapt2 dump badging / permissions / xmltree.
    expect "^package: name='com\.companionintelligence\.xrscan'" "Package name is not com.companionintelligence.xrscan"
    grep '^package: ' <<< "$manifest" | grep -qF "versionCode='${VERSION_CODE}' versionName='${VERSION_NAME}'" ||
      errors+=("versionCode and versionName are not ${VERSION_CODE} and ${VERSION_NAME} (Player Settings > Version)")
    expect "^(minSdkVersion|sdkVersion):'32'$" "Minimum SDK is not API 32"
    expect "^targetSdkVersion:'34'$" "Target SDK is not API 34 (the Meta Horizon Store requires exactly 34)"
    expect "^native-code: 'arm64-v8a'$" "native-code is not exactly arm64-v8a"
    expect "^install-location:'auto'$" "install-location is not auto (the Meta Horizon Store requires it; Player Settings and QuestManifestRules set it)"
    permission() { printf "name='%s'" "${1//./\\.}"; }
    # Passthrough before the splash screen (Meta camera feature, Passthrough Pre Splash Screen).
    grep -A 2 '"com\.oculus\.ossplash\.background"' <<< "$manifest" | grep -q '"passthrough-contextual"' ||
      errors+=("Missing meta-data com.oculus.ossplash.background = passthrough-contextual (Passthrough Pre Splash Screen)")
    # Quest 3 and 3S only: scanning needs the Passthrough Camera API, which only they have (QuestManifestRules).
    grep -A 2 '"com\.oculus\.supportedDevices"' <<< "$manifest" | grep -qF '="quest3|quest3s"' ||
      errors+=("Missing meta-data com.oculus.supportedDevices = quest3|quest3s, exactly (QuestManifestRules)")
  else
    # bundletool dump manifest: the manifest as XML.
    expect "package=\"com\.companionintelligence\.xrscan\"" "Package name is not com.companionintelligence.xrscan"
    grep -qF "android:versionCode=\"${VERSION_CODE}\"" <<< "$manifest" ||
      errors+=("versionCode is not ${VERSION_CODE} (Player Settings > Version)")
    grep -qF "android:versionName=\"${VERSION_NAME}\"" <<< "$manifest" ||
      errors+=("versionName is not ${VERSION_NAME} (Player Settings > Version)")
    expect "android:minSdkVersion=\"32\"" "Minimum SDK is not API 32"
    expect "android:targetSdkVersion=\"34\"" "Target SDK is not API 34 (the Meta Horizon Store requires exactly 34)"
    expect 'android:installLocation="(auto|0)"' "installLocation is not auto (the Meta Horizon Store requires it; Player Settings and QuestManifestRules set it)"
    permission() { printf 'android:name="%s"' "${1//./\\.}"; }
    grep 'com\.oculus\.ossplash\.background' <<< "$manifest" | grep -q 'passthrough-contextual' ||
      errors+=("Missing meta-data com.oculus.ossplash.background = passthrough-contextual (Passthrough Pre Splash Screen)")
    grep 'com\.oculus\.supportedDevices' <<< "$manifest" | grep -qF 'android:value="quest3|quest3s"' ||
      errors+=("Missing meta-data com.oculus.supportedDevices = quest3|quest3s, exactly (QuestManifestRules)")
  fi
  expect "$(permission android.permission.RECORD_AUDIO)" "Missing android.permission.RECORD_AUDIO (added by XRScanManifestPostProcessor)"
  expect "$(permission horizonos.permission.HEADSET_CAMERA)" "Missing horizonos.permission.HEADSET_CAMERA (comes from the Meta AR camera feature)"
  expect "$(permission com.oculus.permission.USE_SCENE)" "Missing com.oculus.permission.USE_SCENE (comes from com.unity.xr.meta-openxr)"
  reject "com\.pvr\.tobactivate\.permission\.AUTH_CHECK" "PICO permission com.pvr.tobactivate.permission.AUTH_CHECK is in the Quest build"
  # Unity adds both with the microphone and the app uses neither; XRScanManifestPostProcessor removes them (Meta
  # VRC.Quest.Security.2: request the minimum). BLUETOOTH would also make android.hardware.bluetooth a required feature,
  # which the same name attribute matches: aapt2's uses-feature and uses-implied-feature, bundletool's <uses-feature>.
  reject "$(permission android.permission.BLUETOOTH)" "android.permission.BLUETOOTH is in the Quest build (XRScanManifestPostProcessor removes it)"
  reject "$(permission android.permission.MODIFY_AUDIO_SETTINGS)" "android.permission.MODIFY_AUDIO_SETTINGS is in the Quest build (XRScanManifestPostProcessor removes it)"
  reject "$(permission android.hardware.bluetooth)" "The Quest build declares or implies the feature android.hardware.bluetooth"
  # Meta features XRScanBuilder keeps off: Social Eye Gaze and the unused anchor, boundary and colocation ones.
  for name in EYE_TRACKING BOUNDARY_VISIBILITY IMPORT_EXPORT_IOT_MAP_DATA USE_COLOCATION_DISCOVERY_API; do
    reject "com\.oculus\.permission\.${name}" "com.oculus.permission.${name} is in the Quest build (its Meta feature should be off)"
  done
fi

signer=""
signer_sha256=""
if [[ -n "$signature" ]]; then
  if [[ "$kind" == apk ]]; then
    # The Meta Horizon Store accepts APK signature scheme v1 or v2; Unity signs with v2.
    grep -qx 'Verified using v2 scheme (APK Signature Scheme v2): true' <<< "$signature" ||
      errors+=("The APK is not signed with APK signature scheme v2")
    grep -qx 'Number of signers: 1' <<< "$signature" || errors+=("The APK does not have exactly one signer")
    signer="$(sed -n 's/^Signer #1 certificate DN: //p' <<< "$signature" | head -n 1)"
    signer_sha256="$(sed -n 's/^Signer #1 certificate SHA-256 digest: //p' <<< "$signature" | head -n 1)"
  else
    grep -qx 'jar verified\.' <<< "$signature" || errors+=("The app bundle does not carry a valid JAR signature")
    [[ "$(grep -c '^Signer #' <<< "$signature")" == 1 ]] || errors+=("The app bundle does not have exactly one signer")
    signer="$(sed -n 's/^Owner: //p' <<< "$signature" | head -n 1)"
    signer_sha256="$(sed -n 's/^[[:space:]]*SHA256: //p' <<< "$signature" | head -n 1 | tr -d ':' | tr '[:upper:]' '[:lower:]')"
  fi
  if [[ "$signing" == release && ( -z "$signer" || "$signer" == *"CN=Android Debug"* ) ]]; then
    errors+=("A release build is signed with '${signer:-no certificate}', not the release keystore")
  fi
fi

{
  echo "### Quest $( [[ "$kind" == apk ]] && echo APK || echo app bundle )"
  echo
  echo "- File: \`$(basename "$package")\` ($(du -h "$package" | cut -f 1))"
  echo "- SHA-256: \`$(sha256sum "$package" | cut -d ' ' -f 1)\`"
  echo "- Unity: \`${UNITY_VERSION}\`"
  echo "- Version: \`${VERSION_NAME}\`, versionCode \`${VERSION_CODE}\`"
  if [[ "$signing" == release ]]; then
    echo "- Signed with the release keystore: \`${signer:-unknown}\`, certificate SHA-256 \`${signer_sha256:-unknown}\`. Compare it with the key's recorded fingerprint before uploading."
  elif [[ "$signer" == *"CN=Android Debug"* ]]; then
    echo "- Signed with Unity's debug key (\`${signer}\`). Not for a store upload."
  else
    echo "- Signed with \`${signer:-unknown}\`, certificate SHA-256 \`${signer_sha256:-unknown}\`."
  fi
  if [[ "$kind" == apk && -n "$manifest" ]]; then
    echo
    echo '```'
    grep -E "^(package|install-location|sdkVersion|minSdkVersion|targetSdkVersion|native-code|uses-permission):" <<< "$manifest" | sort -u || true
    echo '```'
  fi
} >> "$summary"

if (( ${#errors[@]} > 0 )); then
  for error in "${errors[@]}"; do echo "::error title=${title}::${error}"; done
  exit 1
fi
echo "Quest ${kind} checks passed (${signing} signing: ${signer:-unknown})."
