using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Shipping guard: a release (non-development) player build fails when a built scene still wires mock
    /// backends - any component from the <c>XRScan.Mocks</c> assembly, or a component whose serialized
    /// <c>useMocks</c> flag is on (AppBootstrap). Development builds only log a warning, so the mock development
    /// scene (XRScan_Dev) stays buildable for testing. Entering Play Mode is never affected.
    /// </summary>
    public class ReleaseMockGuard : IProcessSceneWithReport
    {
        public const string MocksAssemblyName = "XRScan.Mocks";

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return; // Play Mode, not a player build

            var problems = FindMockWiring(scene);
            if (problems.Count == 0) return;

            string message = "[ReleaseMockGuard] Scene '" + scene.path + "' wires mock backends:\n  " +
                             string.Join("\n  ", problems) +
                             "\nShipping builds must use the real backends (XRScan_Main + CompanionConfig); " +
                             "keep mocks in the development scene (XRScan_Dev).";

            if ((report.summary.options & BuildOptions.Development) != 0) Debug.LogWarning(message);
            else throw new BuildFailedException(message);
        }

        /// <summary>Every mock component, and every enabled <c>useMocks</c> flag, in a loaded scene.</summary>
        public static List<string> FindMockWiring(Scene scene)
        {
            var problems = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded) return problems;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue; // missing script
                    var type = behaviour.GetType();
                    string where = PathOf(behaviour.transform) + " (" + type.Name + ")";

                    if (type.Assembly.GetName().Name == MocksAssemblyName)
                    {
                        problems.Add(where + ": mock component");
                        continue;
                    }

                    using (var serialized = new SerializedObject(behaviour))
                    {
                        var flag = serialized.FindProperty("useMocks");
                        if (flag != null && flag.propertyType == SerializedPropertyType.Boolean && flag.boolValue)
                            problems.Add(where + ": useMocks is on");
                    }
                }
            }
            return problems;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (var parent = t.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }
}
