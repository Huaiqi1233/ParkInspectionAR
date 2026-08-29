// LogicSmokeTest.cs —— 纯逻辑冒烟验证（不依赖 PlayMode/AR 运行时，batchmode 可跑）。
// 为什么：batchmode 下 XR Simulation 的 PlayMode 会卡死（模拟器需要 GUI 渲染帧），
// 改为验证可脱离 AR 运行的纯逻辑：JSON 构造（契约对齐）+ 信封解析 + UTF-8 编码。
// 用法：Unity.exe -batchmode -quit -projectPath <proj> -executeMethod ParkInspectionAR.EditorTools.LogicSmokeTest.Run
using UnityEditor;
using UnityEngine;

namespace ParkInspectionAR.EditorTools
{
    public static class LogicSmokeTest
    {
        [MenuItem("Tools/园区巡检AR/运行纯逻辑冒烟验证")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string msg)
            {
                if (cond) Debug.Log($"[LogicTest] PASS: {msg}");
                else { Debug.LogError($"[LogicTest] FAIL: {msg}"); fail++; }
            }

            Debug.Log("[LogicTest] ===== 纯逻辑冒烟验证开始 =====");

            // 1) JSON 构造：geo=null 时应剔除 geo 字段（契约可空语义）
            var json1 = MarkerJson.BuildCreateJson("hazard", "3号配电箱外壳破损", "",
                new Pose(new Vector3(12.5f, 0f, -8.2f), new Quaternion(0f, 0.7071f, 0f, 0.7071f)),
                null, "张巡检");
            Debug.Log($"[LogicTest] JSON(null geo): {json1}");
            Check(json1.Contains("\"type\":\"hazard\""), "JSON 含 type（驼峰契约）");
            Check(json1.Contains("\"title\":\"3号配电箱外壳破损\""), "JSON 含中文 title（未乱码）");
            Check(json1.Contains("\"reporter\":\"张巡检\""), "JSON 含 reporter 预填值");
            Check(!json1.Contains("geo"), "geo=null 时剔除 geo 字段");
            Check(!json1.Contains(",,"), "剔除 geo 后无残留双逗号（非法 JSON 防回归）");
            Check(json1.Contains("\"position\":{\"x\":12.5"), "position.x 值正确");

            // 2) JSON 构造：geo 非空时应保留
            var geo = new GeoData { lat = 39.9042, lng = 116.4074 };
            var json2 = MarkerJson.BuildCreateJson("equipment", "水泵", "", new Pose(Vector3.zero, Quaternion.identity), geo, "李工");
            Debug.Log($"[LogicTest] JSON(with geo): {json2}");
            Check(json2.Contains("\"geo\":{\"lat\":39.9042,\"lng\":116.4074}"), "geo 非空时保留完整字段");

            // 3) 信封解析（服务端返回的 {code,message,data}）
            var okEnv = JsonUtility.FromJson<ApiEnvelope>("{\"code\":0,\"message\":\"ok\",\"data\":{\"id\":\"abc\"}}");
            Check(okEnv != null && okEnv.code == 0, "信封解析 code=0（成功）");
            var failEnv = JsonUtility.FromJson<ApiEnvelope>("{\"code\":40001,\"message\":\"type 非法\"}");
            Check(failEnv != null && failEnv.code == 40001 && failEnv.message == "type 非法", "信封解析非 0 code（业务错误）");

            // 4) UTF-8 编码（上报 body 的字节编码，与 Go 端一致）
            var bytes = System.Text.Encoding.UTF8.GetBytes("3号配电箱");
            var decoded = System.Text.Encoding.UTF8.GetString(bytes);
            Check(decoded == "3号配电箱", "UTF-8 编解码中文无损");

            Debug.Log($"[LogicTest] ===== 结束：{(fail == 0 ? "全部通过 ✅" : $"{fail} 项失败 ❌")} =====");
            if (fail > 0)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
