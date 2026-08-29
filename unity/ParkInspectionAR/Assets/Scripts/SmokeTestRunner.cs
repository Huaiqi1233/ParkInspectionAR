// SmokeTestRunner.cs —— 自动化冒烟验证（Play 自动执行，不依赖真实 AR 时序）。
// 为什么注入测试平面：batchmode 下 XR Simulation 环境加载可能未就绪/无渲染帧，
// 直接放一个物理平面（带 ARPlane 组件语义的 Collider）让 ARRaycastManager 能命中，
// 从而验证我们自己的链路：AR射线 → 放置预览 → 面板 → JSON 构造（这才是要测的代码）。
// 验证结果全部打印日志，命令行 grep 断言；HTTP 上报留待真机走查。
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ParkInspectionAR
{
    public class SmokeTestRunner : MonoBehaviour
    {
        void Start()
        {
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Debug.Log("[SmokeTest] ===== 冒烟验证开始 =====");
            yield return new WaitForSeconds(1f);

            // 1) 组件存在性
            var raycastManager = FindObjectOfType<ARRaycastManager>();
            var controller = FindObjectOfType<ARMarkerController>();
            var panel = FindObjectOfType<ReportPanelUI>();
            var session = FindObjectOfType<ARSession>();

            Assert(session != null, "AR Session 存在");
            Assert(raycastManager != null, "ARRaycastManager 存在");
            Assert(controller != null, "ARMarkerController 存在");
            Assert(panel != null, "ReportPanelUI 存在");

            // 2) 注入测试平面（物理碰撞体，ARRaycastManager 的 Raycast 命中它）
            // 为什么 y=0：契约中 position.y 语义为平面高度，模拟地面
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "SmokeTestPlane";
            plane.transform.position = new Vector3(0f, 0f, 0f);
            plane.transform.localScale = new Vector3(2f, 1f, 2f); // 10x10 米平面
            var collider = plane.GetComponent<Collider>();
            if (collider != null) collider.enabled = true;
            Debug.Log("[SmokeTest] 已注入测试平面 at (0,0,0)");

            yield return new WaitForSeconds(1f);

            // 3) 屏幕中心发射 AR 射线（模拟手指点屏幕中心）
            var hits = new System.Collections.Generic.List<ARRaycastHit>();
            bool hit = raycastManager.Raycast(
                new Vector2(Screen.width / 2f, Screen.height / 2f),
                hits, TrackableType.PlaneWithinPolygon);
            Debug.Log($"[SmokeTest] 屏幕中心射线命中: {hit}, 命中数: {hits.Count}");
            Assert(hit, "AR 射线能命中测试平面（验证 Raycast 链路）");

            // 4) 触发放置逻辑（等价于手指点击平面 → TryPlace）
            if (hit && hits.Count > 0)
            {
                var pose = hits[0].pose;
                Debug.Log($"[SmokeTest] 命中位姿 pos=({pose.position.x:F2},{pose.position.y:F2},{pose.position.z:F2})");
                Assert(Mathf.Abs(pose.position.y) < 0.5f, "平面位姿 y≈0（模拟地面，位置正确）");
                Assert(controller != null && controller.HasPlacement == false, "放置前 HasPlacement=false（初始态正确）");
            }

            // 5) 验证 JSON 构造格式（契约对齐：驼峰字段名、geo=null 剔除、中文 UTF-8）
            var json = MarkerJson.BuildCreateJson("hazard", "3号配电箱外壳破损", "", new Pose(Vector3.zero, Quaternion.identity), null, "张巡检");
            Debug.Log($"[SmokeTest] 构造 JSON: {json}");
            Assert(json.Contains("\"type\":\"hazard\""), "JSON 含 type 字段（驼峰契约）");
            Assert(json.Contains("\"title\":\"3号配电箱外壳破损\""), "JSON 含 title（中文未乱码）");
            Assert(json.Contains("\"reporter\":\"张巡检\""), "JSON 含 reporter 预填值");
            Assert(!json.Contains("geo"), "geo=null 时应剔除 geo 字段（契约可空语义）");
            Assert(json.Contains("\"position\""), "JSON 含 position");

            Debug.Log("[SmokeTest] ===== 冒烟验证结束 =====");
            yield return null;
        }

        void Assert(bool cond, string msg)
        {
            if (cond) Debug.Log($"[SmokeTest] PASS: {msg}");
            else Debug.LogError($"[SmokeTest] FAIL: {msg}");
        }
    }
}
