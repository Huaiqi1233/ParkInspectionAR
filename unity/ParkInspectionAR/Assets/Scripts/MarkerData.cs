// MarkerData.cs —— 三端共享数据模型的 C# 声明。
// 字段名必须与 docs/api-contract.md v0.1 及 Go models.go 的 json tag 逐字对齐，
// 否则 UnityWebRequest 上传后 Go 端反序列化会字段错位。
// 为什么用 [Serializable] + JsonUtility：Unity 内置序列化，零第三方依赖，
// 契约体量小，不需要 Newtonsoft.Json（避免过度设计）。
using System;
using UnityEngine;

namespace ParkInspectionAR
{
    // ---- 嵌套几何/位置结构（对应契约 position/rotation/geo）----

    [Serializable]
    public class PositionData
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class RotationData
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    // Geo 可空：无 GPS 时传 null（契约允许 geo 为 null）
    [Serializable]
    public class GeoData
    {
        public double lat;
        public double lng;
    }

    // ---- POST 请求体：id/status/createdAt/updatedAt 由服务端生成，客户端不提交 ----

    [Serializable]
    public class CreateMarkerRequest
    {
        public string type;          // equipment|hazard|route_point|other（枚举白名单服务端校验）
        public string title;
        public string description;
        public PositionData position;  // AR 会话空间坐标（相对园区原点）
        public RotationData rotation;  // AR Foundation Pose 四元数原样上报
        public GeoData geo;            // 可为 null（无 GPS）
        public string reporter;        // 客户端输入，预填"张巡检"
    }

    // ---- 响应信封：与 Go Envelope 对应，code=0 恒成功 ----

    [Serializable]
    public class ApiEnvelope
    {
        public int code;
        public string message;
        public MarkerData data;
    }

    // 完整 Marker 实体（服务端返回用；上报成功后如需回显可读）
    [Serializable]
    public class MarkerData
    {
        public string id;
        public string type;
        public string title;
        public string description;
        public PositionData position;
        public RotationData rotation;
        public GeoData geo;
        public string status;
        public string reporter;
        public string photoUrl;
        public string createdAt;
        public string updatedAt;
    }

    // ---- 工具方法：拼装请求 JSON ----

    public static class MarkerJson
    {
        // BuildCreateJson：由 AR 放置位姿 + 表单输入构造上传体 JSON。
        // JsonUtility.ToJson 输出驼峰字段名（与 C# 字段名一致），契约已按此命名。
        public static string BuildCreateJson(string type, string title, string description,
                                             Pose pose, GeoData geo, string reporter)
        {
            var req = new CreateMarkerRequest
            {
                type = type,
                title = title,
                description = description ?? "",
                // AR Foundation 的 Pose.position 就是 AR 会话空间坐标（契约语义）
                position = new PositionData { x = pose.position.x, y = pose.position.y, z = pose.position.z },
                // pose.rotation 是四元数 {x,y,z,w}，与契约 rotation 字段一一对应
                rotation = new RotationData { x = pose.rotation.x, y = pose.rotation.y, z = pose.rotation.z, w = pose.rotation.w },
                geo = geo, // null 时 JsonUtility 序列化为 {"geo":null}？注意：见下方说明
                reporter = reporter,
            };

            // JsonUtility 不支持顶层 null 字段序列化为 null（会输出默认结构），
            // 这里手动把 geo 从 JSON 中剔除，保证 geo 字段语义正确：
            // 有 GPS 带 geo，无 GPS 不带（Go 端 geo 为 nil → 响应 null）。
            var json = JsonUtility.ToJson(req);
            if (geo == null)
            {
                // 移除 ",geo":{...} 或 "geo":{...} 片段（JsonUtility 输出格式固定为 "geo":{...}）
                json = json.Replace(",\"geo\":{\"lat\":0,\"lng\":0}", "")
                           .Replace("\"geo\":{\"lat\":0,\"lng\":0}", "");
            }
            return json;
        }
    }
}
