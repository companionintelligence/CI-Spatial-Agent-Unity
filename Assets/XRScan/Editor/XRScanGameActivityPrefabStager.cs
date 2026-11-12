using System;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Keeps the GameActivity native build under the Windows 260-character path limit. With the
    /// GameActivity entry point, Gradle runs a CMake/ninja step that links the games-activity prefab
    /// (a static library plus headers) straight from the Gradle cache. Ninja 1.10, which ships with the
    /// SDK's CMake 3.22.1, fails with "Filename longer than 260 characters" on those paths once
    /// GRADLE_USER_HOME is longer than about 65 characters, as it can be on CI. This adds a few lines to
    /// the generated <c>GameActivity/CMakeLists.txt</c> that copy the library and headers into the
    /// CMake build dir, which Unity already keeps short (under <c>&lt;project&gt;/.utmp</c>), and point
    /// the imported target at the copy, so ninja only sees short paths on clean and incremental builds.
    /// </summary>
    public class XRScanGameActivityPrefabStager : IPostGenerateGradleAndroidProject
    {
        // After XRScanManifestPostProcessor (1) and XRScanNativeLibExcluder (2); they edit other files.
        public int callbackOrder => 3;

        private const string Anchor = "find_package(game-activity REQUIRED CONFIG)";
        private const string Marker = "# XRScan: GameActivity prefab staging";

        private static readonly string Snippet = string.Join("\n",
            "",
            Marker + " (XRScanGameActivityPrefabStager.cs): link a copy of the prefab in the short",
            "# CMake build dir, because ninja 1.10 cannot use the 260+ character Gradle cache path on Windows.",
            "if(TARGET game-activity::game-activity_static)",
            "    get_target_property(XRSCAN_GA_LIB game-activity::game-activity_static IMPORTED_LOCATION)",
            "    get_target_property(XRSCAN_GA_INC game-activity::game-activity_static INTERFACE_INCLUDE_DIRECTORIES)",
            "    if(EXISTS \"${XRSCAN_GA_LIB}\" AND IS_DIRECTORY \"${XRSCAN_GA_INC}\")",
            "        set(XRSCAN_GA_DIR \"${CMAKE_BINARY_DIR}/xrscan-ga\")",
            "        file(COPY \"${XRSCAN_GA_LIB}\" DESTINATION \"${XRSCAN_GA_DIR}/lib\")",
            "        file(COPY \"${XRSCAN_GA_INC}/\" DESTINATION \"${XRSCAN_GA_DIR}/include\")",
            "        get_filename_component(XRSCAN_GA_LIB_NAME \"${XRSCAN_GA_LIB}\" NAME)",
            "        set_target_properties(game-activity::game-activity_static PROPERTIES",
            "            IMPORTED_LOCATION \"${XRSCAN_GA_DIR}/lib/${XRSCAN_GA_LIB_NAME}\"",
            "            INTERFACE_INCLUDE_DIRECTORIES \"${XRSCAN_GA_DIR}/include\")",
            "    endif()",
            "endif()");

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // `path` is the unityLibrary module. Only the GameActivity entry point generates this file.
            var cmakeLists = Path.Combine(path, "src", "main", "cpp", "GameActivity", "CMakeLists.txt");
            if (!File.Exists(cmakeLists)) return;

            var text = File.ReadAllText(cmakeLists);
            if (text.Contains(Marker)) return; // idempotent (Unity regenerates the file each build anyway)

            int idx = text.IndexOf(Anchor, StringComparison.Ordinal);
            if (idx < 0)
            {
                Debug.LogWarning($"[XRScanGameActivityPrefabStager] `{Anchor}` not found in {cmakeLists}; skipped.");
                return;
            }

            File.WriteAllText(cmakeLists, text.Insert(idx + Anchor.Length, Snippet));
            Debug.Log("[XRScanGameActivityPrefabStager] GameActivity now links a copy of its prefab in the CMake build dir.");
        }
    }
}
