// GpsLocator.cs —— 读取手机 GPS 经纬度（方案 A：GPS + 地图链接，加分项"跨设备/跨会话定位"）。
// 位置可选：无权限 / 未授权 / 无信号时返回 false，调用方以 (0,0) 兜底（后端视为未定位）。
// 权限在 Assets/Plugins/Android/AndroidManifest.xml 已声明 FINE/COARSE_LOCATION。
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ParkInspectionAR
{
    public static class GpsLocator
    {
        static bool permissionAsked;

        // 幂等启动：请求定位权限 + 启动位置服务（尽早调用，让 GPS 提前热身）
        public static void EnsureStarted()
        {
            if (permissionAsked)
            {
                return;
            }
            permissionAsked = true;
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
            }
#endif
            if (Input.location.isEnabledByUser)
            {
                Input.location.Start(5f, 2f); // 期望精度 5 米，更新间隔 2 秒
            }
        }

        // 尝试读取当前 GPS；成功返回 true 并输出经纬度 + 精度（米，0=未知），失败输出 0
        public static bool TryGet(out float lat, out float lng, out float accuracy)
        {
            lat = 0f;
            lng = 0f;
            accuracy = 0f;
            if (Input.location.status != LocationServiceStatus.Running)
            {
                return false;
            }
            var d = Input.location.lastData;
            if (d.latitude == 0f && d.longitude == 0f)
            {
                return false; // 尚无有效定位
            }
            lat = d.latitude;
            lng = d.longitude;
            accuracy = d.horizontalAccuracy; // 精度（米）
            return true;
        }
    }
}
