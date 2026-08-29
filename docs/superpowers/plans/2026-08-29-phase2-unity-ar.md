# Phase 2: Unity AR 端实施计划（unity/ParkInspectionAR）

> **For agentic workers:** REQUIRED SUB-SKILL: executing-plans（Inline 逐任务执行，用户已确认）。

**Goal:** AR Foundation 5.x Android 上报端：UI/AR 点击区分 + 平面放置预览 + POST 上报 + 失败重试，命令行构建 APK 编译通过，真机走查由用户操作。

**Architecture:** 命令行创建空工程 → 依赖注入（AR Foundation/ARCore/InputSystem）→ SceneBuilder Editor 菜单一键搭场景 → 4 个 C# 脚本（数据/控制器/上报/UI）→ batchmode 构建验证。

**Tech Stack:** Unity 2022.3.62f2（中国区版 c1）/ AR Foundation 5.1.5 / ARCore XR Plugin 5.1.5 / Input System 1.7.0 / C#。

**Spec:** `docs/superpowers/specs/2026-08-29-三端开发顺序与接口契约确认书.md` + `docs/api-contract.md` v0.1

---

## Global Constraints

1. 关键代码中文注释（为什么，非逐行翻译）。
2. 必须 `EventSystem.IsPointerOverGameObject(fingerId)` 带触点参数区分 UI/AR 点击（真机必须）。
3. AR 射线只检测 `TrackableType.PlaneWithinPolygon`。
4. 严禁编造 Unity API；本计划所有 API 均为 AR Foundation 5.x 真实存在，如存疑必须询问。
5. 上报地址用局域网 IP（真机不能访问 localhost），可在 Inspector 配置。
6. 每任务结束 git commit（Unity 工程二进制不入库，只入 Assets/Packages/ProjectSettings）。

---

### Task 1: 命令行创建空工程 + 依赖注入

**Files:**
- Create: `unity/ParkInspectionAR/Packages/manifest.json`（含 AR Foundation 5.1.5 / ARCore 5.1.5 / Input System 1.7.0）
- 由 Unity batchmode 生成其余工程文件

**Interfaces:**
- Produces: 可打开的 Unity 2022.3.62f2 工程（带 AR 依赖）。

- [ ] Step 1: `Unity.exe -batchmode -quit -createProject unity/ParkInspectionAR`
- [ ] Step 2: 写入 manifest.json 注入依赖
- [ ] Step 3: batchmode 重开一次工程解析依赖
- [ ] Step 4: git commit `feat(unity): Task1 工程创建 + AR 依赖注入`

### Task 2: MarkerData.cs —— 契约数据结构（JSON 对齐）

**Files:**
- Create: `unity/ParkInspectionAR/Assets/Scripts/MarkerData.cs`

**Interfaces:**
- Produces: `[Serializable] class PositionData/RotationData/GeoData/CreateMarkerRequest`，`[Serializable] class ApiEnvelope`（含 `MarkerData` 完整实体），`static string BuildCreateJson(...)`。
- Consumes: 契约字段（docs/api-contract.md v0.1）。

- [ ] Step 1: 写 MarkerData.cs（json 字段名与契约驼峰一致，用 JsonUtility）
- [ ] Step 2: commit `feat(unity): Task2 契约数据结构`

### Task 3: SceneBuilder.cs —— Editor 菜单一键搭场景

**Files:**
- Create: `unity/ParkInspectionAR/Assets/Editor/SceneBuilder.cs`

**Interfaces:**
- Produces: 菜单 `Tools/园区巡检AR/一键搭建场景`：创建 AR Session、AR Session Origin（含 Camera + AR Raycast Manager）、Canvas（ReportPanel）、EventSystem（含 InputSystemUIInputModule）。
- Consumes: 场景对象命名约定（`AR Session`/`AR Session Origin`/`UICanvas`/`EventSystem`）。

- [ ] Step 1: 写 SceneBuilder.cs（Editor 脚本，EditorApplication.delayCall 保证可撤销）
- [ ] Step 2: commit `feat(unity): Task3 场景生成器`

### Task 4: 核心脚本 —— ARMarkerController + ReportPanelUI + MarkerSubmitter

**Files:**
- Create: `unity/ParkInspectionAR/Assets/Scripts/ARMarkerController.cs`、`ReportPanelUI.cs`、`MarkerSubmitter.cs`

**Interfaces:**
- Consumes: `MarkerData` 类型、`SceneBuilder` 场景对象、`ARRaycastManager`（场景内引用）。
- Produces: `ARMarkerController`（触摸→IsPointerOverGameObject 区分→Raycast→放置预览→回调面板）、`ReportPanelUI`（表单+确认/重试按钮+toast）、`MarkerSubmitter`（POST + 解析 + 缓存重试）。

- [ ] Step 1: 写三个脚本（关键：fingerId 触点参数；PlaneWithinPolygon；局域网 IP 可配置）
- [ ] Step 2: commit `feat(unity): Task4 交互/上报/UI 脚本`

### Task 5: 命令行构建 Android APK + 验收文档

**Files:**
- Create: `unity/ParkInspectionAR/README.md`
- 构建产物 `Builds/ParkInspectionAR.apk`（不入库）

**Interfaces:**
- Consumes: 全部脚本 + 场景 + manifest 依赖。
- Produces: 可安装 APK；README（如何打开场景/配置 IP/构建/真机走查步骤）。

- [ ] Step 1: 命令行构建 APK（batchmode，Android 平台，IL2CPP 或 Mono）
- [ ] Step 2: 修编译错误直至 APK 产出
- [ ] Step 3: README + 最终 commit

---

## 验收清单（Task 5 执行）

1. `Unity.exe -batchmode -buildTarget Android` 产出 `Builds/ParkInspectionAR.apk` 无错误。
2. README 包含：打开工程步骤、SceneBuilder 菜单用法、局域网 IP 配置、真机走查步骤（放置→上报→curl 验证→React 可见）。
3. 真机走查（用户操作）：AR 平面放置预览 → 表单 → 上报 → Go 收到 → React 页面出现新标注。
