using UnityEditor;
using UnityEngine;

public static class RpmAnimationSetupPipeline
{
    [MenuItem("Tools/Avatar/Setup RPM Animation Library")]
    public static void RunAll()
    {
        Debug.Log("[RPM] Running animation import setup...");
        RpmAnimationImportConfigurator.Configure();
        AvatarAnimationLibraryBuilder.Build();
        AvatarAnimationMapBuilder.Build();
        Debug.Log("[RPM] Animation setup complete.");
    }
}
