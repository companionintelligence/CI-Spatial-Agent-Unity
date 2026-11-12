using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Second line of defence that keeps managed assemblies which must not be redistributed out of
    /// player builds. <see cref="XRScanBuilder"/> already builds each APK from a project without those
    /// packages (<see cref="XRScanBuildPackages"/>), so in its builds this finds nothing to drop; it
    /// matters for builds made another way while the packages are installed. It only filters
    /// assemblies: the packages' <c>Resources</c>, serialized objects and script names still ship, which
    /// is why <see cref="XRScanVendorPackageGuard"/> refuses a non-Pico Android build with the PICO SDK
    /// installed.
    /// <list type="bullet">
    /// <item><b>Every player build:</b> <c>com.coplaydev.unity-mcp</c> (MCP for Unity). It is
    /// edit-time tooling; its <c>MCPForUnity.Runtime</c> assembly is only used by its Editor bridge.</item>
    /// <item><b>Every build except an explicit Pico build</b> (<c>XRSCAN_PICO</c>):
    /// <c>com.unity.xr.openxr.picoxr</c>. The PICO SDK licence forbids reproduction without written
    /// permission from PICO.</item>
    /// </list>
    /// Excluded assemblies are matched by the package that owns their asmdef (or precompiled DLL),
    /// so new assemblies added to those packages are covered automatically.
    /// </summary>
    public class XRScanPlayerAssemblyFilter : IFilterBuildAssemblies
    {
        /// <summary>Packages whose assemblies never ship in a player (edit-time tooling).</summary>
        internal static readonly string[] EditorOnlyPackages = XRScanBuildPackages.EditorOnlyPackages;

        /// <summary>Packages whose assemblies ship only in a Pico build.</summary>
        internal static readonly string[] PicoOnlyPackages = XRScanBuildPackages.PicoOnlyPackages;

        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            var excludedPackages = ExcludedPackagesForActiveBuild();
            var excludedNames = AssemblyNamesOwnedBy(excludedPackages);

            var kept = new List<string>(assemblies.Length);
            var removed = new List<string>();
            foreach (var path in assemblies)
            {
                if (excludedNames.Contains(Path.GetFileNameWithoutExtension(path))) removed.Add(Path.GetFileName(path));
                else kept.Add(path);
            }

            Debug.Log(removed.Count > 0
                ? $"[XRScanPlayerAssemblyFilter] Excluded {removed.Count} assembl{(removed.Count == 1 ? "y" : "ies")} " +
                  $"from the {EditorUserBuildSettings.activeBuildTarget} player ({string.Join(", ", excludedPackages)}): " +
                  string.Join(", ", removed.OrderBy(n => n, StringComparer.Ordinal))
                : $"[XRScanPlayerAssemblyFilter] Nothing to exclude (looked for {string.Join(", ", excludedPackages)}).");
            return kept.ToArray();
        }

        /// <summary>The packages to keep out of the player for the build currently being made.</summary>
        internal static IReadOnlyList<string> ExcludedPackagesForActiveBuild()
        {
            var packages = new List<string>(EditorOnlyPackages);
            bool picoBuild = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android && XRScanBuilder.BuildingForPico();
            if (!picoBuild) packages.AddRange(PicoOnlyPackages);
            return packages;
        }

        /// <summary>
        /// Names (no extension) of the player assemblies whose asmdef or precompiled DLL lives in one
        /// of <paramref name="packageNames"/>.
        /// </summary>
        internal static HashSet<string> AssemblyNamesOwnedBy(IEnumerable<string> packageNames)
        {
            var wanted = new HashSet<string>(packageNames, StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                var asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                if (string.IsNullOrEmpty(asmdef)) continue;
                var owner = PackageInfo.FindForAssetPath(asmdef);
                if (owner != null && wanted.Contains(owner.name)) names.Add(assembly.name);
            }

            // Precompiled managed DLLs shipped inside those packages (none today, but stay covered).
            var roots = PackageInfo.GetAllRegisteredPackages()
                .Where(p => wanted.Contains(p.name) && !string.IsNullOrEmpty(p.resolvedPath))
                .Select(p => Path.GetFullPath(p.resolvedPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
                .ToArray();
            foreach (var dll in CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UserAssembly))
            {
                var full = Path.GetFullPath(dll);
                if (roots.Any(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                    names.Add(Path.GetFileNameWithoutExtension(full));
            }

            return names;
        }

        /// <summary>
        /// Diagnostic: logs which player assemblies each Android build would leave out. Menu:
        /// <b>XRScan &gt; Build &gt; Dump excluded player assemblies</b>.
        /// </summary>
        [MenuItem("XRScan/Build/Dump excluded player assemblies", false, 22)]
        public static void DumpExcludedAssemblies()
        {
            var editorOnly = AssemblyNamesOwnedBy(EditorOnlyPackages);
            var picoOnly = AssemblyNamesOwnedBy(PicoOnlyPackages);
            Debug.Log("[XRScanPlayerAssemblyFilter] Never in a player: " + string.Join(", ", editorOnly.OrderBy(n => n)) +
                      "\n[XRScanPlayerAssemblyFilter] Only in a Pico build: " + string.Join(", ", picoOnly.OrderBy(n => n)));
        }
    }
}
