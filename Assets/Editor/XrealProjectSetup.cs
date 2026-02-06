using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using Unity.XR.XREAL;

public static class XrealProjectSetup
{
    const string LoaderTypeName = "Unity.XR.XREAL.XREALXRLoader";
    const string SettingsAssetPath = "Assets/Settings/XREALSettings.asset";

    [MenuItem("Tools/XREAL/Configure Android XR")]
    public static void ConfigureAndroidXr()
    {
        EnsureAndroidBuildTarget();
        EnsureXrManagementForAndroid();
        EnsureXrealSettingsAsset();

        Debug.Log("[XREAL] Android build target + XR settings configured.");
    }

    static void EnsureAndroidBuildTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            return;

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
    }

    static void EnsureXrManagementForAndroid()
    {
        var generalSettings = GetOrCreateGeneralSettings();

        if (!generalSettings.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            generalSettings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);

        if (!generalSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            generalSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

        var androidSettings = generalSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
        if (androidSettings != null)
        {
            androidSettings.InitManagerOnStart = true;
            EditorUtility.SetDirty(androidSettings);
        }

        var manager = generalSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        if (manager != null && !XRPackageMetadataStore.IsLoaderAssigned(LoaderTypeName, BuildTargetGroup.Android))
        {
            if (!XRPackageMetadataStore.AssignLoader(manager, LoaderTypeName, BuildTargetGroup.Android))
                Debug.LogWarning("[XREAL] Failed to assign XREAL XR Loader for Android.");
        }

        AssetDatabase.SaveAssets();
    }

    static XRGeneralSettingsPerBuildTarget GetOrCreateGeneralSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget existing) && existing != null)
            return existing;

        var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            existing = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (existing != null)
            {
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, existing, true);
                return existing;
            }
        }

        const string settingsDir = "Assets/Settings";
        Directory.CreateDirectory(settingsDir);
        var assetPath = Path.Combine(settingsDir, "XRGeneralSettingsPerBuildTarget.asset");
        var created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
        AssetDatabase.CreateAsset(created, assetPath);
        AssetDatabase.SaveAssets();
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);
        return created;
    }

    static void EnsureXrealSettingsAsset()
    {
        var settings = AssetDatabase.LoadAssetAtPath<XREALSettings>(SettingsAssetPath);
        if (settings == null)
        {
            var dir = Path.GetDirectoryName(SettingsAssetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            settings = ScriptableObject.CreateInstance<XREALSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
        }

        EditorBuildSettings.AddConfigObject(XREALSettings.k_SettingsKey, settings, true);
        EditorUtility.SetDirty(settings);
    }
}
