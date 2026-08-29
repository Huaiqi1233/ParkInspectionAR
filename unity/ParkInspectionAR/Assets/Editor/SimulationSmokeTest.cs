// SimulationSmokeTest.cs —— 命令行自动化跑 XR Simulation 冒烟验证。
// 用法：Unity.exe -batchmode -projectPath <proj> -executeMethod ParkInspectionAR.EditorTools.SimulationSmokeTest.Run
// 流程：加载 Main 场景 → 挂 SmokeTestRunner → 进 PlayMode（XR Simulation 模拟 AR）
//     → 等断言跑完 → 退出 PlayMode → 检查日志有无 FAIL → EditorApplication.Exit(0/1)。
// 为什么手动 Exit：-quit 会在第一帧退出，PlayMode 没时间跑断言。
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParkInspectionAR.EditorTools
{
    public static class SimulationSmokeTest
    {
        const float WaitSeconds = 15f; // 给断言 + 退出留足时间
        const string LogPath = "smoke_test_result.log"; // 输出到工程根，便于命令行读取
        static double s_StartTime;
        static bool s_PlayExited;

        [MenuItem("Tools/园区巡检AR/运行 XR Simulation 冒烟验证")]
        public static void Run()
        {
            // 1) 打开主场景（含 AR Session / XR Origin / 面板）
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);

            // 2) 挂 SmokeTestRunner（场景根上，Play 后自动执行断言）
            var go = new GameObject("SmokeTestRunner");
            go.AddComponent<SmokeTestRunner>();

            // 3) 进 PlayMode（XR Simulation 此时加载模拟环境）
            s_StartTime = EditorApplication.timeSinceStartup;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_StartTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += CheckDone;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                s_PlayExited = true;
                // PlayMode 完全退出后（下个状态 ExitedPlayMode）再判断结果退出
                EditorApplication.update += CheckExited;
            }
        }

        static void CheckDone()
        {
            if (EditorApplication.timeSinceStartup - s_StartTime > WaitSeconds)
            {
                EditorApplication.update -= CheckDone;
                EditorApplication.ExitPlaymode();
            }
        }

        static void CheckExited()
        {
            if (!s_PlayExited) return;
            // 退出 PlayMode 后（此回调可能在 ExitingPlayMode 即触发，稍等一帧）
            if (EditorApplication.isPlaying) return;

            EditorApplication.update -= CheckExited;
            Finish();
        }

        static void Finish()
        {
            // 读 Unity 日志文件：断言失败会打 LogError（含 "[SmokeTest] FAIL"）
            // Unity 日志路径：%LOCALAPPDATA%\Unity\Editor\Editor.log
            var logPath = Path.Combine(Application.dataPath, "../smoke_test_result.log");
            var editorLog = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Unity", "Editor", "Editor.log");
            bool hasFail = false;
            if (File.Exists(editorLog))
            {
                var content = File.ReadAllText(editorLog);
                hasFail = content.Contains("[SmokeTest] FAIL");
                File.WriteAllText(logPath, hasFail ? "FAIL" : "PASS");
            }
            else
            {
                File.WriteAllText(logPath, "NO_LOG");
            }

            Debug.Log($"[SimulationSmokeTest] 冒烟验证结果: {(hasFail ? "FAIL" : "PASS")} -> {logPath}");
            EditorApplication.Exit(hasFail ? 1 : 0);
        }
    }
}
