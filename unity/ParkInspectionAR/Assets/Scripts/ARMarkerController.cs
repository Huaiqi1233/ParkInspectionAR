// ARMarkerController.cs —— 核心交互（Task 4）：
// 触摸 → UI点击区分 → AR平面射线 → 放置预览体。
// 必须区分 UI 点击与 AR 平面点击：EventSystem.IsPointerOverGameObject(fingerId)
// 带触点参数（无参重载只对鼠标有效，真机触摸永远返回 false，会导致点 UI 也穿透发射线）。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ParkInspectionAR
{
    public class ARMarkerController : MonoBehaviour
    {
        [Header("引用（SceneBuilder 自动装配，运行时查找）")]
        private ARRaycastManager raycastManager;
        private Camera arCamera;

        [Header("预览体配置")]
        public GameObject markerPreviewPrefab; // 可选：不配置则代码生成 Cube

        // 预览体：半透明占位，确认上报后转实体
        private GameObject preview;

        // 当前放置位姿（上报时传给 ReportPanelUI）
        public Pose CurrentPose { get; private set; }
        public bool HasPlacement { get; private set; }

        // 命中缓存：避免每帧 GC 分配
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        void Awake()
        {
            // SceneBuilder 把脚本挂在 XR Origin 上，组件同物体可直接拿
            raycastManager = GetComponent<ARRaycastManager>();
            arCamera = GetComponentInChildren<Camera>();
        }

        void Update()
        {
            // 只在有触摸输入时处理（编辑器下可用鼠标模拟：Input.touches 为空时回退鼠标）
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
            // 关键：带 fingerId 的 IsPointerOverGameObject。
            // 手指按在 UI 上（表单/按钮）时返回 true → 忽略，不发射 AR 射线
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                return;
            }

            // 按下瞬间发射一次射线（避免拖动/长按重复放置）
            if (touch.phase != TouchPhase.Began)
            {
                return;
            }

            TryPlace(touch.position);
        }

#if UNITY_EDITOR
        void HandleMouseClick()
        {
            // 编辑器模拟：鼠标无 fingerId，用无参重载（仅编辑器可用，真机不走这里）
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            TryPlace(Input.mousePosition);
        }
#endif

        void TryPlace(Vector2 screenPos)
        {
            // 只检测平面多边形内部：忽略点云/平面外区域，保证标注可落点、不漂到墙上
            if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
            {
                CurrentPose = hits[0].pose; // 结果按距离排序，取最近命中
                HasPlacement = true;

                if (preview == null)
                {
                    preview = CreatePreview();
                }
                // 预览体跟随放置点
                preview.transform.SetPositionAndRotation(CurrentPose.position, CurrentPose.rotation);
                preview.SetActive(true);

                // 通知面板弹出表单
                FindObjectOfType<ReportPanelUI>()?.ShowPanel(true);
            }
        }

        // 生成半透明预览体：Cube + 标题牌（原型最简，不依赖外部 prefab）
        GameObject CreatePreview()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MarkerPreview";
            // 半透明：材质换透明着色器 + 低透明度，表示"待确认"
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            return go;
        }

        // 上报成功后：预览体转实体色（由 ReportPanelUI 调用）
        public void ConfirmVisual()
        {
            if (preview != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.2f, 0.8f, 1f, 1f); // 不透明实体色
                preview.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        // 取消放置：隐藏预览体
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
