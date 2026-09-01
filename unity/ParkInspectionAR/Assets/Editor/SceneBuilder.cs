// SceneBuilder.cs —— Editor 菜单一键搭建 AR 场景（Task 3）。
// 为什么用脚本搭场景而不是手摆：场景对象层级、组件引用容易出错，
// 脚本化保证每次重建结果一致；且全部使用 AR Foundation 5.1.5 官方公开 API
// （XROriginCreateUtil 是 internal 不可调用，故参照其源码用公开组件自行拼装，已对照 PackageCache 查证）。
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;      // InputAction 扩展方法 AddBinding 所在命名空间
using UnityEngine.InputSystem.XR;   // TrackedPoseDriver
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ParkInspectionAR.EditorTools
{
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/园区巡检AR/一键搭建场景")]
        public static void BuildScene()
        {
            // 清空当前场景：保证可重复执行、结果一致
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();

            // ---- 1) XR Origin：官方推荐的 AR 场景根（XROrigin 在 com.unity.xr.core-utils）----
            var originGo = new GameObject("XR Origin", typeof(XROrigin));
            Undo.RegisterCreatedObjectUndo(originGo, "Create XR Origin");

            // Camera Offset：XROrigin 要求相机放在其下，作为设备位姿的偏移层
            var offsetGo = new GameObject("Camera Offset");
            Undo.RegisterCreatedObjectUndo(offsetGo, "Create Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            // Main Camera：AR 相机（ARCameraManager 管理相机画面 + TrackedPoseDriver 跟随设备位姿）
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener),
                typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
            camGo.transform.SetParent(offsetGo.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.tag = "MainCamera"; // Camera.main 可找到（ARMarkerController 依赖）
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black; // AR 背景由 ARCameraBackground 渲染相机画面

            // TrackedPoseDriver：让相机跟随 AR 设备位姿（参照官方 XROriginCreateUtil 的绑定配置）。
            // 位置/旋转各一个 InputAction，绑定 XR HMD 与手持 AR 设备的位姿通道。
            var tpd = camGo.GetComponent<TrackedPoseDriver>();
            var posAction = new UnityEngine.InputSystem.InputAction(
                "Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            posAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotAction = new UnityEngine.InputSystem.InputAction(
                "Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            tpd.positionInput = new UnityEngine.InputSystem.InputActionProperty(posAction);
            tpd.rotationInput = new UnityEngine.InputSystem.InputActionProperty(rotAction);

            // 关联 XROrigin 与相机（CameraOffsetObject 用于设备高度偏移）
            var origin = originGo.GetComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = cam;

            // ---- 2) AR Session：驱动整个 AR 会话（官方示例：ARSession + ARInputManager）----
            var sessionGo = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            Undo.RegisterCreatedObjectUndo(sessionGo, "Create AR Session");

            // ---- 3) AR Plane Manager：平面检测（射线命中的前提）。
            // 注意：5.1.5 用 requestedDetectionMode（detectionMode 已废弃）；
            // Horizontal|Vertical 允许地面/桌面/墙面放置，覆盖园区巡检场景。
            var planeManager = originGo.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

            // 关键：planePrefab 为空时 ARPlaneManager 只创建裸 ARPlane（无 MeshRenderer），
            // 平面被检测到但完全不可见 → 用户误判"平面无法识别"。
            // 用官方 ARPlaneMeshVisualizer（AR Foundation 5.1.5 内置）+ 半透明材质做可视化。
            planeManager.planePrefab = CreatePlanePrefab();

            // ---- 4) AR Raycast Manager：屏幕点 → 平面命中（ARMarkerController 调用）----
            originGo.AddComponent<ARRaycastManager>();

            // ---- 5) EventSystem + StandaloneInputModule：UI 点击必需（IsPointerOverGameObject 依赖 EventSystem）。
            // 为什么用 StandaloneInputModule 而非 InputSystemUIInputModule：
            // 本工程 ProjectSettings activeInputHandler=0（旧 Input Manager），触摸用 Input.touches 读取，
            // 配套的 UI 输入模块必须是 StandaloneInputModule，否则按钮/下拉无法响应。----
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");

            // ---- 6) UI Canvas：表单面板载体（ScreenSpaceOverlay：UI 永远在 AR 画面上方）----
            var canvasGo = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create UICanvas");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // 适配不同手机分辨率
            scaler.referenceResolution = new Vector2(1080, 1920);

            // ---- 7) 挂运行时脚本（组件由 SceneBuilder 装配，脚本内部用 Find 解决引用）----
            originGo.AddComponent<ARMarkerController>();
            originGo.AddComponent<MarkerSubmitter>();
            canvasGo.AddComponent<ReportPanelUI>();
            canvasGo.AddComponent<ARStatusHud>(); // 顶部常驻状态栏（AR 状态/平面数/引导）

            // ---- 8) 保存场景并加入 Build Settings（Android 构建需要场景在列表里）----
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(newScene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log("[SceneBuilder] 场景搭建完成: " + ScenePath);
            Selection.activeGameObject = originGo;
        }

        // 创建 AR 平面可视化预制体（借用官方 ARPlaneMeshVisualizer 组件，AR Foundation 5.1.5 内置）。
        // 平面网格由官方组件按边界自动生成；半透明填充 + 描边让平面"肉眼可见"，
        // 解决 planePrefab 为空时"平面检测到了但看不见"的误判。
        static GameObject CreatePlanePrefab()
        {
            System.IO.Directory.CreateDirectory("Assets/Materials");

            // 填充材质：Sprites/Default 是 Cull Off（双面）透明 shader，从任意角度看平面都可见
            var fillMat = new Material(Shader.Find("Sprites/Default"));
            fillMat.name = "ARPlaneFill";
            fillMat.color = new Color(0.2f, 0.85f, 1f, 0.35f);
            AssetDatabase.DeleteAsset("Assets/Materials/ARPlaneFill.mat");
            AssetDatabase.CreateAsset(fillMat, "Assets/Materials/ARPlaneFill.mat");

            // 描边材质：不透明，勾勒平面边界
            var lineMat = new Material(Shader.Find("Sprites/Default"));
            lineMat.name = "ARPlaneLine";
            lineMat.color = new Color(0.3f, 0.9f, 1f, 1f);
            AssetDatabase.DeleteAsset("Assets/Materials/ARPlaneLine.mat");
            AssetDatabase.CreateAsset(lineMat, "Assets/Materials/ARPlaneLine.mat");

            // 平面模板：ARPlaneMeshVisualizer 自动生成边界网格 + 更新 LineRenderer
            var go = new GameObject("ARPlane");
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fillMat;

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMat;
            line.startWidth = 0.01f;
            line.endWidth = 0.01f;
            line.loop = true;          // 闭合多边形边界
            line.useWorldSpace = false;
            line.positionCount = 0;

            go.AddComponent<ARPlaneMeshVisualizer>(); // [RequireComponent(ARPlane)] 会自动补 ARPlane

            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            AssetDatabase.DeleteAsset("Assets/Prefabs/ARPlane.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/ARPlane.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
