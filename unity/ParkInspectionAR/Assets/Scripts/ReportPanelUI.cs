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
        private Dropdown priorityDropdown;  // 优先级：high/medium/low
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
            // 复用 toast 区域显示标记详情
            if (toastText != null)
            {
                toastText.text = string.Format("[{0}] {1}\n{2}\n{3}", priority, title, description, positionText);
                toastText.fontSize = 34;
                toastText.alignment = TextAnchor.MiddleLeft;
            }
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
            var desc = descInput.text.Trim();
            if (desc.Length == 0)
            {
                ShowToast("请填写描述");
                return;
            }
            // 若尚未在 AR 平面放置标记，用当前相机前方位置兜底（保证功能连通）
            var pose = controller != null && controller.HasPlacement
                ? controller.CurrentPose
                : new Pose(Vector3.zero, Quaternion.identity);

            var priority = priorityDropdown.options[priorityDropdown.value].text;
            var json = MarkerJson.BuildCreateJson(title, desc, priority, pose);
            Debug.Log("[ReportPanel] 提交 JSON: " + json);
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
                        priorityDropdown.options[priorityDropdown.value].text);
                }
            }
        }

        void ShowToast(string msg)
        {
            if (toastText != null)
            {
                toastText.text = msg;
            }
            Debug.Log("[ReportPanel] " + msg);
        }

        // ---- UI 构建（简洁大字）----

        void BuildUI()
        {
            panel = new GameObject("ReportPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.55f); // 占屏幕下半 55%
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 16;

            // 标题输入
            titleInput = CreateInputField("TitleInput", "标题（如：3号楼前地面破损）");

            // 描述输入
            descInput = CreateInputField("DescInput", "描述（如：地面有约30cm裂缝）");

            // 优先级下拉
            priorityDropdown = CreateDropdown("PriorityDropdown");
            priorityDropdown.ClearOptions();
            priorityDropdown.AddOptions(new System.Collections.Generic.List<string> { "high", "medium", "low" });

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

            // Toast 提示
            toastText = CreateLabel("Toast", "");
            toastText.alignment = TextAnchor.MiddleCenter;
            toastText.fontSize = 40;
            toastText.color = new Color(1f, 0.9f, 0.3f, 1f);

            // 面板初始隐藏，等 AR 放置后弹出
            panel.SetActive(false);
        }

        // ---- 控件工厂（统一大字 40-44px）----

        Dropdown CreateDropdown(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var dd = go.AddComponent<Dropdown>();
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var txt = labelGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 42;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleLeft;
            dd.captionText = txt;
            return dd;
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
            ph.fontSize = 40;
            ph.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            ph.text = placeholder;
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 40;
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
            txt.fontSize = 40;
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
            txt.fontSize = 44;
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
