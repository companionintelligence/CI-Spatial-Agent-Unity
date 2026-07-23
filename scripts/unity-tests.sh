#!/usr/bin/env bash
set -euo pipefail

UNITY_BIN_DEFAULT="/Applications/Unity/Hub/Editor/6000.3.7f1/Unity.app/Contents/MacOS/Unity"
UNITY_BIN="${UNITY_BIN:-$UNITY_BIN_DEFAULT}"
PROJECT_PATH="${PROJECT_PATH:-/Users/liam/devel/ci/xr/CI-Unity-XR}"
RESULTS_PATH="${RESULTS_PATH:-$PROJECT_PATH/TestResults}"

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "Unity binary not found or not executable: $UNITY_BIN" >&2
  echo "Set UNITY_BIN to your Unity editor path (Unity Hub > Installs)." >&2
  exit 1
fi

mkdir -p "$RESULTS_PATH"

"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_PATH" \
  -runTests \
  -testPlatform EditMode \
  -testResults "$RESULTS_PATH/editmode.xml" \
  -quit

"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_PATH" \
  -runTests \
  -testPlatform PlayMode \
  -testResults "$RESULTS_PATH/playmode.xml" \
  -quit
