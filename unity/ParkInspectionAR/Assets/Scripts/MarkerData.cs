// MarkerData.cs —— 三端共享数据模型的 C# 声明（对齐契约 v2.0）。
// v2.0 严格对齐任务书：priority 字段、status 三态、position 仅 x/y/z，
// 无 rotation/geo/reporter/photoUrl/type。
// 字段名必须与 Go models.go 的 json tag 逐字一致，否则上报后 Go 反序列化错位。
using System;
using UnityEngine;

namespace ParkInspectionAR
{
    [Serializable]
    public class PositionData
    {
        public float x;
        public float y;
        public float z;
    }

    // POST 请求体：id/status/createdAt/updatedAt 由服务端生成（任务书 3.3）
    [Serializable]
    public class CreateMarkerRequest
    {
        public string title;
        public string description;
        public string priority;   // high/medium/low（任务书表单：优先级）
        public PositionData position; // AR 放置点的 x/y/z（任务书：位置）
    }

    // 响应信封：与 Go Envelope 对应，code=0 恒成功
    [Serializable]
    public class ApiEnvelope
    {
        public int code;
        public string message;
        public MarkerData data;
    }

    // 完整 Marker 实体（服务端返回）
    [Serializable]
    public class MarkerData
    {
        public string id;
        public string title;
        public string description;
        public string priority;
        public string status;      // open/in_progress/resolved
        public PositionData position;
        public string createdAt;
        public string updatedAt;
    }

    public static class MarkerJson
    {
        // BuildCreateJson：由 AR 放置位姿 + 表单输入构造上传体 JSON。
        public static string BuildCreateJson(string title, string description, string priority, Pose pose)
        {
            var req = new CreateMarkerRequest
            {
                title = title,
                description = description,
                priority = priority,
                position = new PositionData
                {
                    x = pose.position.x,
                    y = pose.position.y,
                    z = pose.position.z,
                },
            };
            return JsonUtility.ToJson(req);
        }
    }
}
