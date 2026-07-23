using System.IO;
using System.Linq;
using UnityEngine;

public class AvatarGalleryUI : MonoBehaviour
{
    [SerializeField] string avatarsFolder = "Avatars";
    [SerializeField] int thumbnailSize = 96;
    [SerializeField] int labelWidth = 220;

    string[] _avatarPaths = new string[0];
    Texture2D[] _thumbnails = new Texture2D[0];
    Vector2 _scroll;
    AvatarBootstrap _bootstrap;

    static AvatarGalleryUI s_Instance;
    public static bool SuppressOnGui;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_Instance != null)
            return;

        var go = new GameObject("AvatarGalleryUI");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<AvatarGalleryUI>();
    }

    void Start()
    {
        _bootstrap = FindObjectOfType<AvatarBootstrap>();
        BuildIndex();
    }

    void BuildIndex()
    {
        string folder = ResolveAvatarFolder();
        if (!Directory.Exists(folder))
            return;

        _avatarPaths = Directory.GetFiles(folder, "*.glb").OrderBy(p => p).ToArray();
        var thumbPaths = Directory.GetFiles(folder, "*.png");

        _thumbnails = new Texture2D[_avatarPaths.Length];
        for (int i = 0; i < _avatarPaths.Length; i++)
        {
            string glbName = Path.GetFileNameWithoutExtension(_avatarPaths[i]).ToLowerInvariant();
            string match = thumbPaths.FirstOrDefault(p =>
                glbName.Contains(Path.GetFileNameWithoutExtension(p).ToLowerInvariant()) ||
                Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(glbName));

            if (string.IsNullOrEmpty(match))
                continue;

            _thumbnails[i] = LoadThumbnail(match);
        }
    }

    string ResolveAvatarFolder()
    {
        var envPath = System.Environment.GetEnvironmentVariable("CI_AVATAR_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

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

    static Texture2D LoadThumbnail(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);
        tex.name = Path.GetFileNameWithoutExtension(path);
        return tex;
    }

    void OnGUI()
    {
        if (SuppressOnGui)
            return;
        if (_avatarPaths == null || _avatarPaths.Length == 0)
            return;

        const int padding = 10;
        GUILayout.BeginArea(new Rect(padding, padding, thumbnailSize + labelWidth + 40, Screen.height - padding * 2), "Avatars", GUI.skin.window);
        _scroll = GUILayout.BeginScrollView(_scroll);

        for (int i = 0; i < _avatarPaths.Length; i++)
        {
            GUILayout.BeginHorizontal();
            if (_thumbnails[i] != null)
            {
                if (GUILayout.Button(_thumbnails[i], GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize)))
                    SelectAvatar(i);
            }
            else
            {
                if (GUILayout.Button("Select", GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize)))
                    SelectAvatar(i);
            }

            GUILayout.Label(Path.GetFileNameWithoutExtension(_avatarPaths[i]), GUILayout.Width(labelWidth), GUILayout.Height(thumbnailSize));
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void SelectAvatar(int index)
    {
        if (_bootstrap == null)
            _bootstrap = FindObjectOfType<AvatarBootstrap>();
        if (_bootstrap != null)
            _bootstrap.LoadAvatarByIndex(index);
    }
}
