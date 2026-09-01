// ARStatusHud.cs —— 顶部常驻状态栏（任务书 3.1 的"明确提示" + 用户反馈"没有 UI"）。
// 为什么需要：表单面板只在"放置成功后"才弹出，导致 AR 未就绪/无平面时用户看到空白画面，
// 误以为"没 UI / 平面识别失败"。本组件始终显示：AR 会话状态 + 已识别平面数 + 引导文案。
// 关键诊断价值：国行无 GMS 的手机上 ARSession.state 会停在 NeedsInstall/Unsupported，
// 这里会直接把原因显示出来，而不是无声失败。
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace ParkInspectionAR
{
    public class ARStatusHud : MonoBehaviour
    {
        private ARPlaneManager planeManager;
        private Text statusText;
        private float refreshTimer;

        void Start()
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
            BuildUI();
            Refresh(); // 立即刷新一次，避免前 0.25s 空白
        }

        void Update()
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.25f; // 4Hz 刷新足够，避免每帧字符串拼接
                Refresh();
            }
        }

        void BuildUI()
        {
            var go = new GameObject("StatusHud", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.90f);   // 顶部 10% 区域（压缩，给平面留更多可视/可点空间）
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            statusText = CreateText(go.transform, "StatusText", "初始化…");
            statusText.alignment = TextAnchor.MiddleCenter;
        }

        Text CreateText(Transform parent, string name, string content)
        {
            var txtGo = new GameObject(name, typeof(RectTransform));
            txtGo.transform.SetParent(parent, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(16, 8);
            txtRt.offsetMax = new Vector2(-16, -8);
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 30;
            txt.color = Color.white;
            txt.text = content;
            txt.supportRichText = false;
            return txt;
        }

        void Refresh()
        {
            if (statusText == null)
            {
                return;
            }

            var state = ARSession.state; // ARSession.state 是静态属性（5.1.5）
            int planeCount = planeManager != null ? planeManager.trackables.count : 0;

            switch (state)
            {
                case ARSessionState.None:
                    statusText.text = "AR 未启动，请检查 XR 插件配置";
                    break;
                case ARSessionState.Unsupported:
                    statusText.text = "此设备不支持 AR（缺少 ARCore/ARKit）";
                    break;
                case ARSessionState.CheckingAvailability:
                    statusText.text = "正在检查 AR 支持…";
                    break;
                case ARSessionState.NeedsInstall:
                    statusText.text = "需要安装 Google Play Services for AR（国行机常见）";
                    break;
                case ARSessionState.Installing:
                    statusText.text = "正在安装 AR 组件…";
                    break;
                case ARSessionState.Ready:
                    statusText.text = "请缓慢移动手机扫描地面，等待识别平面";
                    break;
                case ARSessionState.SessionInitializing:
                    statusText.text = "AR 会话初始化中…";
                    break;
                case ARSessionState.SessionTracking:
                    statusText.text = planeCount > 0
                        ? $"已识别 {planeCount} 个平面，点击地面放置标记"
                        : "正在检测平面…请缓慢移动手机对准地面";
                    break;
                default:
                    statusText.text = "AR 状态: " + state;
                    break;
            }
        }
    }
}
