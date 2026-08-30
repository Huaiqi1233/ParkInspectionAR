// EnableARCore.cs —— 为 Android 平台启用 ARCore Loader（真机 AR 必需）。
// 为什么真机黑屏：之前只给 Standalone 加了 SimulationLoader（模拟器用），
// Android 平台未启用 ARCoreLoader，导致 XRSession/Camera/Raycast/Plane 全部 No active。
// 本脚本用官方 XR Plug-in Management API 给 Android 添加 ARCoreLoader。
// 用法：Unity.exe -batchmode -quit -projectPath <proj> -executeMethod ParkInspectionAR.EditorTools.EnableARCore.Enable
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.Management;

namespace ParkInspectionAR.EditorTools
{
    public static class EnableARCore
    {
        [MenuItem("Tools/园区巡检AR/启用 Android ARCore")]
        public static void Enable()
        {
            var buildTarget = BuildTargetGroup.Android;

            // 1) 查找或创建 XRGeneralSettingsPerBuildTarget 资产
            var perBuildTarget = LoadOrCreatePerBuildTarget();
            if (perBuildTarget == null)
            {
                Debug.LogError("[EnableARCore] 无法获取 XRGeneralSettingsPerBuildTarget");
                return;
            }

            // 2) 获取 Android 平台的 XRGeneralSettings（无则创建）
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

            // 4) 添加 ARCoreLoader（若尚未添加）
            bool exists = false;
            foreach (var loader in managerSettings.activeLoaders)
            {
                if (loader != null && loader.GetType() == typeof(ARCoreLoader))
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                var loader = ScriptableObject.CreateInstance<ARCoreLoader>();
                loader.name = "ARCoreLoader";
                managerSettings.TryAddLoader(loader);
                AssetDatabase.AddObjectToAsset(loader, managerSettings);
                Debug.Log("[EnableARCore] 已添加 ARCoreLoader 到 Android");
            }

            // 5) 持久化
            EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(perBuildTarget);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EnableARCore] 完成：Android activeLoaders={managerSettings.activeLoaders.Count}");
        }

        static UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget LoadOrCreatePerBuildTarget()
        {
            var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var existing = AssetDatabase.LoadAssetAtPath<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>(path);
                if (existing != null)
                {
                    Debug.Log($"[EnableARCore] 复用现有资产: {path}");
                    return existing;
                }
            }
            const string createPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            System.IO.Directory.CreateDirectory("Assets/XR");
            var created = ScriptableObject.CreateInstance<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, createPath);
            Debug.Log($"[EnableARCore] 新建资产: {createPath}");
            return created;
        }
    }
}
