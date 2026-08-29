// AndroidToolchain.cs —— 命令行配置 Android SDK/JDK 路径（构建前置）。
// 为什么用官方 AndroidExternalToolsSettings API 而非手写 EditorPrefs：
// Unity 2022.3 的 Android 工具路径读取逻辑内部化（键带 hash 后缀），
// 直接改 EditorPrefs 键名易失效（此前 "JDK not found" 就是键名不匹配导致）；
// 官方 API 保证构建时读到的是同一份配置。
using UnityEditor;
using UnityEditor.Android;

namespace ParkInspectionAR.EditorTools
{
    public static class AndroidToolchain
    {
        [MenuItem("Tools/园区巡检AR/配置 Android 工具链")]
        public static void Configure()
        {
            // 本机安装路径（构建脚本的硬性前提，见 wolf memory 环境备忘）。
            // 为什么 JDK 11 而非 17：Unity 2022.3 官方 API 明确校验
            // "Incompatible Java version"，2022.3 只接受 JDK 11（实测报错确认）。
            // NDK 为什么必须配：即使 Mono 后端 Unity 2022.3 构建也强制校验 NDK（实测报错确认）。
            const string sdkRoot = @"D:\Android\Sdk";
            const string jdkRoot = @"D:\Android\jdk11\jdk-11.0.32.1+1";
            const string ndkRoot = @"D:\Android\Sdk\ndk\23.1.7779620";

            // 官方 API：jdkRootPath/sdkRootPath/ndkRootPath（Unity 2022.3 Scripting API:
            // UnityEngine.Android.AndroidExternalToolsSettings）
            AndroidExternalToolsSettings.jdkRootPath = jdkRoot;
            AndroidExternalToolsSettings.sdkRootPath = sdkRoot;
            AndroidExternalToolsSettings.ndkRootPath = ndkRoot;

            UnityEngine.Debug.Log($"[AndroidToolchain] SDK={AndroidExternalToolsSettings.sdkRootPath}\nJDK={AndroidExternalToolsSettings.jdkRootPath}\nNDK={AndroidExternalToolsSettings.ndkRootPath}");
        }
    }
}
