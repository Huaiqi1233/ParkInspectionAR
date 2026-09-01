// BuildScript.cs —— 命令行构建 Android APK（Task 5）。
// 用法：Unity.exe -batchmode -quit -projectPath <proj> -executeMethod ParkInspectionAR.EditorTools.BuildScript.BuildAndroid
// 流程：搭场景 → 切 Android 平台 → 设置包名/最低SDK → BuildPipeline 构建 APK。
// 全部使用 Unity 2022.3 官方 API（BuildPipeline/PlayerSettings/EditorUserBuildSettings）。
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ParkInspectionAR.EditorTools
{
    public static class BuildScript
    {
        // 包名：Android 应用唯一标识（安装/覆盖安装依据），原型用反域名格式
        const string PackageName = "com.parkinspection.ar";

        [MenuItem("Tools/园区巡检AR/构建 Android APK")]
        public static void BuildAndroid()
        {
            // 1) 先搭场景（复用 SceneBuilder，保证构建用的是最新场景结构）
            SceneBuilder.BuildScene();

            // 2) 切换到 Android 平台（首次切换需导入平台模块，耗时较长）
            var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            if (!switched)
            {
                throw new Exception("切换到 Android 平台失败：请确认已安装 Android Build Support 模块");
            }

            // 3) Android 构建配置（ARCore 要求 minSdkVersion >= 24，见 ARCore 包源码校验）
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageName);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
            // 允许明文 HTTP：Go 后端是局域网 http://，Android 9+ 默认禁明文，
            // 否则 UnityWebRequest 抛 "InvalidOperationException: Insecure connection not allowed"
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            // 图形 API：ARCore 不支持某些 API（包内 preprocess 会校验），用 OpenGLES3 是 AR 设备最稳组合
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            // 构建 APK（非 AAB）：便于直接安装到测试机
            EditorUserBuildSettings.buildAppBundle = false;

            // 4) 构建
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/ParkInspectionAR.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var result = BuildPipeline.BuildPlayer(options);

            if (result.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] APK 构建成功: {result.summary.outputPath} ({result.summary.totalSize / 1024 / 1024} MB)");
            }
            else
            {
                throw new Exception($"[BuildScript] APK 构建失败: {result.summary.result}, 错误数={result.summary.totalErrors}");
            }
        }
    }
}
