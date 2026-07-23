using System.IO;
using UnityEditor;
using UnityEngine;

public static class AvatarStreamingAssetsSync
{
    const string SourceFolder = "Assets/Avatars";
    const string TargetFolder = "Assets/StreamingAssets/Avatars";

    [MenuItem("Tools/Avatar/Sync Avatars to StreamingAssets")]
    public static void Sync()
    {
        if (!Directory.Exists(SourceFolder))
        {
            Debug.LogWarning($"[AvatarSync] Missing source folder: {SourceFolder}");
            return;
        }

        Directory.CreateDirectory(TargetFolder);
        int copied = 0;
        foreach (var file in Directory.GetFiles(SourceFolder))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".glb" && ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                continue;

            var dest = Path.Combine(TargetFolder, Path.GetFileName(file));
            File.Copy(file, dest, true);
            copied++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[AvatarSync] Copied {copied} files to {TargetFolder}.");
    }
}
