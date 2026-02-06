using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Avatar/Animation Library", fileName = "AvatarAnimationLibrary")]
public class AvatarAnimationLibrary : ScriptableObject
{
    public List<AnimationClip> clips = new List<AnimationClip>();

    public AnimationClip FindByNameContains(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        token = token.ToLowerInvariant();
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            if (clip.name.ToLowerInvariant().Contains(token))
                return clip;
        }
        return null;
    }
}
