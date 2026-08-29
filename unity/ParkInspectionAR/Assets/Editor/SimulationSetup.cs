// SimulationSetup.cs —— 命令行启用 XR Simulation（编辑器模拟 AR，无需真机/GMS）。
// 为什么：真机 ARCore 需要 GMS（国行 iQOO 未装），先用官方 XR Simulation 验证
// "平面点击→放置→上报"代码逻辑跑通（AR Foundation 5.1.5 内置，无需额外包）。
// 用法：Unity.exe -batchmode -quit -projectPath <proj> -executeMethod ParkInspectionAR.EditorTools.SimulationSetup.Enable
// 关键：XRGeneralSettingsPerBuildTarget 资产由 XR Plug-in Management 首次 GUI 初始化时创建，
// 本脚本用 AssetDatabase 查找现有资产（Assets/XR/XRGeneralSettingsPerBuildTarget.asset），
// 找不到则新建——保证命令行可用。
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.Simulation;

namespace ParkInspectionAR.EditorTools
{
    public static class SimulationSetup
    {
        [MenuItem("Tools/园区巡检AR/启用 XR Simulation")]
        public static void Enable()
        {
            var buildTarget = BuildTargetGroup.Standalone;

            // 1) 查找或创建 XRGeneralSettingsPerBuildTarget 资产（Editor 命名空间类型）
            var perBuildTarget = LoadOrCreatePerBuildTarget();
            if (perBuildTarget == null)
            {
                Debug.LogError("[SimulationSetup] 无法创建 XRGeneralSettingsPerBuildTarget 资产");
                return;
            }

            // 2) 获取该平台的 XRGeneralSettings（无则创建）
            var generalSettings = perBuildTarget.SettingsForBuildTarget(buildTarget);
            if (generalSettings == null)
            {
                generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                generalSettings.name = buildTarget.ToString();
                perBuildTarget.SetSettingsForBuildTarget(buildTarget, generalSettings);
                AssetDatabase.AddObjectToAsset(generalSettings, perBuildTarget);
            }

            // 3) 获取 XRManagerSettings（无则创建）
            var managerSettings = generalSettings.Manager;
            if (managerSettings == null)
            {
                managerSettings = ScriptableObject.CreateInstance<XRManagerSettings>();
                managerSettings.name = $"{buildTarget} Providers";
                generalSettings.Manager = managerSettings;
                AssetDatabase.AddObjectToAsset(managerSettings, generalSettings);
            }

            // 4) 添加 SimulationLoader（若尚未添加）
            bool exists = false;
            foreach (var loader in managerSettings.activeLoaders)
            {
                if (loader != null && loader.GetType() == typeof(SimulationLoader))
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                var loader = ScriptableObject.CreateInstance<SimulationLoader>();
                loader.name = "SimulationLoader";
                managerSettings.TryAddLoader(loader);
                AssetDatabase.AddObjectToAsset(loader, managerSettings);
                Debug.Log("[SimulationSetup] 已添加 SimulationLoader 到 Standalone");
            }

            // 5) 持久化
            EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(perBuildTarget);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SimulationSetup] 完成：Standalone activeLoaders={managerSettings.activeLoaders.Count}");
        }

        static UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget LoadOrCreatePerBuildTarget()
        {
            // 先找现有资产
            var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var existing = AssetDatabase.LoadAssetAtPath<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>(path);
                if (existing != null)
                {
                    Debug.Log($"[SimulationSetup] 复用现有资产: {path}");
                    return existing;
                }
            }

            // 新建（与官方默认路径一致）
            const string createPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            System.IO.Directory.CreateDirectory("Assets/XR");
            var created = ScriptableObject.CreateInstance<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, createPath);
            Debug.Log($"[SimulationSetup] 新建资产: {createPath}");
            return created;
        }
    }
}
