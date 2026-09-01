// ReportPanelUI.cs —— 底部表单面板（任务书 3.1：填写表单）。
// 表单字段严格对齐任务书：标题 / 描述 / 优先级(high/medium/low) / 位置(自动取 AR 位姿 x/y/z)。
// UI 设计原则（用户反馈）：简洁、大字、无冗余。只保留必需控件。
// 用 UnityEngine.UI 原生组件（TextMeshPro 需额外字体资源，原型不引入）。
using UnityEngine;
using UnityEngine.UI;

namespace ParkInspectionAR
{
    public class ReportPanelUI : MonoBehaviour
    {
        private ARMarkerController controller;
        private MarkerSubmitter submitter;

        private GameObject panel;
        private GameObject toastGo;                 // toast 容器（用于显示/隐藏 + 自动消失）
        private Coroutine toastHideCoroutine;       // 自动隐藏协程
        private string selectedPriority = "high";  // 当前优先级（默认 high）
        private readonly System.Collections.Generic.Dictionary<string, Button> priorityButtons = new System.Collections.Generic.Dictionary<string, Button>();
        private InputField titleInput;      // 标题
        private InputField descInput;       // 描述
        private Text positionText;          // 位置只读显示（AR 自动填充）
        private Text toastText;             // 状态提示
        private Button submitBtn;           // 提交
        private Button retryBtn;            // 重试（仅失败显示）

        void Start()
        {
            controller = FindObjectOfType<ARMarkerController>();
            submitter = FindObjectOfType<MarkerSubmitter>();
            GpsLocator.EnsureStarted(); // 提前请求定位权限并启动 GPS（方案 A），提交时大概率已有定位
            BuildUI();
            if (submitter != null)
            {
                submitter.OnResult += OnSubmitResult;
            }
        }

        void OnDestroy()
        {
            if (submitter != null)
            {
                submitter.OnResult -= OnSubmitResult;
            }
        }

        public void ShowPanel(bool show)
        {
            if (panel != null)
            {
                panel.SetActive(show);
                if (show)
                {
                    UpdatePositionDisplay();
                }
            }
        }

        // 查看已放置标记的输入内容（任务书 3.1：点击标记查看输入）
        public void ShowViewPanel(string title, string description, string priority, string positionText)
        {
            ShowToast(string.Format("[{0}] {1}\n{2}\n{3}", priority, title, description, positionText));
            Debug.Log($"[ReportPanel] 查看标记: {title} / {description} / {priority} / {positionText}");
        }

        // 位置显示：从 ARMarkerController 读取当前放置位姿的 x/y/z（任务书：位置字段）
        void UpdatePositionDisplay()
        {
            if (controller != null && controller.HasPlacement && positionText != null)
            {
                var p = controller.CurrentPose.position;
                positionText.text = string.Format("位置: x={0:F2} y={1:F2} z={2:F2}", p.x, p.y, p.z);
            }
            else if (positionText != null)
            {
                positionText.text = "位置: 未放置（先点平面）";
            }
        }

        // 提交上报（任务书：提交反馈）
        void OnSubmitClick()
        {
            var title = titleInput.text.Trim();
            if (title.Length == 0)
            {
                ShowToast("请填写标题");
                return;
            }
            var desc = descInput.text.Trim(); // 描述：可选（任务书 3.1），不校验非空
            // 若尚未在 AR 平面放置标记，用当前相机前方位置兜底（保证功能连通）
            var pose = controller != null && controller.HasPlacement
                ? controller.CurrentPose
                : new Pose(Vector3.zero, Quaternion.identity);

            var priority = selectedPriority;
            GpsLocator.TryGet(out float lat, out float lng, out float accuracy); // 失败为 (0,0)，后端视为未定位
            var photo = PhotoCapture.CaptureBase64(); // 现场照片（方案 C），失败为空串
            var json = MarkerJson.BuildCreateJson(title, desc, priority, pose, lat, lng, accuracy, photo);
            // photo 是 base64 很大，日志截断避免刷屏
            Debug.Log("[ReportPanel] 提交 JSON(长度=" + json.Length + "): " +
                (json.Length > 300 ? json.Substring(0, 300) + "…" : json));
            submitter.Submit(json);
            ShowToast("提交中…");
        }

        void OnRetryClick()
        {
            if (submitter != null)
            {
                retryBtn.gameObject.SetActive(false);
                submitter.Retry();
            }
        }

        void OnSubmitResult(bool success, string message)
        {
            ShowToast(message);
            if (!success)
            {
                retryBtn.gameObject.SetActive(true);
            }
            else
            {
                retryBtn.gameObject.SetActive(false);
                if (controller != null)
                {
                    // 上报成功：预览体转实体标记，记录标题/描述/优先级供"点击查看"
                    controller.ConfirmVisual(
                        titleInput.text.Trim(),
                        descInput.text.Trim(),
                        selectedPriority);
                }
                // 成功后隐藏面板并清空表单，允许继续点平面放置新标记（"已上报"提示仍在顶部 toast 显示）
                ShowPanel(false);
                titleInput.text = "";
                descInput.text = "";
            }
        }

        void ShowToast(string msg)
        {
            if (toastText != null)
            {
                toastText.text = msg;
                toastText.fontSize = 32;              // 复位（ShowViewPanel 可能改过）
                toastText.alignment = TextAnchor.MiddleCenter;
                if (toastGo != null) toastGo.SetActive(true);
                if (toastHideCoroutine != null) StopCoroutine(toastHideCoroutine);
                toastHideCoroutine = StartCoroutine(HideToastAfter(2.5f)); // 2.5s 后自动消失
            }
            Debug.Log("[ReportPanel] " + msg);
        }

        System.Collections.IEnumerator HideToastAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (toastGo != null) toastGo.SetActive(false);
        }

        // ---- UI 构建（简洁大字）----

        void BuildUI()
        {
            panel = new GameObject("ReportPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.35f); // 只占屏幕下 35%，给上方平面留出可点区域
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8;

            // 标题输入
            titleInput = CreateInputField("TitleInput", "标题（如：3号楼前地面破损）");

            // 描述输入
            descInput = CreateInputField("DescInput", "描述（如：地面有约30cm裂缝）");

            // 优先级选择：3 个按钮（high/medium/low）。
            // 为什么不用 Dropdown：程序化创建的 Dropdown 缺 template/targetGraphic，无法弹出选项列表，手机上点不动。
            var priorityRow = new GameObject("PriorityRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            priorityRow.transform.SetParent(panel.transform, false);
            var prLayout = priorityRow.GetComponent<HorizontalLayoutGroup>();
            prLayout.spacing = 16;
            prLayout.childForceExpandWidth = true;
            prLayout.childControlWidth = true;
            foreach (var p in new[] { "high", "medium", "low" })
            {
                var captured = p; // 闭包捕获：避免 foreach 变量复用
                var pbtn = CreateButton("Priority_" + p, p, priorityRow.transform, new Color(0.4f, 0.4f, 0.45f, 1f));
                pbtn.onClick.AddListener(() => SetPriority(captured));
                priorityButtons[captured] = pbtn;
            }
            SetPriority("high");

            // 位置只读显示
            positionText = CreateLabel("PositionText", "位置: 未放置");

            // 按钮行
            var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(panel.transform, false);
            var btnRowLayout = btnRow.GetComponent<HorizontalLayoutGroup>();
            btnRowLayout.spacing = 16;
            btnRowLayout.childForceExpandWidth = true;
            btnRowLayout.childControlWidth = true;

            submitBtn = CreateButton("SubmitBtn", "提交", btnRow.transform, new Color(0.2f, 0.7f, 0.3f, 1f));
            submitBtn.onClick.AddListener(OnSubmitClick);

            retryBtn = CreateButton("RetryBtn", "重试", btnRow.transform, new Color(0.9f, 0.5f, 0.2f, 1f));
            retryBtn.onClick.AddListener(OnRetryClick);
            retryBtn.gameObject.SetActive(false);

            // Toast 提示：独立于表单面板，放在顶部状态栏下方（不遮挡底部平面）。
            // 为什么独立：提交成功后要隐藏面板，但"已上报"提示仍需可见，所以 toast 不能挂在 panel 下。
            // 注意：uGUI 一个 GameObject 只能挂一个 Graphic（Image/Text 互斥），所以 Text 必须是 Image 的子节点。
            toastGo = new GameObject("Toast", typeof(RectTransform), typeof(Image));
            toastGo.transform.SetParent(transform, false); // 挂 Canvas，不是 panel
            var toastRt = toastGo.GetComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0.05f, 0.76f);
            toastRt.anchorMax = new Vector2(0.95f, 0.90f);
            toastRt.offsetMin = Vector2.zero;
            toastRt.offsetMax = Vector2.zero;
            var toastBg = toastGo.GetComponent<Image>();
            toastBg.color = new Color(0f, 0f, 0f, 0.65f);

            var toastTextGo = new GameObject("Text", typeof(RectTransform));
            toastTextGo.transform.SetParent(toastGo.transform, false);
            var toastTextRt = toastTextGo.GetComponent<RectTransform>();
            toastTextRt.anchorMin = Vector2.zero;
            toastTextRt.anchorMax = Vector2.one;
            toastTextRt.offsetMin = Vector2.zero;
            toastTextRt.offsetMax = Vector2.zero;
            toastText = toastTextGo.AddComponent<Text>();
            toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            toastText.fontSize = 32;
            toastText.color = new Color(1f, 0.9f, 0.3f, 1f);
            toastText.alignment = TextAnchor.MiddleCenter;
            toastText.supportRichText = false;
            toastGo.SetActive(false); // 初始隐藏，ShowToast 时再显示

            // 面板初始隐藏，等 AR 放置后弹出
            panel.SetActive(false);
        }

        // ---- 控件工厂（统一大字 40-44px）----

        // 选中优先级：高亮选中按钮（绿），其余灰
        void SetPriority(string value)
        {
            selectedPriority = value;
            foreach (var kv in priorityButtons)
            {
                var img = kv.Value.GetComponent<Image>();
                img.color = kv.Key == value
                    ? new Color(0.2f, 0.7f, 0.3f, 1f)   // 选中：绿
                    : new Color(0.4f, 0.4f, 0.45f, 1f);  // 未选中：灰
            }
        }

        InputField CreateInputField(string name, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var input = go.AddComponent<InputField>();
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(go.transform, false);
            var ph = phGo.AddComponent<Text>();
            ph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ph.fontSize = 32;
            ph.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            ph.text = placeholder;
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 32;
            txt.color = Color.white;
            txt.supportRichText = false;
            input.textComponent = txt;
            input.placeholder = ph;
            return input;
        }

        Text CreateLabel(string name, string content)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 28;
            txt.color = Color.white;
            txt.text = content;
            return txt;
        }

        Button CreateButton(string name, string label, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 34;
            txt.color = Color.white;
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            return btn;
        }
    }
}
