// ARMarkerController.cs —— 核心交互（任务书 3.1）：
// 触摸 → UI点击区分 → AR平面射线 → 放置预览体 → 点击已放置标记查看输入。
// 必须区分 UI 点击与 AR 平面点击：EventSystem.IsPointerOverGameObject(fingerId)
// 带触点参数（无参重载只对鼠标有效，真机触摸永远返回 false，点 UI 会穿透发射线）。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ParkInspectionAR
{
    // 已放置标记：记录其标题/描述/优先级，供"点击查看"（任务书 3.1：点击标记查看输入）
    public class PlacedMarker : MonoBehaviour
    {
        public string title;
        public string description;
        public string priority;
        public string positionText;
    }

    public class ARMarkerController : MonoBehaviour
    {
        private ARRaycastManager raycastManager;
        private ARPlaneManager planeManager;
        private Camera arCamera;

        private GameObject preview;         // 半透明预览体（待确认）
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        // 已放置的标记列表（供点击查看）
        private readonly List<PlacedMarker> placedMarkers = new List<PlacedMarker>();

        public Pose CurrentPose { get; private set; }
        public bool HasPlacement { get; private set; }

        void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
            planeManager = GetComponent<ARPlaneManager>();
            arCamera = GetComponentInChildren<Camera>();
            if (raycastManager == null)
                Debug.LogError("[ARMarker] ARRaycastManager 未找到（应与本组件同挂 XR Origin）");
            if (planeManager == null)
                Debug.LogError("[ARMarker] ARPlaneManager 未找到");
        }

        void Update()
        {
            if (Input.touchCount > 0)
            {
                HandleTouch(Input.GetTouch(0));
            }
#if UNITY_EDITOR
            else if (Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }
#endif
        }

        void HandleTouch(Touch touch)
        {
            if (touch.phase != TouchPhase.Began)
            {
                return;
            }
            // 关键：带 fingerId 的 IsPointerOverGameObject。
            // 手指按在 UI 上时返回 true → 忽略，不发射 AR 射线
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                Debug.Log("[ARMarker] 点击被 UI 拦截 fingerId=" + touch.fingerId + " pos=" + touch.position);
                return;
            }

            // 先尝试点击已放置标记（查看输入）
            if (TryHitPlacedMarker(touch.position))
            {
                return;
            }
            TryPlace(touch.position);
        }

#if UNITY_EDITOR
        void HandleMouseClick()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            TryPlace(Input.mousePosition);
        }
#endif

        // 射线检测已放置标记：命中则弹出查看面板
        bool TryHitPlacedMarker(Vector2 screenPos)
        {
            if (arCamera == null)
            {
                return false;
            }
            var ray = arCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                var placed = hit.collider.GetComponentInParent<PlacedMarker>();
                if (placed != null)
                {
                    var panel = FindObjectOfType<ReportPanelUI>();
                    panel?.ShowViewPanel(placed.title, placed.description, placed.priority, placed.positionText);
                    return true;
                }
            }
            return false;
        }

        void TryPlace(Vector2 screenPos)
        {
            if (raycastManager == null)
            {
                Debug.LogError("[ARMarker] ARRaycastManager 为 null，无法投放");
                return;
            }
            // 只检测平面多边形内部；叠加 PlaneEstimated 兼容"已显示但尚未完全追踪"的平面
            if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated))
            {
                CurrentPose = hits[0].pose;
                HasPlacement = true;

                if (preview == null)
                {
                    preview = CreatePreview();
                }
                preview.transform.SetPositionAndRotation(CurrentPose.position, CurrentPose.rotation);
                preview.SetActive(true);

                Debug.Log("[ARMarker] 射线命中平面 pos=" + CurrentPose.position + " screenPos=" + screenPos);
                FindObjectOfType<ReportPanelUI>()?.ShowPanel(true);
            }
            else
            {
                Debug.Log("[ARMarker] 射线未命中平面 screenPos=" + screenPos +
                    " 平面数=" + (planeManager != null ? planeManager.trackables.count : -1));
            }
        }

        GameObject CreatePreview()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MarkerPreview";
            var mat = new Material(Shader.Find("Sprites/Default")); // 不用 Standard：Android 构建会裁剪它，Shader.Find 返回 null 抛 ArgumentNullException
            mat.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            // 加 Collider：让"点击标记查看"的 Physics.Raycast 能命中
            return go;
        }

        // 上报成功后：预览体转实体色 + 注册为可点击标记（任务书：点击标记查看输入）
        public void ConfirmVisual(string title, string description, string priority)
        {
            if (preview != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default")); // 不用 Standard：Android 构建会裁剪它，Shader.Find 返回 null 抛 ArgumentNullException
                mat.color = new Color(0.2f, 0.8f, 1f, 1f);
                preview.GetComponent<Renderer>().sharedMaterial = mat;

                var placed = preview.AddComponent<PlacedMarker>();
                placed.title = title;
                placed.description = description;
                placed.priority = priority;
                var p = CurrentPose.position;
                placed.positionText = string.Format("x={0:F2} y={1:F2} z={2:F2}", p.x, p.y, p.z);
                placedMarkers.Add(placed);

                preview = null; // 预览体已转为正式标记，下次放置创建新的
                HasPlacement = false;
            }
        }

        public void CancelPlacement()
        {
            if (preview != null)
            {
                preview.SetActive(false);
            }
            HasPlacement = false;
        }
    }
}
