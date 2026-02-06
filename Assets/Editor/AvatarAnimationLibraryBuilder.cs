using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AvatarAnimationLibraryBuilder
{
    const string DefaultLibraryRoot = "Assets/ThirdParty/readyplayerme-animation-library";
    const string AlternateLibraryRoot = "Assets/AnimationLibrary";
    const string OutputPath = "Assets/Resources/AvatarAnimationLibrary.asset";

    [MenuItem("Tools/Avatar/Build Animation Library")]
    public static void Build()
    {
        var root = DefaultLibraryRoot;
        if (!Directory.Exists(root) && Directory.Exists(AlternateLibraryRoot))
            root = AlternateLibraryRoot;

        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"[AvatarAnimationLibraryBuilder] Missing folder: {DefaultLibraryRoot} (or {AlternateLibraryRoot})");
            return;
        }

        var clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { root });
        var clips = clipGuids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p))
            .Where(c => c != null)
            .Distinct()
            .ToList();

        if (clips.Count == 0)
        {
            Debug.LogWarning("[AvatarAnimationLibraryBuilder] No AnimationClips found.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Assets/Resources");

        var existing = AssetDatabase.LoadAssetAtPath<AvatarAnimationLibrary>(OutputPath);
        if (existing == null)
        {
            existing = ScriptableObject.CreateInstance<AvatarAnimationLibrary>();
            AssetDatabase.CreateAsset(existing, OutputPath);
        }

        existing.clips = clips;
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AvatarAnimationLibraryBuilder] Built library with {clips.Count} clips at {OutputPath}");
    }
}
