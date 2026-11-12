using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Fails any Android build other than an explicit Pico build (<c>XRSCAN_PICO</c>) while PICO SDK
    /// content is in the project. Build callbacks can drop PICO's assemblies, native libraries and Java
    /// plugins, but not the assets in its <c>Resources</c> folders (controller and hand models, textures,
    /// shaders, prefabs), the OpenXR feature objects it owns, or the settings files its editor code writes
    /// to <c>Assets/Resources</c>; Unity packs all of those into every player. The PICO SDK licence forbids
    /// reproducing that material without PICO's written permission, so a Quest APK must be built from a
    /// project without the package: <b>XRScan &gt; Build &gt; Quest 3</b> does that
    /// (<see cref="XRScanBuildPackages"/>). Builds made another way, e.g. from the Build Profiles window,
    /// stop here instead of shipping PICO's material.
    /// </summary>
    public class XRScanVendorPackageGuard : IPreprocessBuildWithReport
    {
        // Before every other callback, so a refused build has no side effects.
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android || XRScanBuilder.BuildingForPico()) return;

            var problems = new List<string>();
            if (XRScanBuildPackages.IsRegistered(XRScanBuildPackages.PicoPackage))
                problems.Add($"the PICO OpenXR SDK ({XRScanBuildPackages.PicoPackage}) is installed");
            problems.AddRange(XRScanBuildPackages.PicoGeneratedAssets.Where(File.Exists)
                .Select(p => $"{p} (written by the PICO SDK) is in a Resources folder"));

            if (problems.Count > 0)
            {
                throw new BuildFailedException(
                    "[XRScanVendorPackageGuard] This is not a Pico build, but " + string.Join(" and ", problems) +
                    ". Unity would ship PICO's material in the APK, which the PICO SDK licence does not allow. " +
                    "Build with XRScan > Build > Quest 3 (or XRScan.EditorTools.XRScanBuilder.BuildQuest), which takes " +
                    "the PICO SDK out of the project for the build, or add XRSCAN_PICO for a Pico build.");
            }

            foreach (var package in XRScanBuildPackages.EditorOnlyPackages.Where(XRScanBuildPackages.IsRegistered))
            {
                Debug.LogWarning($"[XRScanVendorPackageGuard] {package} is installed; XRScanPlayerAssemblyFilter keeps its " +
                                 "assemblies out of the player, but XRScan > Build removes the package for the build entirely.");
            }
        }
    }
}
