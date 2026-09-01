# AI 使用记录（AI_log.md）

> 按实际顺序记录向 AI 提问/命令的过程（任务书第 5 部分要求）。
> 保留 Prompt 原文，不泄露敏感信息。

## P-001 项目启动与角色设定

**目标**：确立全栈 AR 原型主程角色与三端技术栈。

**Prompt 原文（节选）**：设定角色为"全栈AR原型主程（Unity + Go + React）"，核心目标打通 Unity(AR标记上报) → Go(存储) → React(管理展示) 闭环，硬性约束 Unity 用 AR Foundation 5.x、Go 用 Gin/SQLite、React 用 TS+Axios+Error Boundary，行动协议要求先输出三步骤再写代码。

**实际发送的命令**：接受了角色设定，初始化 OpenWolf 索引项目根目录，确认空项目状态，询问首个任务。

**结果**：确立三端开发顺序（先契约 → Go → React → Unity，后因本机无 Unity 调整为 Go → React → Unity）。

**是否修改**：开发顺序中途调整（Unity 环境后置）。

---

## P-002 数据契约与 API 设计

**目标**：定义三端共享 JSON 契约与 API 端点。

**Prompt 原文**：定义三端JSON契约 + API端点清单（按行动协议 Step1/Step2）。

**实际发送的命令**：输出 Marker 实体、响应信封、6 个 API 端点、SQLite 建表、curl 示例。

**结果**：生成 docs/api-contract.md v0.1，用户确认。

**是否修改**：是。**重大修正**——后续用户出示真实任务书后，发现 v0.1 字段设计偏离需求（详见 P-009）。

---

## P-003 Go 后端实现

**目标**：实现 Gin + SQLite 后端。

**实际发送的命令**：按计划 5 任务实现 models/store/handlers/main，验收脚本全绿。

**结果**：Go 后端完成，8/8 验收通过（v0.1 契约）。

**是否修改**：是，后续对齐 v2.0 契约重构。

---

## P-004 React 管理端实现

**目标**：实现 React 管理端（列表/筛选/状态流转/Error Boundary）。

**实际发送的命令**：Vite + TS + Axios + Error Boundary，列表/筛选/分页/状态流转/删除。

**结果**：React 管理端完成，端到端验收通过。

**是否修改**：是，后续对齐 v2.0（priority/status 三态）重构。

---

## P-005 Unity 环境安装

**目标**：本机安装 Unity Editor 供 AR 端开发。

**实际发送的命令**：用户问"你不能帮我装 unity 吗"，我自动下载安装 Unity Hub + Editor 2022.3.62f2（china CDN）+ Android Build Support + JDK/SDK/NDK。

**结果**：Unity 2022.3.62f2（中国区版）+ Android 工具链完整就绪，APK 构建成功。

**踩坑记录**：① 工程路径含中文导致 Android 构建失败，用 ASCII 路径 + junction 解决；② Unity 2022.3 只认 JDK 11；③ SDK 需 cmdline-tools 6.0；④ NDK 需 r23b。

---

## P-006 Unity AR 端实现

**目标**：AR Foundation 5.x 上报端（UI/AR 点击区分 + 平面放置 + 上报）。

**实际发送的命令**：SceneBuilder 一键搭场景 + ARMarkerController（IsPointerOverGameObject 区分）+ MarkerSubmitter + ReportPanelUI。

**结果**：编译通过 + APK 构建成功。

**是否修改**：是，UI 重构（用户反馈冗余、字小）→ 对齐 v2.0 简洁大字 UI。

---

## P-007 真机走查（GMS + 网络）

**目标**：iQOO Neo 9 Pro 真机 AR 走查。

**实际发送的命令**：检查 GMS/ARCore 状态 → 发现国行 Stub → Play 商店升级 ARCore → 配置网络。

**结果**：ARCore 升级到 1.54 完整版，摄像头画面正常显示。

**踩坑记录**：① 国行 iQOO 无 GMS，ARCore 是 Stub 空壳需升级；② 手机(192.168.1.x)与电脑(192.168.8.x)不同网段，切换 WiFi 后同网段打通；③ 防火墙 profile 不匹配（Public vs Private）。

---

## P-008 黑屏与平面检测排查

**目标**：解决真机黑屏 + 点击无上报。

**实际发送的命令**：抓 logcat → 发现 ARCore Loader 未在 Android 平台启用 → 启用后摄像头正常；再发现 raycast hits=0（平面未检测到）。

**结果**：黑屏根因（ARCore Loader 未启用）已解决；平面检测未命中是 ARCore 对纯色无纹理表面检测不到（环境限制）。

**是否修改**：此轮我一度偏离——去加"平面可视化"这种需求外功能，被用户纠正"处理问题的逻辑有问题，重复阅读需求"。

---

## P-009 需求对齐修正（关键转折）

**目标**：用户出示真实任务书，指出我之前的字段设计偏离需求。

**Prompt 原文（任务书要点）**：
- 数据结构：`id, title, description, priority(high), status(open), position(x,y,z), createdAt, updatedAt`
- 必做功能：Unity 表单（标题/描述/优先级/位置x/y/z）+ 点击标记查看输入；React 显示标题/优先级/状态 + 改状态(open/in_progress/resolved)；Go 防重放
- 交付：源码 + README + AI_log.md + 演示视频

**实际发送的命令**：重读需求，发现偏差——我之前凭空加了 type/rotation/geo/reporter/photoUrl 字段，漏了 priority、description 必填、status 三态、点击查看、防重放、AI_log。

**结果**：契约升级到 v2.0，三端重构对齐任务书。

**是否修改**：是，这是最重大的一次修正。

---

## P-010 三端 v2.0 重构

**目标**：对齐任务书重构三端。

**实际发送的命令**：
- 契约 v2.0：priority 字段、status 三态(open/in_progress/resolved)、position 仅 x/y/z、去 rotation/geo/reporter/photoUrl/type
- Go：models/store/handlers 重构 + 防重放（dedup_hash 唯一索引）
- Unity：MarkerData 新模型 + 简洁大字 UI（标题/描述/优先级/位置）+ 点击标记查看
- React：显示标题/优先级/状态 + 状态流转

**结果**：Go 验收 13/13 通过（含防重放 409）；Unity/React 编译通过。

**是否修改**：进行中。

---

## P-011 需求理解与 agent 文件固化

- **目标**：理解题目《现场AR问题标注》并固化为可复用文档
- **Prompt 原文**：
  > 根据图内文字，理解该项目
  > 将你理解的项目需求保存为agent文件以便后续开发
- **结果**：修改后采用 —— 产出 `PROJECT_REQUIREMENTS.md`（9 节：背景/技术栈/三端必做/数据结构/AI使用/交付/加分/评分/进度快照），并在 `AGENTS.md` 加指针。验证：后续会话能据此接手。

## P-012 真机「平面无法识别/无标记/无UI」排查

- **目标**：解决真机三个连锁症状
- **Prompt 原文**：
  > 手机应用遇到的问题：平面无法识别、标记无法投放、应用内没有对应 UI，寻找现有开源方案直接挪用
- **结果**：修改后采用 —— 诊断出根因链（无 GMS → 无 ARSession → 无平面 → 射线打不中 → 面板不弹），叠加真 bug（`planePrefab` 为空 → 平面不可见）。借用官方 `ARPlaneMeshVisualizer` 生成 `ARPlane.prefab` + 新增 `ARStatusHud` 顶部状态栏。验证：Unity 编译通过 + SceneBuilder 实跑生成资产。

## P-013 真机点击平面无反应（shader 被裁剪）

- **目标**：点击平面不出现预览立方体/表单
- **结果**：修改后采用 —— logcat 抓到 `ArgumentNullException: Parameter name: shader`，根因 `Shader.Find("Standard")` 在 Android 构建被裁剪返回 null。改用 `Sprites/Default`。验证：真机复测点击出现预览。

## P-014 真机提交卡在「提交中…」（HTTP 明文被拦）

- **目标**：提交不返回结果
- **结果**：修改后采用 —— logcat 抓到 `InvalidOperationException: Insecure connection not allowed`。`BuildScript` 加 `PlayerSettings.insecureHttpOption = AlwaysAllowed`。验证：数据落库（后端出现记录）。

## P-015 真机优先级下拉无法改变

- **目标**：表单「优先级」点不动
- **结果**：修改后采用 —— 程序化 `Dropdown` 缺 template/targetGraphic 弹不出列表。改用 3 个按钮（high/medium/low）+ `selectedPriority`。验证：编译通过、真机可选。

## P-016 React 一直显示「后端服务不可用」

- **目标**：React 列表页误报后端不可用
- **结果**：修改后采用 —— 前端 `client.ts` 拦截器返回完整响应而非 data，组件 `data.items` 为 undefined → 渲染崩溃被 ErrorBoundary 误报。改为 `return body.data`。验证：`tsc -b` 通过、刷新可见数据。

## P-017 提交成功后面板不消失 + 描述可选冲突

- **目标**：成功后 UI 不消失、无法继续放置；「描述」可选与后端必填冲突
- **结果**：修改后采用 —— ① 提交成功隐藏面板 + 清空表单，toast 独立于面板；② Go `handlers.go` 描述改可选（≤256），`docs/api-contract.md` 同步改「可选」。验证：见本轮真机复测 + 后端验收。

---

## P-018 GPS 跨设备定位（方案 A）

- **目标**：用户指出 `position{x,y,z}` 是 AR 会话内相对坐标，第二个人无法据此找到点位——需要跨设备定位方案
- **Prompt 原文**：
  > 这种坐标交给另一个人来寻找的时候怎么可能会找到对应点位呢
  > 考虑 a（GPS 经纬度 + 地图链接）
- **结果**：修改后采用 —— 三端加可选 `location{lat,lng}`：
  - Unity：`AndroidManifest.xml` 加 FINE/COARSE 定位权限 + `GpsLocator`（`Input.location` 读取）+ 提交带经纬度
  - Go：`Location` 模型 + SQLite 迁移（老库 ALTER 补列）+ 范围校验（(0,0)=未定位）
  - React：显示经纬度 + 「📍 地图」链接（高德 `uri.amap.com/marker?position=lng,lat`）
  - 验证：Go 接口实测（带 location 存 31.2304,121.4737；不带则 0,0）；React `tsc` 通过；APK 构建 + aapt 确认权限已进包；真机实测 GPS 坐标/±精度/地图链接可见

---

## P-019 GPS 精度显示 + 现场照片（方案 A 增强 + C）

- **目标**：用户反馈 GPS 定位不准、AR 不够"智能"——需要让人能更精确找到点位
- **Prompt 原文**：
  > 定位不是特别准 感觉是ar功能没那么智能
  > c+a（现场照片 + 精度显示）
- **结果**：修改后采用 ——
  - Unity：`PhotoCapture`（截屏→降采样 480px→JPEG 55→base64）+ `GpsLocator.TryGet` 增加 accuracy 输出，提交带照片与精度
  - Go：`location` 加 `accuracy` 字段 + `photo` 字段（base64≤1MB）+ SQLite 迁移（acc/photo 列）
  - React：显示 GPS ±精度（`±8.5m`）+ 现场照片缩略图（点击看大图）
  - 验证：Go 接口实测（accuracy=8.5、photo 落库）；React `tsc` 通过；APK 构建 + 真机运行（ARCore 会话正常初始化）
