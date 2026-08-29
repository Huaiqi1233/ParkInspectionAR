// AndroidToolchain.cs —— 命令行配置 Android SDK/JDK/NDK 路径（构建前置）。
// 为什么单独脚本：Unity 的 Android 工具路径存在 EditorPrefs，GUI 里 Preferences 手配容易漏，
// 脚本化保证命令行构建前工具链路径必然正确。全部官方 API（EditorPrefs 键与 Unity 内部一致）。
using UnityEditor;

namespace ParkInspectionAR.EditorTools
{
    public static class AndroidToolchain
    {
        // Unity 2022.3 Android 工具路径的 EditorPrefs 键（官方内部键名）
        const string k_AndroidSdkRootKey = "AndroidSdkRoot";
        const string k_JdkRootKey = "JdkRoot";
        const string k_AndroidNdkRootKey = "AndroidNdkRoot";

        [MenuItem("Tools/园区巡检AR/配置 Android 工具链")]
        public static void Configure()
        {
            // 本机安装路径（构建脚本的硬性前提，见 wolf memory 环境备忘）
            const string sdkRoot = @"D:\Android\Sdk";
            const string jdkRoot = @"D:\Android\jdk17\jdk-17.0.20.1+1";

            EditorPrefs.SetString(k_AndroidSdkRootKey, sdkRoot);
            EditorPrefs.SetString(k_JdkRootKey, jdkRoot);
            // NDK：本工程用 Mono 后端（不勾 IL2CPP），无需 NDK，留空避免 Unity 报错
            EditorPrefs.DeleteKey(k_AndroidNdkRootKey);

            UnityEngine.Debug.Log($"[AndroidToolchain] SDK={sdkRoot}\nJDK={jdkRoot}\nNDK=未配置（Mono 后端不需要）");
        }
    }
}
