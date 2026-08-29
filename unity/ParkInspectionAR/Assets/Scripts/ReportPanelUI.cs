// ReportPanelUI.cs —— 底部表单面板（Task 4）：
// type 下拉 + title 输入 + reporter 输入（预填"张巡检"）+ 确认上报 + 失败重试 + toast 提示。
// UI 全部代码动态构建：SceneBuilder 只需创建 UICanvas 根，本类挂上去自动生成子控件。
// 为什么用 UnityEngine.UI 原生组件而非 TextMeshPro：
// TMP 需要导入 Essential Resources 字体资产，原型阶段引入额外依赖属于过度设计；
// 原生 Text/Dropdown/InputField 零依赖、动态创建即用。
using UnityEngine;
using UnityEngine.UI;

namespace ParkInspectionAR
{
    public class ReportPanelUI : MonoBehaviour
    {
        private ARMarkerController controller;
        private MarkerSubmitter submitter;

        // UI 引用
        private GameObject panel;          // 表单容器（默认隐藏）
        private Dropdown typeDropdown;     // 类型下拉
        private InputField titleInput;     // 标题输入
        private InputField reporterInput;  // 巡检员输入
        private Button confirmBtn;         // 确认上报
        private Button retryBtn;           // 重试（仅失败时显示）
        private Text toastText;            // 提示文本

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

        // 显示/隐藏表单
        public void ShowPanel(bool show)
        {
            if (panel != null)
            {
                panel.SetActive(show);
            }
        }

        // 确认上报：收集表单 → 构造 JSON → 提交
        void OnConfirmClick()
        {
            if (controller == null || !controller.HasPlacement)
            {
                ShowToast("请先在平面上放置标注位置");
                return;
            }

            var title = titleInput.text.Trim();
            if (title.Length == 0)
            {
                ShowToast("请填写标题");
                return;
            }

            var reporter = reporterInput.text.Trim();
            if (reporter.Length == 0)
            {
                ShowToast("请填写巡检员");
                return;
            }

            // 原型阶段 geo 传 null：不启用 GPS（LocationService 需要权限配置，属过度设计）
            var json = MarkerJson.BuildCreateJson(
                typeDropdown.options[typeDropdown.value].text, // 契约枚举：equipment/hazard/route_point/other
                title,
                "",
                controller.CurrentPose,
                null,
                reporter);

            submitter.Submit(json);
            ShowToast("上报中…");
        }

        // 重试：用缓存的 JSON 直接重发
        void OnRetryClick()
        {
            if (submitter != null)
            {
                retryBtn.gameObject.SetActive(false);
                submitter.Retry();
            }
        }

        // 上报结果回调
        void OnSubmitResult(bool success, string message)
        {
            ShowToast(message);
            if (success)
            {
                retryBtn.gameObject.SetActive(false);
                controller.ConfirmVisual(); // 预览体转实体色
                ShowPanel(false);           // 隐藏表单
            }
            else
            {
                // 失败：显示重试按钮（缓存已有数据）
                retryBtn.gameObject.SetActive(true);
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

        // ---- UI 动态构建 ----

        void BuildUI()
        {
            panel = new GameObject("ReportPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(transform, false);
            var rt = panel.GetComponent<RectTransform>();
            // 锚到底部：占屏幕下方 45% 高度（给表单控件留足空间）
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.45f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f); // 半透明黑底，AR 画面上可读

            // 类型下拉（契约枚举：equipment/hazard/route_point/other）
            typeDropdown = CreateDropdown("TypeDropdown");
            typeDropdown.ClearOptions();
            typeDropdown.AddOptions(new System.Collections.Generic.List<string>
                { "hazard", "equipment", "route_point", "other" });

            // 标题输入
            titleInput = CreateInputField("TitleInput", "标注标题（如：3号配电箱外壳破损）");

            // 巡检员输入（预填"张巡检"，确认书决策：客户端输入预填默认值）
            reporterInput = CreateInputField("ReporterInput", "巡检员姓名");
            reporterInput.text = "张巡检";

            // 按钮行
            var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(panel.transform, false);
            var btnRowRt = btnRow.GetComponent<RectTransform>();
            btnRowRt.anchorMin = new Vector2(0f, 0f);
            btnRowRt.anchorMax = new Vector2(1f, 0f);
            btnRowRt.offsetMin = new Vector2(10f, 10f);
            btnRowRt.offsetMax = new Vector2(-10f, 70f);

            confirmBtn = CreateButton("ConfirmBtn", "确认上报", btnRow.transform);
            confirmBtn.onClick.AddListener(OnConfirmClick);

            retryBtn = CreateButton("RetryBtn", "重试", btnRow.transform);
            retryBtn.onClick.AddListener(OnRetryClick);
            retryBtn.gameObject.SetActive(false); // 默认隐藏

            // Toast 文本（按钮行上方）
            var toastGo = new GameObject("Toast", typeof(RectTransform));
            toastGo.transform.SetParent(panel.transform, false);
            var toastRt = toastGo.GetComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0f, 0f);
            toastRt.anchorMax = new Vector2(1f, 0f);
            toastRt.offsetMin = new Vector2(10f, 80f);
            toastRt.offsetMax = new Vector2(-10f, 110f);
            toastText = toastGo.AddComponent<Text>();
            toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 2022.3 内置字体
            toastText.fontSize = 28;
            toastText.color = Color.white;
            toastText.alignment = TextAnchor.MiddleCenter;

            panel.SetActive(false); // 初始隐藏，等放置后弹出
        }

        // ---- 控件工厂（全部原生 uGUI，动态创建）----

        Dropdown CreateDropdown(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var dd = go.AddComponent<Dropdown>();
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var txt = label.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 28;
            txt.color = Color.black;
            dd.captionText = txt;
            return dd;
        }

        InputField CreateInputField(string name, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var input = go.AddComponent<InputField>();
            // 占位文本
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(go.transform, false);
            var ph = phGo.AddComponent<Text>();
            ph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ph.fontSize = 28;
            ph.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            ph.text = placeholder;
            // 输入文本
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 28;
            txt.color = Color.black;
            txt.supportRichText = false;
            input.textComponent = txt;
            input.placeholder = ph;
            return input;
        }

        Button CreateButton(string name, string label, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.6f, 1f, 1f);
            var btn = go.AddComponent<Button>();
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 32;
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
