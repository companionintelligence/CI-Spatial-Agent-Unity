using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using ReadyPlayerMe.Core;
using UnityEngine;

public class AvatarBootstrap : MonoBehaviour
{
    [Header("Avatar Source")]
    [SerializeField] string avatarsFolder = "Avatars";
    [SerializeField] int avatarIndex = 0;

    [Header("Placement")]
    [SerializeField] float spawnDistance = 2.0f;
    [SerializeField] float scale = 1.0f;

    [Header("Motion")]
    [SerializeField] float walkRadius = 1.5f;
    [SerializeField] float walkSpeed = 0.6f;

    [Header("Ready Player Me")]
    [SerializeField] bool useReadyPlayerMe = true;
    [SerializeField] string overrideAvatarUrl = "";
    [SerializeField] bool useXrSkeleton = true;

    static AvatarBootstrap s_Instance;
    string[] _avatarPaths = Array.Empty<string>();
    GameObject _currentAvatar;
    bool _isLoading;
    AvatarObjectLoader _rpmLoader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_Instance != null)
            return;

        var go = new GameObject("AvatarBootstrap");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<AvatarBootstrap>();
    }

    async void Start()
    {
        try
        {
            RefreshAvatarList();
            await LoadAvatarByIndexAsync(avatarIndex);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public void LoadAvatarByIndex(int index)
    {
        if (_isLoading)
            return;
        _ = LoadAvatarByIndexAsync(index);
    }

    public string[] GetAvatarPaths()
    {
        return _avatarPaths ?? Array.Empty<string>();
    }

    string ResolveAvatarFolder()
    {
        // Prefer explicit env override if set
        var envPath = Environment.GetEnvironmentVariable("CI_AVATAR_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        // If the original Google Drive path exists, use it in Editor
#if UNITY_EDITOR
        var assetsAvatars = Path.Combine(Application.dataPath, "Avatars");
        if (Directory.Exists(assetsAvatars))
            return assetsAvatars;

        var drivePath = "/Users/liam/Library/CloudStorage/GoogleDrive-liam@lifescope.io/My Drive/CI Main Drive/AVATAR/20250301 - Embodied Avatars Geometry Art/Avatars";
        if (Directory.Exists(drivePath))
            return drivePath;
#endif

        return Path.Combine(Application.streamingAssetsPath, avatarsFolder);
    }

    void RefreshAvatarList()
    {
        string folder = ResolveAvatarFolder();
        if (!Directory.Exists(folder))
        {
            Debug.LogWarning($"[AvatarBootstrap] Missing avatar folder: {folder}");
            _avatarPaths = Array.Empty<string>();
            return;
        }

        _avatarPaths = Directory.GetFiles(folder, "*.glb").OrderBy(p => p).ToArray();
        if (_avatarPaths.Length == 0)
        {
            Debug.LogWarning($"[AvatarBootstrap] No .glb files found in: {folder}");
            return;
        }
    }

    async Task LoadAvatarByIndexAsync(int index)
    {
        if (_avatarPaths == null || _avatarPaths.Length == 0)
            return;

        _isLoading = true;
        avatarIndex = Mathf.Clamp(index, 0, _avatarPaths.Length - 1);
        string glbPath = _avatarPaths[avatarIndex];
        string uri = glbPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            ? glbPath
            : "file://" + glbPath;

        var rpmUrl = string.IsNullOrEmpty(overrideAvatarUrl) ? uri : overrideAvatarUrl;
        var inferredGender = InferGenderFromPath(glbPath);
        if (useReadyPlayerMe && File.Exists(glbPath))
            EnsureLocalMetadata(glbPath, inferredGender);
        if (useReadyPlayerMe)
        {
            bool loaded = await TryLoadWithReadyPlayerMeAsync(rpmUrl, glbPath);
            if (loaded)
            {
                _isLoading = false;
                return;
            }
        }

        var gltf = new GltfImport();
        bool success = await gltf.Load(uri);
        if (!success)
        {
            Debug.LogError($"[AvatarBootstrap] Failed to load GLB: {glbPath}");
            _isLoading = false;
            return;
        }

        if (_currentAvatar != null)
            Destroy(_currentAvatar);

        var root = new GameObject(Path.GetFileNameWithoutExtension(glbPath));
        root.transform.position = GetSpawnPosition();
        root.transform.localScale = Vector3.one * scale;

        await gltf.InstantiateMainSceneAsync(root.transform);

        ApplyAvatarBehaviors(root, glbPath, inferredGender);
        LogRigStatus(root, "glTFast");
        TryPlayFirstAnimation(root);
        _currentAvatar = root;
        _isLoading = false;
    }

    async Task<bool> TryLoadWithReadyPlayerMeAsync(string url, string sourcePath)
    {
        try
        {
            AvatarGender rpmGender = AvatarGender.Any;
            var tcs = new TaskCompletionSource<GameObject>();
            _rpmLoader = new AvatarObjectLoader();
            _rpmLoader.OnCompleted += (sender, args) =>
            {
                rpmGender = TryExtractRpmGender(args);
                tcs.TrySetResult(args.Avatar);
            };
            _rpmLoader.OnFailed += (sender, args) =>
            {
                tcs.TrySetException(new Exception(args.ToString()));
            };

            _rpmLoader.LoadAvatar(url);
            var avatar = await tcs.Task;
            if (avatar == null)
                return false;

            if (_currentAvatar != null)
                Destroy(_currentAvatar);

            var root = new GameObject(Path.GetFileNameWithoutExtension(sourcePath));
            root.transform.position = GetSpawnPosition();
            root.transform.localScale = Vector3.one * scale;

            avatar.transform.SetParent(root.transform, false);
            ApplyAvatarBehaviors(root, sourcePath, rpmGender);
            LogRigStatus(root, "RPM");
            _currentAvatar = root;
            Debug.Log($"[AvatarBootstrap] RPM loaded avatar: {url} (gender={rpmGender})");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AvatarBootstrap] RPM loader failed: {ex.Message}");
            return false;
        }
    }

    void ApplyAvatarBehaviors(GameObject root, string sourcePath, AvatarGender overrideGender)
    {
        var walker = root.AddComponent<AvatarWalker>();
        walker.Radius = walkRadius;
        walker.Speed = walkSpeed;

        var library = Resources.Load<AvatarAnimationLibrary>("AvatarAnimationLibrary");
        var map = Resources.Load<AvatarAnimationMap>("AvatarAnimationMap");
        if (library != null)
        {
            var mixer = root.AddComponent<AvatarAnimationMixer>();
            var gender = overrideGender != AvatarGender.Any ? overrideGender : InferGenderFromPath(sourcePath);
            mixer.Initialize(library, map, gender);
            if (library.clips == null || library.clips.Count == 0)
                Debug.LogWarning("[AvatarBootstrap] Animation library is empty. Run Tools > Avatar > Setup RPM Animation Library after importing animations.");
        }
        else
        {
            Debug.LogWarning("[AvatarBootstrap] Missing AvatarAnimationLibrary asset. Run Tools > Avatar > Setup RPM Animation Library after importing animations.");
        }
    }

    static AvatarGender TryExtractRpmGender(object args)
    {
        if (args == null)
            return AvatarGender.Any;

        var metaProp = args.GetType().GetProperty("Metadata");
        if (metaProp == null)
            return AvatarGender.Any;
        var meta = metaProp.GetValue(args);
        if (meta == null)
            return AvatarGender.Any;

        var genderProp = meta.GetType().GetProperty("OutfitGender");
        if (genderProp == null)
            return AvatarGender.Any;
        var genderValue = genderProp.GetValue(meta);
        if (genderValue == null)
            return AvatarGender.Any;

        var text = genderValue.ToString().ToLowerInvariant();
        if (text.Contains("masc"))
            return AvatarGender.Masculine;
        if (text.Contains("fem"))
            return AvatarGender.Feminine;
        return AvatarGender.Any;
    }

    static void TryPlayFirstAnimation(GameObject root)
    {
        var animation = root.GetComponentInChildren<Animation>();
        if (animation != null)
        {
            foreach (AnimationState state in animation)
            {
                animation.Play(state.name);
                return;
            }
        }

        var animator = root.GetComponentInChildren<Animator>();
        if (animator != null)
            animator.Play(0);
    }

    static void LogRigStatus(GameObject root, string source)
    {
        var animator = root.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"[AvatarBootstrap] {source} avatar has no Animator component.");
            return;
        }

        var avatar = animator.avatar;
        if (avatar == null)
        {
            Debug.LogWarning($"[AvatarBootstrap] {source} avatar Animator has no Avatar assigned.");
            return;
        }

        Debug.Log($"[AvatarBootstrap] {source} avatar rig: isHuman={avatar.isHuman} isValid={avatar.isValid}");
    }

    Vector3 GetSpawnPosition()
    {
        var cam = Camera.main;
        if (cam == null)
            return new Vector3(0f, 0f, spawnDistance);

        var forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        var basePos = cam.transform.position + forward * spawnDistance;
        return basePos;
    }

    static AvatarGender InferGenderFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return AvatarGender.Any;

        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

        if (name.StartsWith("f_"))
            return AvatarGender.Feminine;
        if (name.StartsWith("m_"))
            return AvatarGender.Masculine;

        if (name.Contains("feminine") || name.Contains("female") || name.Contains("woman") || name.Contains("_f") || name.Contains("girl"))
            return AvatarGender.Feminine;
        if (name.Contains("masculine") || name.Contains("male") || name.Contains("man") || name.Contains("_m") || name.Contains("boy"))
            return AvatarGender.Masculine;

        return AvatarGender.Any;
    }

    void EnsureLocalMetadata(string glbPath, AvatarGender gender)
    {
        try
        {
            var jsonPath = Path.ChangeExtension(glbPath, ".json");
            if (string.IsNullOrEmpty(jsonPath) || File.Exists(jsonPath))
                return;

            var outfitGender = gender switch
            {
                AvatarGender.Masculine => "masculine",
                AvatarGender.Feminine => "feminine",
                _ => "neutral"
            };
            var bodyType = useXrSkeleton ? "fullbody-xr" : "fullbody";
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

            var json = $"{{\"bodyType\":\"{bodyType}\",\"outfitGender\":\"{outfitGender}\",\"updatedAt\":\"{timestamp}\",\"skinTone\":\"\"}}";
            File.WriteAllText(jsonPath, json);
            Debug.Log($"[AvatarBootstrap] Wrote local metadata for RPM loader: {jsonPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AvatarBootstrap] Failed to write local metadata: {ex.Message}");
        }
    }
}
