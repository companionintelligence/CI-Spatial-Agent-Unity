using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RpmAnimationImportConfigurator
{
    const string DefaultLibraryRoot = "Assets/ThirdParty/readyplayerme-animation-library";
    const string AlternateLibraryRoot = "Assets/AnimationLibrary";

    static readonly string[] MasculineTokens = { "masculine", "male", "man", "_m", "/m_", "m_" };
    static readonly string[] FeminineTokens = { "feminine", "female", "woman", "_f", "/f_", "f_" };

    static readonly string[] LoopTokens = { "idle", "walk", "run", "jog", "sprint", "locomotion", "dance" };

    [MenuItem("Tools/Avatar/Configure RPM Animation Imports")]
    public static void Configure()
    {
        var root = Directory.Exists(DefaultLibraryRoot) ? DefaultLibraryRoot : AlternateLibraryRoot;
        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"[RPM] Missing animation library folder: {DefaultLibraryRoot} (or {AlternateLibraryRoot})");
            return;
        }

        var masculineTpose = FindTposeAvatar(root, AvatarGender.Masculine);
        var feminineTpose = FindTposeAvatar(root, AvatarGender.Feminine);

        if (masculineTpose == null && feminineTpose != null)
            masculineTpose = feminineTpose;
        if (feminineTpose == null && masculineTpose != null)
            feminineTpose = masculineTpose;

        if (masculineTpose == null || feminineTpose == null)
        {
            Debug.LogWarning("[RPM] Missing TPose FBX files. Expected gendered TPose FBX(s).");
            return;
        }

        Debug.Log($"[RPM] Using TPose: Masculine='{masculineTpose}', Feminine='{feminineTpose}'");

        var masculineAvatar = EnsureHumanoidAvatar(masculineTpose);
        var feminineAvatar = EnsureHumanoidAvatar(feminineTpose);

        if (masculineAvatar == null || feminineAvatar == null)
        {
            Debug.LogWarning("[RPM] Failed to create Humanoid Avatars from TPose files.");
            return;
        }

        var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { root });
        int updated = 0;
        foreach (var guid in modelGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(path, masculineTpose, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, feminineTpose, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = GetExternalAvatarSetup();
            importer.sourceAvatar = IsFeminine(path) ? feminineAvatar : masculineAvatar;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i];
                    var name = clip.name.ToLowerInvariant();
                    clip.loopTime = ContainsAny(name, LoopTokens);
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clips[i] = clip;
                }
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            updated++;
        }

        Debug.Log($"[RPM] Configured {updated} animation FBX imports under {root}.");
    }

    [MenuItem("Tools/Avatar/Log ModelImporterAvatarSetup")]
    public static void LogModelImporterAvatarSetup()
    {
        var names = Enum.GetNames(typeof(ModelImporterAvatarSetup));
        Debug.Log($"[RPM] ModelImporterAvatarSetup values: {string.Join(", ", names)}");
    }

    static string FindTposeAvatar(string root, AvatarGender gender)
    {
        var guids = AssetDatabase.FindAssets("t:Model", new[] { root });
        string genderToken = gender == AvatarGender.Masculine ? "m" : "f";

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!IsTposeName(name))
                continue;

            if (gender == AvatarGender.Masculine && !LooksMasculine(path))
                continue;
            if (gender == AvatarGender.Feminine && !LooksFeminine(path))
                continue;

            return path;
        }

        return null;
    }

    static bool IsTposeName(string name)
    {
        return name.Contains("tpose") || name.Contains("t-pose") || name.Contains("t_pose");
    }

    static bool LooksMasculine(string path)
    {
        var text = path.ToLowerInvariant();
        return ContainsAny(text, MasculineTokens);
    }

    static bool LooksFeminine(string path)
    {
        var text = path.ToLowerInvariant();
        return ContainsAny(text, FeminineTokens);
    }

    static Avatar EnsureHumanoidAvatar(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return null;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();

        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        return assets.OfType<Avatar>().FirstOrDefault(a => a != null && a.isHuman);
    }

    static bool IsFeminine(string path)
    {
        var text = path.ToLowerInvariant();
        return ContainsAny(text, FeminineTokens);
    }

    static bool ContainsAny(string text, string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (text.Contains(token))
                return true;
        }
        return false;
    }

    static ModelImporterAvatarSetup GetExternalAvatarSetup()
    {
        var type = typeof(ModelImporterAvatarSetup);
        foreach (var name in Enum.GetNames(type))
        {
            if (string.Equals(name, "CopyFromOtherAvatar", StringComparison.OrdinalIgnoreCase))
                return (ModelImporterAvatarSetup)Enum.Parse(type, name);
            if (string.Equals(name, "CopyFromOther", StringComparison.OrdinalIgnoreCase))
                return (ModelImporterAvatarSetup)Enum.Parse(type, name);
            if (string.Equals(name, "UseExternalAvatar", StringComparison.OrdinalIgnoreCase))
                return (ModelImporterAvatarSetup)Enum.Parse(type, name);
        }

        return ModelImporterAvatarSetup.CreateFromThisModel;
    }
}
