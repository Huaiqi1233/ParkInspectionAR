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
