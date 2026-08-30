// MarkerSubmitter.cs —— 上报 POST /api/v1/markers（Task 4）。
// 职责单一：把放置好的标注数据发给 Go 后端，解析信封，失败时缓存待重试。
// 为什么独立成类：ARMarkerController 只管交互放置，网络职责分离，便于复用与排查。
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ParkInspectionAR
{
    public class MarkerSubmitter : MonoBehaviour
    {
        [Header("服务器配置")]
        [Tooltip("Go 后端地址：真机必须用电脑局域网 IP，不能用 localhost（那是手机自己）")]
        public string serverBaseUrl = "http://192.168.8.111:8080";

        // 待重试缓存：上一次失败的上报数据（原型极简方案，不引队列/数据库）
        [HideInInspector] public string cachedJson;

        // 上报结果回调：code==0 成功；否则失败（message 可展示）
        public Action<bool, string> OnResult;

        // 上报入口：由 ReportPanelUI 调用
        public void Submit(string json)
        {
            StartCoroutine(PostCoroutine(json));
        }

        // 重试：直接用缓存的 JSON 重新上报（不重新走表单）
        public void Retry()
        {
            if (!string.IsNullOrEmpty(cachedJson))
            {
                StartCoroutine(PostCoroutine(cachedJson));
            }
        }

        IEnumerator PostCoroutine(string json)
        {
            using (var req = new UnityWebRequest(serverBaseUrl + "/api/v1/markers", "POST"))
            {
                // 契约：Content-Type 必须带 charset=utf-8，否则 Go 端中文会乱码（验收脚本踩过的坑）
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                req.timeout = 8; // 8s 超时：后端宕机时快速失败，不一直转圈

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // 网络层失败（后端未启动/超时）：缓存待重试，回调失败
                    cachedJson = json;
                    OnResult?.Invoke(false, "无法连接服务器: " + req.error);
                    yield break;
                }

                // 解析信封 {code,message,data}：code==0 成功
                try
                {
                    var env = JsonUtility.FromJson<ApiEnvelope>(req.downloadHandler.text);
                    if (env != null && env.code == 0)
                    {
                        cachedJson = null; // 成功后清空缓存
                        OnResult?.Invoke(true, "已上报");
                    }
                    else
                    {
                        cachedJson = json;
                        OnResult?.Invoke(false, "上报失败: " + (env != null ? env.message : "响应格式错误"));
                    }
                }
                catch (Exception e)
                {
                    cachedJson = json;
                    OnResult?.Invoke(false, "解析响应失败: " + e.Message);
                }
            }
        }
    }
}
