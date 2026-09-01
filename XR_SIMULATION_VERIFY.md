# XR Simulation 手动验证清单（无需 GMS，Editor 内跑通 AR 链路）

> 用途：不装 GMS、不上真机，用 Unity 官方 XR Simulation 在编辑器里验证
> 「识别平面 → 点击投放 → 填写提交 → Go 落库 → React 查看/改状态 → 重启持久」整条闭环。
> 本清单配合本轮新增的顶部状态栏（ARStatusHud）逐项确认，每步都有可观察的判据。

---

## 0. 前置准备

### 0.1 启动 Go 后端（与 Unity 编辑器同机）
```powershell
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
cd server
go build ./...
.\server.exe            # 监听 :8080，SQLite 落盘 park-inspection.db
```
验证：浏览器打开 `http://localhost:8080/healthz` 返回 `{"code":0,...}`。

### 0.2 修正 Unity 端后端地址（关键，同机必改）
`D:\UnityProjects\ParkInspectionAR\Assets\Scripts\MarkerSubmitter.cs` 里
`serverBaseUrl` 当前是 `http://192.168.8.111:8080`（真机局域网 IP）。
XR Simulation 与后端**同一台电脑**，改成：
```csharp
public string serverBaseUrl = "http://localhost:8080";
```
> 不改也能跑 AR 交互，但"提交"会报「无法连接服务器」。

### 0.3 打开工程与场景
- 工程：`D:\UnityProjects\ParkInspectionAR`
- 场景：`Assets/Scenes/Main.unity`（已由 SceneBuilder 生成，含 XR Origin / AR Session / Plane/Raycast Manager / UICanvas / 状态栏）

### 0.4 确认 XR Simulation 已启用（一般已配好，只在异常时查）
`Edit → Project Settings → XR Plug-in Management → Windows/Mac/Linux 标签页` → 勾选 **XR Simulation**。
模拟环境：`Window → XR → AR Foundation → XR Environment` → 环境下拉选 **DefaultSimulationEnvironment**。

---

## 1. Play 跑通 AR 交互（核心，对应任务书 3.1）

| # | 操作 | 期望观察 |
|---|---|---|
| 1 | 点 **Play** | 顶部状态栏出现文字，且依次变化 |
| 2 | 观察顶部状态栏 | `正在检查 AR 支持…` → `AR 会话初始化中…` → `正在检测平面…` |
| 3 | 右键按住拖动=环顾，**WASD**=移动，**Q/E**=上下，**Shift**=加速；把视角对准地面/桌面 | 相机在场景内自由移动 |
| 4 | 缓慢扫过地面 | 状态栏变 **`已识别 N 个平面，点击地面放置标记`**，且地面出现**青色半透明网格 + 描边** |
| 5 | **左键点击**某个已识别平面（非 UI 区域） | 出现青色预览立方体 + 底部弹出表单面板，位置自动填 `x/y/z` |
| 6 | 填标题（必填）；描述**留空**（可选）；选优先级 high/medium/low | 无"请填写描述"拦截 |
| 7 | 点 **提交** | 提示 `提交中…` → `已上报`（失败则提示 + 重试按钮） |
| 8 | 提交成功后 | 预览立方体变实心，代表已转为正式标记 |

> 说明：左键=模拟"点击平面"；右键=导航环顾。点击时避开顶部状态栏和底部表单区域（点在 UI 上会被忽略，这是防穿透设计）。

---

## 2. 闭环验证（Unity → Go → React，对应演示视频要求）

1. 启动 React 管理端：
   ```powershell
   cd web
   npm run dev
   ```
2. 浏览器打开 React 页面 → 刷新，看到刚提交的问题（标题 / 优先级 / 状态 `open`）。
3. 把状态改为 `in_progress` → 再改 `resolved`，页面正常更新。
4. **重启 Go**（终端 Ctrl+C 停掉，再 `.\server.exe`）→ 刷新 React，问题仍在（SQLite 落盘持久化）。

---

## 3. 排查对照表

| 现象 | 原因 / 处理 |
|---|---|
| 状态栏停 `需要安装 Google Play Services for AR` 或 `AR 不可用` | XR Simulation loader 未激活（这是编辑器模拟器，与真机 ARCore 无关）。去 0.4 勾选 XR Simulation |
| 一直 `正在检测平面…`，看不到平面 | 相机没对准可检测表面；缓慢移动扫描地面；确认环境选的是 DefaultSimulationEnvironment |
| 状态栏 `已识别 N 个平面` 但看不到网格 | 检查 `ARPlaneManager.planePrefab` 是否指向 `Assets/Prefabs/ARPlane.prefab`；重新跑 `Tools → 园区巡检AR → 一键搭建场景` |
| 左键点击无反应 | 点在 UI 上被忽略（换平面区域点）；或 EventSystem 缺失 |
| 相机画面不动 | TrackedPoseDriver 位姿输入异常，看 Console 是否有 Input 相关报错 |
| 提交报 `无法连接服务器` | `serverBaseUrl` 未改 `http://localhost:8080`；或 Go 没启动 |
| 提交报解析/参数错误 | 看 Go 端日志；标题为空会返回明确参数错误（属预期） |

---

## 4. 通过标准（打勾即完成选项 B）

- [ ] 能识别平面（状态栏计数 + 可见青色网格）
- [ ] 点击平面能投放标记
- [ ] 提交成功/失败都有明确提示
- [ ] React 刷新可见该问题，且能改状态
- [ ] Go 重启后数据仍在
