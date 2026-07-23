using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AvatarAnimationMapBuilder
{
    const string DefaultLibraryRoot = "Assets/ThirdParty/readyplayerme-animation-library";
    const string LibraryAssetPath = "Assets/Resources/AvatarAnimationLibrary.asset";
    const string MapAssetPath = "Assets/Resources/AvatarAnimationMap.asset";

    static readonly string[] IdleTokens = { "idle", "stand", "breath", "pose", "rest", "waiting", "relax" };
    static readonly string[] LocomotionTokens = { "walk", "run", "jog", "sprint", "locomotion", "stride" };
    static readonly string[] DanceTokens = { "dance", "dancing", "hiphop", "shuffle", "salsa", "groove", "twerk" };
    static readonly string[] ExpressionTokens = { "talk", "speak", "gesture", "emote", "expression", "nod", "shake", "wave", "point", "clap", "cheer", "laugh", "cry", "smile", "angry", "sad", "happy" };

    static readonly string[] FeminineTokens = { "female", "woman", "fem", "_f", "girl" };
    static readonly string[] MasculineTokens = { "male", "man", "masc", "_m", "boy" };

    static readonly string[] IdlePathTokens = { "/idle/" };
    static readonly string[] LocomotionPathTokens = { "/locomotion/", "/walk/", "/run/" };
    static readonly string[] DancePathTokens = { "/dance/" };
    static readonly string[] ExpressionPathTokens = { "/expression/", "/emote/", "/gesture/" };

    static readonly string[] FemininePathTokens = { "/feminine/", "/female/" };
    static readonly string[] MasculinePathTokens = { "/masculine/", "/male/" };

    [MenuItem("Tools/Avatar/Build Animation Map")]
    public static void Build()
    {
        var library = AssetDatabase.LoadAssetAtPath<AvatarAnimationLibrary>(LibraryAssetPath);
        if (library == null)
        {
            Debug.LogWarning("[AvatarAnimationMapBuilder] Missing AvatarAnimationLibrary. Run 'Build Animation Library' first.");
            return;
        }

        var map = AssetDatabase.LoadAssetAtPath<AvatarAnimationMap>(MapAssetPath);
        if (map == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MapAssetPath) ?? "Assets/Resources");
            map = ScriptableObject.CreateInstance<AvatarAnimationMap>();
            AssetDatabase.CreateAsset(map, MapAssetPath);
        }

        map.entries = BuildEntries(library.clips);
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AvatarAnimationMapBuilder] Built animation map with {map.entries.Count} entries at {MapAssetPath}");
    }

    static List<AvatarAnimationEntry> BuildEntries(List<AnimationClip> clips)
    {
        var entries = new Dictionary<(AvatarAnimCategory, AvatarGender), AvatarAnimationEntry>();

        foreach (var clip in clips)
        {
            if (clip == null) continue;
            var name = clip.name.ToLowerInvariant();
            var path = AssetDatabase.GetAssetPath(clip).ToLowerInvariant();

            var category = ClassifyCategory(path, name);
            var gender = ClassifyGender(path, name);

            var key = (category, gender);
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new AvatarAnimationEntry { category = category, gender = gender, clips = new List<AnimationClip>() };
                entries[key] = entry;
            }
            entry.clips.Add(clip);
        }

        EnsureAllCombinations(entries);
        return entries.Values
            .OrderBy(e => e.category)
            .ThenBy(e => e.gender)
            .ToList();
    }

    static void EnsureAllCombinations(Dictionary<(AvatarAnimCategory, AvatarGender), AvatarAnimationEntry> entries)
    {
        var categories = (AvatarAnimCategory[])Enum.GetValues(typeof(AvatarAnimCategory));
        var genders = new[] { AvatarGender.Masculine, AvatarGender.Feminine, AvatarGender.Any };

        foreach (var category in categories)
        {
            foreach (var gender in genders)
            {
                var key = (category, gender);
                if (!entries.ContainsKey(key))
                    entries[key] = new AvatarAnimationEntry { category = category, gender = gender, clips = new List<AnimationClip>() };
            }
        }
    }

    static AvatarAnimCategory ClassifyCategory(string path, string name)
    {
        if (ContainsAny(path, LocomotionPathTokens)) return AvatarAnimCategory.Locomotion;
        if (ContainsAny(path, IdlePathTokens)) return AvatarAnimCategory.Idle;
        if (ContainsAny(path, DancePathTokens)) return AvatarAnimCategory.Dance;
        if (ContainsAny(path, ExpressionPathTokens)) return AvatarAnimCategory.Expression;

        if (ContainsAny(name, LocomotionTokens)) return AvatarAnimCategory.Locomotion;
        if (ContainsAny(name, IdleTokens)) return AvatarAnimCategory.Idle;
        if (ContainsAny(name, DanceTokens)) return AvatarAnimCategory.Dance;
        if (ContainsAny(name, ExpressionTokens)) return AvatarAnimCategory.Expression;
        return AvatarAnimCategory.Expression;
    }

    static AvatarGender ClassifyGender(string path, string name)
    {
        if (name.StartsWith("f_")) return AvatarGender.Feminine;
        if (name.StartsWith("m_")) return AvatarGender.Masculine;

        if (ContainsAny(path, FemininePathTokens)) return AvatarGender.Feminine;
        if (ContainsAny(path, MasculinePathTokens)) return AvatarGender.Masculine;

        if (ContainsAny(name, FeminineTokens)) return AvatarGender.Feminine;
        if (ContainsAny(name, MasculineTokens)) return AvatarGender.Masculine;
        return AvatarGender.Any;
    }

    static bool ContainsAny(string text, string[] tokens)
    {
        foreach (var t in tokens)
            if (text.Contains(t))
                return true;
        return false;
    }
}
