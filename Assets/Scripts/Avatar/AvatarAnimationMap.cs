using System;
using System.Collections.Generic;
using UnityEngine;

public enum AvatarGender
{
    Any = 0,
    Masculine = 1,
    Feminine = 2
}

public enum AvatarAnimCategory
{
    Idle = 0,
    Locomotion = 1,
    Dance = 2,
    Expression = 3
}

[Serializable]
public class AvatarAnimationEntry
{
    public AvatarAnimCategory category;
    public AvatarGender gender;
    public List<AnimationClip> clips = new List<AnimationClip>();
}

[CreateAssetMenu(menuName = "Avatar/Animation Map", fileName = "AvatarAnimationMap")]
public class AvatarAnimationMap : ScriptableObject
{
    public List<AvatarAnimationEntry> entries = new List<AvatarAnimationEntry>();

    public List<AnimationClip> GetClips(AvatarAnimCategory category, AvatarGender gender)
    {
        var exact = entries.Find(e => e.category == category && e.gender == gender);
        if (exact != null && exact.clips.Count > 0)
            return exact.clips;

        var any = entries.Find(e => e.category == category && e.gender == AvatarGender.Any);
        if (any != null && any.clips.Count > 0)
            return any.clips;

        return null;
    }

    public AnimationClip PickRandom(AvatarAnimCategory category, AvatarGender gender)
    {
        var list = GetClips(category, gender);
        if (list == null || list.Count == 0)
            return null;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }
}
