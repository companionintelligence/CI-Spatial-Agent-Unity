using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CompanionIntelligence.EditorTools
{
    public static class QuickIterationMenu
    {
        private const string DevScenePath = "Assets/Scenes/Dev.unity";

        [MenuItem("CI/Open Dev Scene", priority = 10)]
        public static void OpenDevScene()
        {
            if (!File.Exists(DevScenePath))
            {
                EditorUtility.DisplayDialog("Dev Scene Missing", $"Scene not found at {DevScenePath}", "OK");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(DevScenePath);
            }
        }

        [MenuItem("CI/Run EditMode Tests", priority = 20)]
        public static void RunEditModeTests()
        {
            RunTests(TestMode.EditMode);
        }

        [MenuItem("CI/Run PlayMode Tests", priority = 21)]
        public static void RunPlayModeTests()
        {
            RunTests(TestMode.PlayMode);
        }

        [MenuItem("CI/Open Test Results Folder", priority = 30)]
        public static void OpenTestResultsFolder()
        {
            var path = Path.Combine(Application.dataPath, "..", "TestResults");
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static void RunTests(TestMode mode)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var settings = new ExecutionSettings(new Filter
            {
                testMode = mode
            });

            api.Execute(settings);
        }
    }
}
