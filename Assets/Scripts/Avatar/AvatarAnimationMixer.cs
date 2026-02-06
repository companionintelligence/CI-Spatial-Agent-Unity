using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class AvatarAnimationMixer : MonoBehaviour
{
    [SerializeField] AvatarAnimationLibrary library;
    [SerializeField] AvatarAnimationMap map;
    [SerializeField] AvatarGender gender = AvatarGender.Any;
    [SerializeField] string idleToken = "idle";
    [SerializeField] string walkToken = "walk";

    PlayableGraph _graph;
    AnimationMixerPlayable _mixer;
    AnimationPlayableOutput _output;
    Dictionary<AnimationClip, int> _clipIndex = new Dictionary<AnimationClip, int>();
    int _currentIndex = -1;

    public void Initialize(AvatarAnimationLibrary lib, AvatarAnimationMap animMap = null, AvatarGender g = AvatarGender.Any)
    {
        library = lib;
        map = animMap;
        gender = g;
        BuildGraph();
        PlayDefault();
    }

    void OnDisable()
    {
        if (_graph.IsValid())
            _graph.Destroy();
    }

    void BuildGraph()
    {
        if (library == null || library.clips == null || library.clips.Count == 0)
        {
            Debug.LogWarning("[AvatarAnimationMixer] Missing animation clips. Did you build the AvatarAnimationLibrary?");
            return;
        }

        if (_graph.IsValid())
            _graph.Destroy();

        var animator = GetComponentInChildren<Animator>();
        if (animator == null)
            animator = gameObject.AddComponent<Animator>();
        animator.applyRootMotion = false;

        _graph = PlayableGraph.Create("AvatarAnimationMixer");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        _mixer = AnimationMixerPlayable.Create(_graph, library.clips.Count, true);
        _output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
        _output.SetSourcePlayable(_mixer);

        _clipIndex.Clear();
        for (int i = 0; i < library.clips.Count; i++)
        {
            var clip = library.clips[i];
            if (clip == null)
                continue;
            var playable = AnimationClipPlayable.Create(_graph, clip);
            _graph.Connect(playable, 0, _mixer, i);
            _mixer.SetInputWeight(i, 0f);
            _clipIndex[clip] = i;
        }

        _graph.Play();
    }

    public void PlayDefault()
    {
        if (map != null)
        {
            var locomotion = map.PickRandom(AvatarAnimCategory.Locomotion, gender);
            if (locomotion != null)
            {
                PlayClip(locomotion);
                return;
            }
        }

        if (library != null)
        {
            var walk = library.FindByNameContains(walkToken);
            var idle = library.FindByNameContains(idleToken);

            if (walk != null)
                PlayClip(walk);
            else if (idle != null)
                PlayClip(idle);
            else if (library.clips.Count > 0)
                PlayClip(library.clips[0]);
        }
    }

    public void PlayCategory(AvatarAnimCategory category)
    {
        if (map == null)
            return;
        var clip = map.PickRandom(category, gender);
        if (clip != null)
            PlayClip(clip);
    }

    public void PlayByName(string name)
    {
        if (library == null || library.clips == null)
            return;

        var clip = library.clips.Find(c => c != null && c.name == name);
        if (clip != null)
            PlayClip(clip);
    }

    public void PlayClip(AnimationClip clip)
    {
        if (clip == null || !_graph.IsValid())
            return;

        if (!_clipIndex.TryGetValue(clip, out int index))
            return;

        if (_currentIndex == index)
            return;

        for (int i = 0; i < _mixer.GetInputCount(); i++)
            _mixer.SetInputWeight(i, i == index ? 1f : 0f);

        _currentIndex = index;
    }
}
