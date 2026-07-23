using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BlenderRpmAnimationPipeline
{
    const string DefaultSourceDir = "Assets/AnimationLibrary/Source";
    const string DefaultOutputDir = "Assets/AnimationLibrary";

    [MenuItem("Tools/Avatar/Convert GLB to FBX (Blender MCP)")]
    public static void ConvertGlbToFbx()
    {
        var input = ResolveInputDir();
        var output = ResolveOutputDir();

        if (!Directory.Exists(input))
        {
            Debug.LogWarning($"[RPM] Missing input folder: {input}");
            return;
        }

        Directory.CreateDirectory(output);
        var script = Path.Combine(Application.dataPath, "..", "scripts", "blender_batch_convert.py");
        script = Path.GetFullPath(script);

        if (!File.Exists(script))
        {
            Debug.LogWarning($"[RPM] Missing converter script: {script}");
            return;
        }

        RunProcess("python3", $"\"{script}\" --input \"{input}\" --output \"{output}\"", Application.dataPath);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Avatar/Run RPM Animation Import Pipeline (Blender MCP)")]
    public static void RunPipeline()
    {
        ConvertGlbToFbx();
        RpmAnimationSetupPipeline.RunAll();
    }

    static string ResolveInputDir()
    {
        var env = Environment.GetEnvironmentVariable("CI_RPM_GLTF_PATH");
        if (!string.IsNullOrEmpty(env))
            return env;
        return Path.Combine(Application.dataPath, "..", DefaultSourceDir);
    }

    static string ResolveOutputDir()
    {
        var env = Environment.GetEnvironmentVariable("CI_RPM_FBX_PATH");
        if (!string.IsNullOrEmpty(env))
            return env;
        return Path.Combine(Application.dataPath, "..", DefaultOutputDir);
    }

    static void RunProcess(string fileName, string args, string workingDir)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(startInfo))
            {
                if (proc == null)
                {
                    Debug.LogWarning("[RPM] Failed to start conversion process.");
                    return;
                }

                string output = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    Debug.Log(output);
                if (!string.IsNullOrEmpty(err))
                    Debug.LogWarning(err);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RPM] Conversion failed: {ex.Message}");
        }
    }
}
