using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using XRScan.App;
using XRScan.Mapping;
using XRScan.Platform;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Rebuilds <c>XRScan_Main.unity</c> from an Editor-demo camera into a real mixed-reality rig, in
    /// code so it is reproducible and reviewable (the way <see cref="XRScanBuilder"/> is) rather than
    /// hand-edited YAML. Produces: an <b>AR Session</b> (+ AR input), an <b>XR Origin</b> with a
    /// <b>Floor</b> tracking origin, and a transparent AR camera (clear = solid colour, alpha 0) that
    /// lets Meta passthrough show through, carrying TrackedPoseDriver + ARCameraManager +
    /// ARCameraBackground + <see cref="ARFoundationCameraProvider"/>. It repositions the avatar in front
    /// of the user at floor level, and wires the scene references that make scanning work on device:
    /// <c>AppBootstrap.arCamera</c>, <c>ViewportRaycaster.targetCamera</c> (so pins unproject from the
    /// head pose), and <c>XRInputController.bootstrap</c>. Idempotent: re-running tears down the prior
    /// rig first. Menu: <b>XRScan &gt; Scene &gt; Rebuild MR rig</b>.
    /// </summary>
    public static class MRSceneBuilder
    {
        private const string ScenePath = "Assets/XRScan/App/Scenes/XRScan_Main.unity";
        private const string AvatarName = "Avatar (CI_Brad)";

        // AR Foundation's URP integration requires this renderer feature on the active Universal
        // Renderer when the graphics API is Vulkan (as it is for the Quest/Pico builds). Without it,
        // ARSession.Update throws every frame ("...ARCommandBufferSupportRendererFeature must be added
        // to the current renderer") and passthrough background rendering never runs. Resolved by full
        // name so this editor script carries no compile-time dependency on the exact AR assembly.
        private const string ArFeatureTypeName = "UnityEngine.XR.ARFoundation.ARCommandBufferSupportRendererFeature";
        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/Mobile_Renderer.asset", // Android quality levels
            "Assets/Settings/PC_Renderer.asset",     // default / Editor
        };

        [MenuItem("XRScan/Scene/Rebuild MR rig", false, 40)]
        public static void BuildMRRig()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // --- 1. Tear down any prior rig so the method is idempotent ---
            //     Destroying "XR Origin" also removes its child camera; order the standalone
            //     "Main Camera" (the old editor-demo camera) after it.
            foreach (var name in new[] { "XR Origin", "AR Session", "Main Camera", "XR Input" })
            {
                for (var go = GameObject.Find(name); go != null; go = GameObject.Find(name))
                    Object.DestroyImmediate(go);
            }

            // --- 2. AR Session (drives tracking + supplies pose input) ---
            var sessionGO = new GameObject("AR Session");
            sessionGO.AddComponent<ARSession>();
            sessionGO.AddComponent<ARInputManager>();

            // --- 3. XR Origin (AR): Origin -> Camera Offset -> Main Camera ---
            var originGO = new GameObject("XR Origin");
            var origin = originGO.AddComponent<XROrigin>();

            var offsetGO = new GameObject("Camera Offset");
            offsetGO.transform.SetParent(originGO.transform, false);

            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            camGO.transform.SetParent(offsetGO.transform, false);

            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // alpha 0 -> passthrough composites through
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            camGO.AddComponent<AudioListener>();

            // Head pose from the HMD centre-eye, via the Input System TrackedPoseDriver.
            var tpd = camGO.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(new InputAction(
                "Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction(
                "Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));

            camGO.AddComponent<ARCameraManager>();          // RequireComponent(Camera) — added above
            camGO.AddComponent<ARCameraBackground>();       // RequireComponent(ARCameraManager)
            var provider = camGO.AddComponent<ARFoundationCameraProvider>(); // RequireComponent(ARCameraManager)

            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offsetGO;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // --- 4. Place the avatar ~1.8 m ahead at floor level, facing the user ---
            var avatar = GameObject.Find(AvatarName);
            if (avatar != null)
            {
                avatar.transform.position = new Vector3(0f, 0f, 1.8f);
                avatar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                Debug.LogWarning($"[MRSceneBuilder] Avatar '{AvatarName}' not found — skipped placement.");
            }

            // --- 5. Wire scene references (private [SerializeField]s -> via SerializedObject) ---
            var bootstrap = Object.FindAnyObjectByType<AppBootstrap>();
            SetObjectRef(bootstrap, "arCamera", provider);

            var raycaster = Object.FindAnyObjectByType<ViewportRaycaster>();
            SetObjectRef(raycaster, "targetCamera", cam);

            // --- 6. Controller input handler ---
            var inputGO = new GameObject("XR Input");
            var input = inputGO.AddComponent<XRInputController>();
            SetObjectRef(input, "bootstrap", bootstrap);

            // --- 7. URP renderer feature required for AR background under Vulkan ---
            EnsureArRendererFeature();

            // --- 7b. Android must load OpenXR only (not ARCore) so Meta/Pico owns the AR session ---
            EnsureAndroidOpenXROnly();

            // --- 8. Save ---
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            DumpRig();
            Debug.Log("[MRSceneBuilder] MR rig rebuilt and saved to " + ScenePath);
        }

        /// <summary>
        /// Adds <c>ARCommandBufferSupportRendererFeature</c> to each Universal Renderer that lacks it,
        /// mirroring what the URP inspector's "Add Renderer Feature" does (create sub-asset, append to
        /// <c>m_RendererFeatures</c> and its parallel <c>m_RendererFeatureMap</c> local-id entry).
        /// Idempotent. Also exposed as a menu item so it can be re-applied independently.
        /// </summary>
        [MenuItem("XRScan/Scene/Add AR renderer feature (URP)", false, 41)]
        public static void EnsureArRendererFeature()
        {
            var featureType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(ArFeatureTypeName))
                .FirstOrDefault(t => t != null);
            if (featureType == null)
            {
                Debug.LogError($"[MRSceneBuilder] Type '{ArFeatureTypeName}' not found — is AR Foundation's URP integration present?");
                return;
            }

            foreach (var path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null)
                {
                    Debug.LogWarning($"[MRSceneBuilder] Renderer asset not found: {path} (skipped).");
                    continue;
                }
                if (data.rendererFeatures.Any(f => f != null && featureType.IsInstanceOfType(f)))
                {
                    Debug.Log($"[MRSceneBuilder] {path} already has the AR renderer feature.");
                    continue;
                }

                var feature = ScriptableObject.CreateInstance(featureType);
                feature.name = "AR Command Buffer Support Renderer Feature";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(data);
                Debug.Log($"[MRSceneBuilder] Added AR renderer feature to {path} (localId={localId}).");
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Strips the ARCore and MockHMD loaders from the <b>Android</b> XR loader list, leaving OpenXR
        /// as the only loader. On a Quest/Pico build the OpenXR (Meta/Pico) runtime must own the AR
        /// session + camera subsystems; leaving ARCore in the list makes AR Foundation start ARCore's
        /// session subsystem, which cannot run on the headset and throws every frame from
        /// <c>ARCoreProvider.Update</c>. Editor (Standalone) loaders are left untouched. Idempotent.
        /// </summary>
        [MenuItem("XRScan/Scene/Android: OpenXR loader only", false, 42)]
        public static void EnsureAndroidOpenXROnly()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget perBuildTarget) || perBuildTarget == null)
            {
                Debug.LogError("[MRSceneBuilder] XRGeneralSettingsPerBuildTarget config object not found.");
                return;
            }

            var androidSettings = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            var manager = androidSettings != null ? androidSettings.Manager : null;
            if (manager == null)
            {
                Debug.LogError("[MRSceneBuilder] Android XR manager settings not found.");
                return;
            }

            Debug.Log($"[MRSceneBuilder] Android loaders before: [{LoaderNames(manager)}]");

            var toRemove = manager.activeLoaders
                .Where(l => l != null && (l.GetType().Name.Contains("ARCore") || l.GetType().Name.Contains("MockHMD")))
                .Select(l => l.GetType().FullName)
                .Distinct()
                .ToList();

            foreach (var typeName in toRemove)
            {
                bool ok = XRPackageMetadataStore.RemoveLoader(manager, typeName, BuildTargetGroup.Android);
                Debug.Log($"[MRSceneBuilder] RemoveLoader({typeName}) -> {ok}");
            }

            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MRSceneBuilder] Android loaders after:  [{LoaderNames(manager)}]");
        }

        private static string LoaderNames(XRManagerSettings manager) =>
            string.Join(", ", manager.activeLoaders.Select(l => l != null ? l.GetType().Name : "<null>"));

        private static void SetObjectRef(Object target, string property, Object value)
        {
            if (target == null)
            {
                Debug.LogWarning($"[MRSceneBuilder] Cannot set '{property}': target component not found in scene.");
                return;
            }
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[MRSceneBuilder] Property '{property}' not found on {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Logs the resulting rig so a single batchmode run confirms every piece landed.</summary>
        private static void DumpRig()
        {
            var sb = new StringBuilder("[MRSceneBuilder] Rig verification:\n");

            var origin = Object.FindAnyObjectByType<XROrigin>();
            sb.Append($"  XROrigin present={origin != null}");
            if (origin != null)
                sb.Append($" trackingMode={origin.RequestedTrackingOriginMode} camera={Name(origin.Camera)} offset={Name(origin.CameraFloorOffsetObject)}");
            sb.Append('\n');

            var cam = origin != null ? origin.Camera : null;
            if (cam != null)
            {
                sb.AppendLine($"  Camera clearFlags={cam.clearFlags} bg={cam.backgroundColor} tag={cam.tag}");
                sb.AppendLine($"  Camera components: TrackedPoseDriver={cam.GetComponent<TrackedPoseDriver>() != null}" +
                              $" ARCameraManager={cam.GetComponent<ARCameraManager>() != null}" +
                              $" ARCameraBackground={cam.GetComponent<ARCameraBackground>() != null}" +
                              $" ARFoundationCameraProvider={cam.GetComponent<ARFoundationCameraProvider>() != null}");
            }

            sb.AppendLine($"  ARSession={Object.FindAnyObjectByType<ARSession>() != null}" +
                          $" ARInputManager={Object.FindAnyObjectByType<ARInputManager>() != null}");

            var bootstrap = Object.FindAnyObjectByType<AppBootstrap>();
            sb.AppendLine($"  AppBootstrap.arCamera={ReadRef(bootstrap, "arCamera")}");
            var raycaster = Object.FindAnyObjectByType<ViewportRaycaster>();
            sb.AppendLine($"  ViewportRaycaster.targetCamera={ReadRef(raycaster, "targetCamera")}");
            var input = Object.FindAnyObjectByType<XRInputController>();
            sb.AppendLine($"  XRInputController.bootstrap={ReadRef(input, "bootstrap")}");

            var avatar = GameObject.Find(AvatarName);
            if (avatar != null)
                sb.AppendLine($"  Avatar pos={avatar.transform.position} rot={avatar.transform.eulerAngles}");

            var featureType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(ArFeatureTypeName)).FirstOrDefault(t => t != null);
            foreach (var path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                bool has = data != null && featureType != null &&
                           data.rendererFeatures.Any(f => f != null && featureType.IsInstanceOfType(f));
                sb.AppendLine($"  Renderer {System.IO.Path.GetFileName(path)} hasArFeature={has}");
            }

            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget pbt) && pbt != null)
            {
                var android = pbt.SettingsForBuildTarget(BuildTargetGroup.Android);
                if (android != null && android.Manager != null)
                    sb.AppendLine($"  Android XR loaders=[{LoaderNames(android.Manager)}]");
            }

            Debug.Log(sb.ToString());
        }

        private static string Name(Object o) => o != null ? o.name : "<null>";

        private static string ReadRef(Object target, string property)
        {
            if (target == null) return "<component missing>";
            var prop = new SerializedObject(target).FindProperty(property);
            if (prop == null) return "<property missing>";
            return prop.objectReferenceValue != null ? prop.objectReferenceValue.ToString() : "<null>";
        }
    }
}
